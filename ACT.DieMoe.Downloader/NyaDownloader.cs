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
		public async Task startDownload()
		{
			/*long fileSize = getDownloadFileFullSize();
			chunkList = getDownloadChunks(fileSize);
			this.fileSize = fileSize;
			var taskList = new List<Task>();
			for(var i = 0; i < chunkList.Length; ++i)
			{
				for(var j = taskList.Length; j < MAX_PARALLEL_TASKS; ++j) {
					taskList.Add(DownloadChunk(i * CHUNK_SIZE, chunk_size, fileStream));
				}
				Task<int> finishedTask = await Task.WhenAny(downloadTasks);
				taskList.Remove(finsihedTask);
			}
			 */

			List<Task> taskList = new List<Task>();
			long fileSize = getDownloadFileFullSize();
			chunkList = getDownloadChunks(fileSize);
			this.fileSize = fileSize;
			for (int i = 0; i < chunkList.Count;)
			{
				for (int j = taskList.Count; j < downloadThreadCount && i < chunkList.Count; j++, i++)
				{
					taskList.Add(
						downloadChunkToFile(
							downloadFileUrl,
							chunkList[i].startRange,
							chunkList[i].downloadSize,
							chunkList[i].tempFilePath
						)
					);
				}
				var finishedTask = await Task.WhenAny(taskList);
				taskList.Remove(finishedTask);
			}
			await Task.WhenAll(taskList);
			Complete();
		}
		public async Task downloadChunkToFile(string url, long startRange, long size, string fileName)
		{
			long chunkDownloadSize = 0;
			for (int retryTime = 0; retryTime < 5; retryTime++)
			{
				try
				{
					/*HttpWebRequest downloadRequest = (HttpWebRequest)WebRequest.Create(downloadFileUrl);
					downloadRequest.Timeout = HTTP_TIME_OUT;
					downloadRequest.KeepAlive = true;
					downloadRequest.AddRange(startRange,startRange+size);
					HttpWebResponse downloadResponse = (HttpWebResponse)downloadRequest.GetResponse();*/

					HttpRequestMessage getMessage = new HttpRequestMessage(HttpMethod.Get, url);
					getMessage.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(startRange, startRange + size);
					Console.WriteLine("Range[{0} - {1}]", startRange, startRange + size);
					getMessage.Headers.Add("keep-alive", "timeout=5, max=100");
					var resp = await client.SendAsync(getMessage);
					var stream = await resp.Content.ReadAsStreamAsync();
					byte[] buffer = new byte[8192];
					int getByteSize;
					using (var fs = new FileStream(fileName, FileMode.Create))
					{
						do
						{
							getByteSize = stream.Read(buffer, 0, buffer.Length);
							fs.Write(buffer, 0, getByteSize);
							lock (locker) downloadSize += getByteSize;
							chunkDownloadSize += getByteSize;
							fileDownloadProcess?.Invoke(downloadSize);
						} while (getByteSize > 0);
					}
					break;
				}
				catch (Exception ex)
				{
					lock (locker) downloadSize -= chunkDownloadSize;
					if (retryTime >= 5)
					{
						throw ex;
					}
				}
			}
		}
		public List<downloadChunkInfo> getDownloadChunks(long fileSize)
		{
			List<downloadChunkInfo> downloadChunks = new List<downloadChunkInfo>();
			double chunkNums = (double)fileSize / (double)CHUNK_MAX_SIZE;
			double numChunk = Math.Ceiling(chunkNums);
			long lastChunkSize = fileSize % CHUNK_MAX_SIZE;
			for (int i = 0; i < numChunk - 1; i++)
			{
				downloadChunks.Add(new downloadChunkInfo()
				{
					chunkIndex = i,
					startRange = i * CHUNK_MAX_SIZE + 1,
					isBeginDownload = false,
					downloadSize = CHUNK_MAX_SIZE - 1,
					tempFilePath = Path.Combine(Path.GetTempPath(), String.Format("{0}_{1}.tmp", fileName, i))
				});
			}
			if (numChunk > 1)
			{
				var copy = downloadChunks[0];
				copy.startRange = 0;
				copy.downloadSize = CHUNK_MAX_SIZE;
				downloadChunks[0] = copy;
				downloadChunks.Add(new downloadChunkInfo()
				{
					chunkIndex = downloadChunks.Count,
					startRange = downloadChunks.Count * CHUNK_MAX_SIZE + 1,
					isBeginDownload = false,
					downloadSize = lastChunkSize != 0 ? lastChunkSize : CHUNK_MAX_SIZE,
					tempFilePath = Path.Combine(Path.GetTempPath(), String.Format("{0}_{1}.tmp", fileName, downloadChunks.Count))
				});
			}
			else
			{
				downloadChunks.Add(new downloadChunkInfo()
				{
					chunkIndex = downloadChunks.Count,
					startRange = downloadChunks.Count * CHUNK_MAX_SIZE,
					isBeginDownload = false,
					downloadSize = lastChunkSize != 0 ? lastChunkSize : CHUNK_MAX_SIZE,
					tempFilePath = Path.Combine(Path.GetTempPath(), String.Format("{0}_{1}.tmp", fileName, downloadChunks.Count))
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

