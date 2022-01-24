using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ACT.DieMoe.Downloader
{
	public class NyaDownloader
	{
		const int HTTP_TIME_OUT = 15000;
		HttpClient client;
		int downloadThreadCount;
		long CHUNK_MAX_SIZE;
		string fileSavePath;
		string downloadFileUrl;
		string fileName;
		List<downloadChunkInfo> chunkList;
		public long downloadSize;
		public long fileSize;
		private object locker = new object();
		public FileDownloadProcess fileDownloadProcess { get; set; } = null;
		public FileDownloadFinish fileDownloadFinishCallBack { get; set; } = null;
		public delegate void FileDownloadProcess(float downloadSize);
		public delegate void FileDownloadFinish();
		public int downloadFinishChunkNum = 0;
		public NyaDownloader(HttpClient downloadHttpClient, string fileHttpUrl, int downloadThread, long downloadChunkSize, string downloadFileSavePath, string downloadFileName)
		{
			client = downloadHttpClient;
			downloadThreadCount = downloadThread;
			CHUNK_MAX_SIZE = downloadChunkSize;
			fileSavePath = downloadFileSavePath;
			downloadFileUrl = fileHttpUrl;
			fileName = downloadFileName;
		}
		public void startDownload()
		{
			long fileSize = getDownloadFileFullSize();
			chunkList = getDownloadChunks(fileSize);
			this.fileSize = fileSize;
			for (int i = 0; i < downloadThreadCount; i++)
			{
				new Task(() => { downloadThread(); }).Start();
				Thread.Sleep(10);
			}
		}
		public void downloadThread()
		{
			for (int i = 0; i < chunkList.Count; i++)
			{
				if (!chunkList[i].isBeginDownload)
				{
					var copy = chunkList[i];
					copy.isBeginDownload = true;
					chunkList[i] = copy;
					for (int retryTime = 0; retryTime < 5; retryTime++)
					{
						try
						{
							Console.WriteLine("download chunk {0}", i);
							downloadChunks(chunkList[i]);
							Console.WriteLine("chunk{0} downloadFinish", i);
							break;
						}
						catch (Exception ex)
						{
							if (retryTime == 5)
							{
								throw ex;
							}
							lock (locker) downloadSize -= CHUNK_MAX_SIZE;
							Console.WriteLine("chunk {0} retry", i);
						}
					}
				}
			}
		}
		public void downloadChunks(downloadChunkInfo info)
		{
			Stream httpFileStream = null, localFileStream = null;
			try
			{
				// 检查是否存在上次的临时文件
				if (File.Exists(info.tempFilePath))
				{
					File.Delete(info.tempFilePath);
				}
				HttpWebRequest downloadRequest = (HttpWebRequest)WebRequest.Create(downloadFileUrl);
				downloadRequest.Timeout = HTTP_TIME_OUT;
				downloadRequest.KeepAlive = true;
				if (info.downloadSize != 0)
				{
					downloadRequest.AddRange(info.startRange, info.startRange + info.downloadSize);
				}
				else
				{
					downloadRequest.AddRange((info.startRange + (info.chunkIndex != 0 ? 1 : 0)), info.startRange + CHUNK_MAX_SIZE);
				}
				HttpWebResponse downloadResponse = (HttpWebResponse)downloadRequest.GetResponse();
				httpFileStream = downloadResponse.GetResponseStream();
				localFileStream = new FileStream(info.tempFilePath, FileMode.Create);
				byte[] buffer = new byte[8192];
				int getByteSize = httpFileStream.Read(buffer, 0, buffer.Length);
				while (getByteSize > 0)
				{
					lock(locker)downloadSize += getByteSize;
					localFileStream.Write(buffer, 0, getByteSize);
					getByteSize = httpFileStream.Read(buffer, 0, buffer.Length);
					fileDownloadProcess?.Invoke(downloadSize);
				}
			}
			catch (Exception ex)
			{
				throw ex;
			}
			finally
			{
				if (httpFileStream != null) httpFileStream.Dispose();
				if (localFileStream != null) localFileStream.Dispose();
			}
			lock(locker)downloadFinishChunkNum++;
			if (downloadFinishChunkNum == chunkList.Count)
			{
				Complete();
			}
		}
		public List<downloadChunkInfo> getDownloadChunks(long fileSize)
		{
			List<downloadChunkInfo> downloadChunks = new List<downloadChunkInfo>();
			double chunkNums = (double)fileSize / (double)CHUNK_MAX_SIZE;
			double numChunk = Math.Ceiling(chunkNums);
			long lastChunkSize = fileSize % CHUNK_MAX_SIZE;
			for (int i = 0; i < numChunk-1; i++)
			{
				downloadChunks.Add(new downloadChunkInfo()
				{
					chunkIndex = i,
					startRange = i * CHUNK_MAX_SIZE,
					isBeginDownload = false,
					tempFilePath = Path.Combine(Path.GetTempPath(), String.Format("{0}_{1}.tmp", fileName, i))
				});
			}
			if (lastChunkSize != 0)
			{
					downloadChunks.Add(new downloadChunkInfo()
					{
						chunkIndex = downloadChunks.Count,
						startRange = downloadChunks.Count* CHUNK_MAX_SIZE + 1,
						isBeginDownload = false,
						downloadSize = lastChunkSize,
						tempFilePath = Path.Combine(Path.GetTempPath(), String.Format("{0}_{1}.tmp", fileName, i))
					});
			}
			return downloadChunks;
		}
		public long getDownloadFileFullSize()
		{
			try
			{
				HttpWebRequest downloadFileRequest = (HttpWebRequest)WebRequest.Create(downloadFileUrl);
				downloadFileRequest.Timeout = HTTP_TIME_OUT;
				long size = downloadFileRequest.GetResponse().ContentLength;
				downloadFileRequest.Abort();
				return size;
			}
			catch (Exception e)
			{
				throw e;
			}
		}

		public void Complete()
		{
			Stream mergeFile = new FileStream(Path.Combine(fileSavePath, fileName), FileMode.Create);
			BinaryWriter AddWriter = new BinaryWriter(mergeFile);
			chunkList.Sort((a, b) =>
			{
				if (Convert.ToInt32(a.tempFilePath.Split('_').Last().Split('.').First()) > Convert.ToInt32(b.tempFilePath.Split('_').Last().Split('.').First()))
					return 1;
				else
					return -1;
			});
			foreach (downloadChunkInfo file in chunkList)
			{
				using (FileStream fs = new FileStream(file.tempFilePath, FileMode.Open))
				{
					BinaryReader TempReader = new BinaryReader(fs);
					AddWriter.Write(TempReader.ReadBytes((int)fs.Length));
					TempReader.Close();
				}
				File.Delete(file.tempFilePath);
			}
			AddWriter.Close();
			fileDownloadFinishCallBack?.Invoke();
		}

		public struct downloadChunkInfo
		{
			public int chunkIndex;
			public bool isBeginDownload;
			public long startRange;
			public string tempFilePath;
			public long downloadSize;
			public bool isFinish;
		}
	}
}

