using System;
using System.Net;
using System.Net.Http;
using DotnetSpider.Sample.samples;
using Serilog;
using Serilog.Events;

namespace DotnetSpider.Sample
{
	class Program
	{
		static void Main(string[] args)
		{
			// NvshensSpider.Run();
			// Startup.Execute<OnlineStoreBhxHomeProductSpider>(args);
			// Startup.Execute<OnlineStoreBhxCategorySpider>(args);
			// Startup.Execute<OnlineStoreVinmartCategorySpider>(args);
			// Startup.Execute<OnlineStoreVinmartPopularProductSpider>(args);
			Startup.Execute<OnlineStoreVinmartProductDetailSpider>(args);
			Console.Read();
		}
	}
}
