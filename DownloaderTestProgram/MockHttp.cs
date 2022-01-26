using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DownloaderTestProgram
{
	public class MockHttp : HttpClient
	{
		public override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			
			return base.SendAsync(request, cancellationToken);
		}
	}
}
