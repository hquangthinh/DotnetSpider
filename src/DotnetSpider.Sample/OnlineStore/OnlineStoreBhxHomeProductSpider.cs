using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Threading.Tasks;
using DotnetSpider.DataFlow;
using DotnetSpider.DataFlow.Parser;
using DotnetSpider.DataFlow.Parser.Attribute;
using DotnetSpider.DataFlow.Parser.Formatter;
using DotnetSpider.DataFlow.Storage;
using DotnetSpider.DataFlow.Storage.Model;
using DotnetSpider.Downloader;
using DotnetSpider.Scheduler;
using DotnetSpider.Selector;

namespace DotnetSpider.Sample.samples
{
	public class OnlineStoreBhxHomeProductSpider : Spider
	{
		public OnlineStoreBhxHomeProductSpider(SpiderParameters parameters) : base(parameters)
		{
		}

		protected override async Task Initialize()
		{
			NewGuidId();
			Scheduler = new QueueDistinctBfsScheduler();
			Speed = 1;
			Depth = 3;
			AddDataFlow(new DataParser<ProductEntry>())
				.AddDataFlow(GetDefaultStorage());
			await AddRequests(
				new Request("https://www.bachhoaxanh.com/", new Dictionary<string, string> {{"Home", "Home_Products"}})
				{
					UseProxy = false
				}
			);
		}
	}

	[Schema("bachhoaxanh", "products")]
	[EntitySelector(Expression = ".//li[@class='hideExpired product hasNotUnit']", Type = SelectorType.XPath)]
	[GlobalValueSelector(Expression = ".//ul[@class='cate cateproduct']", Name = "Category", Type = SelectorType.XPath)]
	//[FollowSelector(XPaths = new[] {"//div[@class='pager']"})]
	public class ProductEntry : EntityBase<ProductEntry>
	{
		protected override void Configure()
		{
			HasIndex(x => x.Name);
		}

		public int Id { get; set; }

		[Required]
		[Column(TypeName = "nvarchar(1000)")]
		[StringLength(1000)]
		[ValueSelector(Expression = ".//div[@class='product-name']")]
		public string Name { get; set; }

		[StringLength(225)]
		[ValueSelector(Expression = ".//a/@href")]
		public string ProductDetailUrl { get; set; }

		[StringLength(225)]
		[ValueSelector(Expression = ".//img/@src")]
		public string ProductImageUrl { get; set; }

		[StringLength(50)]
		[Column(TypeName = "nvarchar(50)")]
		[ValueSelector(Expression = ".//div[@class='price']/strong")]
		public string Price { get; set; }

		[StringLength(50)]
		[Column(TypeName = "nvarchar(50)")]
		[ValueSelector(Expression = ".//div[@class='price']/span")]
		public string OldPrice { get; set; }

		[StringLength(50)]
		[Column(TypeName = "nvarchar(50)")]
		[ValueSelector(Expression = ".//div[@class='price']/label")]
		public string Discount { get; set; }

		[StringLength(40)]
		[ValueSelector(Expression = "GUID", Type = SelectorType.Enviroment)]
		public string Guid { get; set; }

		[ValueSelector(Expression = "DATETIME", Type = SelectorType.Enviroment)]
		public DateTime CreationTime { get; set; }
	}
}
