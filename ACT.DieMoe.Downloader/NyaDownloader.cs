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
					int retryTime = 0;
					retry:
					try
					{
						Console.WriteLine("download chunk {0}",i);
						downloadChunks(chunkList[i]);
						chunkDownloadFinish();
						Console.WriteLine("chunk{0} downloadFinish",i);
					}
					catch (Exception ex)
					{
						if (retryTime == 5)
						{
							throw ex;
						}
						Console.WriteLine("chunk {0} retry",i);
						retryTime++;
						goto retry;
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
					downloadRequest.AddRange(info.startRange, info.startRange + info.downloadSize);
				else
					downloadRequest.AddRange(info.startRange, info.startRange + CHUNK_MAX_SIZE);
				HttpWebResponse downloadResponse = (HttpWebResponse)downloadRequest.GetResponse();
				httpFileStream = downloadResponse.GetResponseStream();
				localFileStream = new FileStream(info.tempFilePath, FileMode.Create);
				byte[] buffer = new byte[8192];
				int getByteSize = httpFileStream.Read(buffer, 0, buffer.Length);
				while (getByteSize > 0)
				{
					localFileStream.Write(buffer, 0, getByteSize);
					getByteSize = httpFileStream.Read(buffer, 0, buffer.Length);
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
		}
		public void chunkDownloadFinish()
		{
			for (int i = 0; i < chunkList.Count; i++)
			{
				if (!chunkList[i].isFinish)
				{
					break;
				}
			}
			// 合并
		}
		public List<downloadChunkInfo> getDownloadChunks(long fileSize)
		{
			List<downloadChunkInfo> downloadChunks = new List<downloadChunkInfo>();
			decimal numChunk = Math.Ceiling((decimal)(fileSize / CHUNK_MAX_SIZE));
			long lastChunkSize = fileSize % CHUNK_MAX_SIZE;
			for (int i = 0; i < numChunk; i++)
			{
				if (lastChunkSize != 0)
				{
					if (i == numChunk-1)
					{
						downloadChunks.Add(new downloadChunkInfo()
						{
							chunkIndex = i,
							startRange = i * CHUNK_MAX_SIZE,
							isBeginDownload = false,
							downloadSize = lastChunkSize,
							tempFilePath = Path.Combine(Path.GetTempPath(), String.Format("{0}_{1}.tmp", fileName, i))
						});
						continue;
					}
				}
				downloadChunks.Add(new downloadChunkInfo()
				{
					chunkIndex = i,
					startRange = i * CHUNK_MAX_SIZE ==0? 1 : i * CHUNK_MAX_SIZE,
					isBeginDownload = false,
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

