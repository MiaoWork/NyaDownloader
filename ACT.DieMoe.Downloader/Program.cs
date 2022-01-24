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
		static void Main(string[] args)
		{
			try
			{
				int proc = 0;
				NyaDownloader nya = new NyaDownloader(new HttpClient(), "https://registrationcenter-download.intel.com/akdlm/irc_nas/tec/18411/w_pythoni39_oneapi_p_2022.0.0.118_offline.exe", 10, 5242880, ".","test");
				nya.startDownload();
				//downloader.StartDownload(@"https://registrationcenter-download.intel.com/akdlm/irc_nas/tec/18411/w_pythoni39_oneapi_p_2022.0.0.118_offline.exe", 32, "test.exe", @".\");
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
			}
			Console.ReadLine();
		}
	}
}
