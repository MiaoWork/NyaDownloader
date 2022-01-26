using ACT.DieMoe.Downloader;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace DownloaderTestProgram
{
	[TestClass]
	public class MainTest
	{
		const long START_TEST_SIZE = 524288;
		const string DOWNLOAD_FILE_DIRECTORY = @"D:\IIS\FileDownloadTest\downloadFile";
		const string TEST_FILE_SOURCE_FILE = @"D:\IIS\FileDownloadTest\testSourceFile";
		[TestMethod]
		public void downloadMockTest()
		{
			cleanTestFile();
			Random random = new Random();
			summonFile(TEST_FILE_SOURCE_FILE + $"/test.txt", random.Next(1,999999));
			NyaDownloader downloader = new NyaDownloader(
				new MockHttp(TEST_FILE_SOURCE_FILE + $"/test.txt"),
				$"http://192.168.31.24/testSourceFile/test.txt",
				16,
				random.Next(1, 99999),
				DOWNLOAD_FILE_DIRECTORY,
				DOWNLOAD_FILE_DIRECTORY + $"/test.txt"
				);
			downloader.fileDownloadFinishCallBack = () => {
					// 校验函数
				Assert.AreEqual(File.ReadAllText(TEST_FILE_SOURCE_FILE + $"/test.txt"), File.ReadAllText(DOWNLOAD_FILE_DIRECTORY + $"/test.txt"));
			};
			downloader.startDownload().Wait();
		}
		[TestMethod]
		public void downloadBaseTest()
		{
			cleanTestFile();
			Random random = new Random();
			summonFile(TEST_FILE_SOURCE_FILE + $"/test.txt", random.Next(1, 99999));
			NyaDownloader downloader = new NyaDownloader(new System.Net.Http.HttpClient(),$"http://192.168.31.24/testSourceFile/test.txt", 16, random.Next(1, 99999), DOWNLOAD_FILE_DIRECTORY, DOWNLOAD_FILE_DIRECTORY + $"/test.txt");
			downloader.fileDownloadFinishCallBack = () => {
				// 校验函数
				Assert.AreEqual(File.ReadAllText(TEST_FILE_SOURCE_FILE + $"/test.txt"), File.ReadAllText(DOWNLOAD_FILE_DIRECTORY + $"/test.txt"));
			};
			downloader.startDownload().Wait();
		}
		[TestMethod]
		public void downloadFileWhenHttpClientBadRequest()
		{
			cleanTestFile();
			try
			{
				Random random = new Random();
				summonFile(TEST_FILE_SOURCE_FILE + $"/test.txt", random.Next(1, 99999));
				NyaDownloader downloader = new NyaDownloader(new MockHttp(TEST_FILE_SOURCE_FILE + $"/test.txt", System.Net.HttpStatusCode.BadRequest), $"http://192.168.31.24/testSourceFile/test.txt", 16, random.Next(1, 99999), DOWNLOAD_FILE_DIRECTORY, DOWNLOAD_FILE_DIRECTORY + $"/test.txt");
				downloader.startDownload().Wait();
				Assert.Fail("Can't Catch Error");
			}
			catch (Exception e)
			{
				return;
			}
		}
		[TestMethod]
		public void downloadFileWhenFileNotFound()
		{
			cleanTestFile();
			try
			{
				Random random = new Random();
				summonFile(TEST_FILE_SOURCE_FILE + $"/test.txt", random.Next(1, 99999));
				NyaDownloader downloader = new NyaDownloader(new MockHttp(TEST_FILE_SOURCE_FILE + $"/test.txt"), $"http://192.168.31.24/testSourceFile/UNKNOW.txt", 16, random.Next(1, 99999), DOWNLOAD_FILE_DIRECTORY, DOWNLOAD_FILE_DIRECTORY + $"/test.txt");
				downloader.startDownload().Wait();
				Assert.Fail("Can't Catch Error");
			}
			catch (Exception e)
			{
				return;
			}
		}
		[TestMethod]
		public void downloadFileWhenConnectBlocked()
		{
			cleanTestFile();
			try
			{
				Random random = new Random();
				summonFile(TEST_FILE_SOURCE_FILE + $"/test.txt",98126319);
				NyaDownloader downloader = new NyaDownloader(new MockHttp(TEST_FILE_SOURCE_FILE + $"/test.txt",System.Net.HttpStatusCode.OK,true), $"http://192.168.31.24/testSourceFile/test.txt", 16, random.Next(1, 99999), DOWNLOAD_FILE_DIRECTORY, DOWNLOAD_FILE_DIRECTORY + $"/test.txt");
				downloader.fileDownloadFinishCallBack = () => {
					// 校验函数
					Assert.AreEqual(File.ReadAllText(TEST_FILE_SOURCE_FILE + $"/test.txt"), File.ReadAllText(DOWNLOAD_FILE_DIRECTORY + $"/test.txt"));
				};
				downloader.startDownload().Wait();
				Assert.Fail("Can't Catch Error");
			}
			catch (Exception e)
			{
				return;
			}
		}
		public bool testFile(string sourceFilePath,string downloadFilePath)
		{
			string sourceFile = File.ReadAllText(sourceFilePath);
			string downloadFile = File.ReadAllText(downloadFilePath);
			return sourceFile.Equals(downloadFile);
		}
		public void cleanTestFile()
		{
			foreach (var item in Directory.GetFiles(DOWNLOAD_FILE_DIRECTORY))
			{
				File.Delete(item);
			}
			foreach (var item in Directory.GetFiles(TEST_FILE_SOURCE_FILE))
			{
				File.Delete(item);
			}
		}
		public void summonFile(string filePath,long fileSize)
		{
			Random random = new Random();
			using (FileStream fs = new FileStream(filePath, FileMode.OpenOrCreate))
			using (var sw = new StreamWriter(fs))
			{
				for (int i = 0; i < fileSize; i++)
				{
					sw.Write(random.Next(0, 9));
				}
				sw.Close();
				fs.Close();
			}
		}
	}
}
