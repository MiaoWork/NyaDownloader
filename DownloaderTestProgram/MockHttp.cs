using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DownloaderTestProgram
{
	public class MockHttp : HttpClient
	{
		string filePath;
		System.Net.HttpStatusCode testReturnCode;
		bool randomBlockConnect;
		public MockHttp(string testFilePath, System.Net.HttpStatusCode returnCode = System.Net.HttpStatusCode.OK,bool needRandomBlockConnect = false)
		{
			//文件位置
			filePath = testFilePath;
			//返回值
			testReturnCode = returnCode;
			//是否随机阻断连接
			randomBlockConnect = needRandomBlockConnect;
		}
		public override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			if (randomBlockConnect)
			{
				int time = new Random().Next(0,100);
				if (time > 30)
				{
					HttpResponseMessage returnRequestMessage = new HttpResponseMessage();
					returnRequestMessage.StatusCode = System.Net.HttpStatusCode.BadRequest;
					returnRequestMessage.Content = new StringContent(string.Format("{{ \"url\": \"{0}\" }}", request.RequestUri));
					return Task.FromResult(returnRequestMessage);
				}
				else
				{
					return base.SendAsync(request, cancellationToken);
				}
			}
			if (testReturnCode == System.Net.HttpStatusCode.OK)
			{
				return base.SendAsync(request, cancellationToken);
			}
			else
			{
				HttpResponseMessage returnRequestMessage = new HttpResponseMessage();
				using (var fileStream = new MemoryStream(File.ReadAllBytes(filePath)))
				{
					returnRequestMessage.StatusCode = testReturnCode;
					returnRequestMessage.Content = new StreamContent(fileStream);
				}
				return Task.FromResult(returnRequestMessage);
			}
		}
	}
}
