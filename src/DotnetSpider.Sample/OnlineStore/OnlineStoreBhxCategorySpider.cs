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
	public class OnlineStoreBhxCategorySpider : Spider
	{
		public OnlineStoreBhxCategorySpider(SpiderParameters parameters) : base(parameters)
		{
		}

		protected override async Task Initialize()
		{
			NewGuidId();
			Scheduler = new QueueDistinctBfsScheduler();
			Speed = 1;
			Depth = 3;
			AddDataFlow(new DataParser<CategoryEntry>())
				.AddDataFlow(GetDefaultStorage());
			await AddRequests(
				new Request("https://www.bachhoaxanh.com/", new Dictionary<string, string> {{"Home", "Home_Categories"}})
				{
					UseProxy = false
				}
			);
		}
	}

	[Schema("bachhoaxanh", "categories")]
	[EntitySelector(Expression = ".//li[@data-id]", Type = SelectorType.XPath)]
	[GlobalValueSelector(Expression = ".//ul[@class='colmenu-ul']", Name = "Category", Type = SelectorType.XPath)]
	public class CategoryEntry : EntityBase<CategoryEntry>
	{
		protected override void Configure()
		{
			HasIndex(x => x.Name);
		}

		public int Id { get; set; }

		[Required]
		[Column(TypeName = "nvarchar(255)")]
		[StringLength(255)]
		[ValueSelector(Expression = ".//div[@class='nav-parent']")]
		public string Name { get; set; }



		[StringLength(40)]
		[ValueSelector(Expression = "GUID", Type = SelectorType.Enviroment)]
		public string Guid { get; set; }

		[ValueSelector(Expression = "DATETIME", Type = SelectorType.Enviroment)]
		public DateTime CreationTime { get; set; }
	}
}
