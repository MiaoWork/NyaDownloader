
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ACT.DieMoe.Downloader
{
	public class Download
	{
		// 线程池
		List<Thread> downloadThreadPool = new List<Thread>();
		// 临时文件区
		List<string> tempFileList = new List<string>();
		// 总下载量
		public long downloadSize = 0;
		private object locker = new object();
		private long _threadCompleteNum;
		public long fileSize;
		public FileDownloadProcess fileDownloadProcess { get; set; } = null;
		public FileDownloadFinish fileDownloadFinishCallBack { get; set; } = null;
		public FileChunksDownloadFinish fileChunksDownloadFinishCallBack { get; set; } = null;
		public delegate void FileDownloadProcess(float downloadSize);
		public delegate void FileDownloadFinish();
		public delegate void FileChunksDownloadFinish(int downloadIndex);
		/// <summary>
		/// 开始下载
		/// </summary>
		/// <param name="fileUrl">文件下载链接</param>
		/// <param name="threadNum">线程数</param>
		/// <param name="fileName">文件名称</param>
		/// <param name="savePath">保存路径</param>
		public void StartDownload(string fileUrl, int threadNum, string fileName, string savePath)
		{
			System.Net.ServicePointManager.DefaultConnectionLimit = 512;
			//初始化协议
			ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
			HttpWebRequest downloadRequest = (HttpWebRequest)WebRequest.Create(fileUrl);
			downloadRequest.Timeout = 15000;
			HttpWebResponse downloadResponse = (HttpWebResponse)downloadRequest.GetResponse();
			long fileSize = downloadResponse.ContentLength;

			// var MAX_CHUNK_SIZE = 5242880;
			// numChunk = Math.ceil(file_size / MAX_CHUNK_SIZE);
			// lastChunkSize = fileSize % MAX_CHUNK_SIZE;
			// [chunk1, 512], [chunk2, 512], [chunk3, 42]
			// downloadChunk(5, MAX_CHUNK_SIZE);
			long filePointer = 0;
			bool needDownloadLastChunk = true;
			long fileChunks = fileSize / threadNum;
			downloadRequest.Abort();
			downloadResponse.Close();
			this.fileSize = fileSize;
			//创建下载线程
			//Console.WriteLine($"DownloadInfo:线程数[{threadNum}] 文件块大小[{fileChunks}b] 是否有尾块[{needDownloadLastChunk}] 单独块大小[{fileSize % threadNum}b] 文件大小[{fileSize}b]");
			for (int i = 0; i < threadNum; i++)
			{
				downloadThreadPool.Add(new Thread(new ParameterizedThreadStart(downloadFileChunks)));
				DownloadChunksSetting setting = new DownloadChunksSetting();
				setting.fileName = fileName;
				setting.startRange = filePointer == 0 ? filePointer : filePointer + 1;
				setting.lastRange = i == threadNum - 1 ? filePointer + fileChunks + fileSize % threadNum : filePointer + fileChunks;
				setting.fileIndex = i;
				setting.downloadUrl = fileUrl;
				setting.threadNum = threadNum;
				setting.savePath = savePath;
				downloadThreadPool[i].Start(setting);
				filePointer += fileChunks;
			}
		}

		public void downloadFileChunks(object obj)
		{
			DownloadChunksSetting setting = (DownloadChunksSetting)obj;
			long threadFileSize = setting.lastRange - setting.startRange;
			long fileChunkAmount = (threadFileSize / 5242880) + (threadFileSize % 5242880 != 0 ? 1 : 0);
			//Console.WriteLine($"Download 线程[{setting.fileIndex}]: 线程负责大小[{threadFileSize}]b 文件块数量[{fileChunkAmount}]");
			Stream httpFileStream = null, localFileStream = null;
			long downloadByteOffset = setting.startRange;
			List<string> threadFileTempFileList = new List<string>();
			//线程临时文件
			string ThreadtmpFileChunks = Path.Combine(Path.GetTempPath(), setting.fileName + "_" + setting.fileIndex + ".tmp");
			if (File.Exists(Path.Combine(Path.GetTempPath(), setting.fileName + "_" + setting.fileIndex + ".tmp")))
			{
				File.Delete(Path.Combine(Path.GetTempPath(), setting.fileName + "_" + setting.fileIndex + ".tmp"));
			}
			if (!tempFileList.Contains(ThreadtmpFileChunks))
			{
				tempFileList.Add(ThreadtmpFileChunks);
			}
			for (int i = 0; i < fileChunkAmount; i++)
			{
				int retryTime = 0;
			Retry:
				long downloadChunkSize = 0;
				try
				{
					string tmpFileChunks = Path.Combine(Path.GetTempPath(), setting.fileName + "_" + setting.fileIndex + "_" + i + ".tmp");
					if (File.Exists(Path.Combine(Path.GetTempPath(), setting.fileName + "_" + setting.fileIndex + "_" + i + ".tmp")))
					{
						File.Delete(Path.Combine(Path.GetTempPath(), setting.fileName + "_" + setting.fileIndex + "_" + i + ".tmp"));
					}
					if (!threadFileTempFileList.Contains(tmpFileChunks))
					{
						threadFileTempFileList.Add(tmpFileChunks);
					}
					HttpWebRequest downloadThreadRequest = (HttpWebRequest)WebRequest.Create(setting.downloadUrl);
					downloadThreadRequest.Timeout = 15000;
					downloadThreadRequest.KeepAlive = true;
					//分节请求文件
					downloadThreadRequest.AddRange((downloadByteOffset + (i == 0 ? 0 : 1)), (downloadByteOffset + (i == fileChunkAmount - 1 ? threadFileSize % 5242880 : 5242880)));
					HttpWebResponse downloadThreadResponse = (HttpWebResponse)downloadThreadRequest.GetResponse();
					httpFileStream = downloadThreadResponse.GetResponseStream();
					localFileStream = new FileStream(tmpFileChunks, FileMode.Create);
					byte[] byteList = new byte[8192];
					 
					// downloadedData = this.DownloadFromUrl(url, range);
					// localFileStream.Write(downloadedData);
					int getByteSize = httpFileStream.Read(byteList, 0, byteList.Length);
					while (getByteSize > 0)
					{
						lock (locker) downloadSize += getByteSize;
						localFileStream.Write(byteList, 0, getByteSize);
						getByteSize = httpFileStream.Read(byteList, 0, (int)byteList.Length);
						downloadChunkSize += getByteSize;
						fileDownloadProcess?.Invoke(((float)downloadSize / (float)fileSize) * 100);
					}
				}
				catch (Exception ex)
				{
					retryTime++;
					if (retryTime == 6)
					{
						throw new Exception($"[Thread][{setting.fileIndex}]" + ex.Message.ToString());
					}
					downloadSize -= downloadChunkSize;
					goto Retry;
				}
				finally
				{
					if (httpFileStream != null) httpFileStream.Dispose();
					if (localFileStream != null) localFileStream.Dispose();
				}
				downloadByteOffset += 5242880;
			}
			ThreadComplete(threadFileTempFileList, ThreadtmpFileChunks);
			lock (locker) _threadCompleteNum++;
			if (_threadCompleteNum == setting.threadNum)
			{
				// 合并
				Complete(setting);
			}
		}
		public void Complete(DownloadChunksSetting setting)
		{
			Stream mergeFile = new FileStream(Path.Combine(setting.savePath, setting.fileName), FileMode.Create);
			BinaryWriter AddWriter = new BinaryWriter(mergeFile);
			tempFileList.Sort((a, b) =>
			{

				if (Convert.ToInt32(a.Split('_').Last().Split('.').First()) > Convert.ToInt32(b.Split('_').Last().Split('.').First()))
					return 1;
				else
					return -1;
			});
			foreach (string file in tempFileList)
			{
				using (FileStream fs = new FileStream(file, FileMode.Open))
				{
					BinaryReader TempReader = new BinaryReader(fs);
					AddWriter.Write(TempReader.ReadBytes((int)fs.Length));
					TempReader.Close();
				}
				File.Delete(file);
			}
			AddWriter.Close();
			fileDownloadFinishCallBack?.Invoke();
		}

		public void ThreadComplete(List<string> TtmpFileList, string margeFilePath)
		{
			Console.WriteLine("线程文件合并" + margeFilePath);
			Stream mergeFile = new FileStream(margeFilePath, FileMode.Create);
			BinaryWriter AddWriter = new BinaryWriter(mergeFile);
			TtmpFileList.Sort((a, b) =>
			{

				if (Convert.ToInt32(a.Split('_').Last().Split('.').First()) > Convert.ToInt32(b.Split('_').Last().Split('.').First()))
					return 1;
				else
					return -1;
			});
			foreach (string file in TtmpFileList)
			{
				using (FileStream fs = new FileStream(file, FileMode.Open))
				{
					BinaryReader TempReader = new BinaryReader(fs);
					AddWriter.Write(TempReader.ReadBytes((int)fs.Length));
					TempReader.Close();
				}
				File.Delete(file);
			}
			AddWriter.Close();
		}
		public void abort()
		{
			foreach (var item in downloadThreadPool)
			{
				item.Abort();
			}
			try
			{
				foreach (var item in tempFileList)
				{
					File.Delete(item);
				}
			}
			catch
			{
			}
		}
		public struct DownloadChunksSetting
		{
			public int fileIndex;
			public long startRange;
			public long lastRange;
			public string fileName;
			public string downloadUrl;
			public int threadNum;
			public string savePath;
		}
	}
}
