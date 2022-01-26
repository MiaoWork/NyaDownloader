using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ACT.DieMoe.Downloader
{
	class Program
	{
		static  void Main(string[] args)
		{
			try
			{
				int proc = 0;
				NyaDownloader nya = new NyaDownloader(new HttpClient(), "https://ffxiv-res.diemoe.net/ACT.DieMoe/Assets/ACT.DieMoe/Updates/5.58.1.03/patch.exe", 10, 1048576, ".", "patch.exe");
				nya.startDownload().Wait();
				//downloader.StartDownload(@"https://registrationcenter-download.intel.com/akdlm/irc_nas/tec/18411/w_pythoni39_oneapi_p_2022.0.0.118_offline.exe", 32, "test.exe", @".\");
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
				throw e;
			}
			Console.WriteLine("Finished, press any key to continue");
			Console.ReadLine();
		}
	}
}
