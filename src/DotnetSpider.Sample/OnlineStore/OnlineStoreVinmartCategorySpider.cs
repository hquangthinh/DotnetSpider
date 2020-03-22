using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using DotnetSpider.DataFlow;
using DotnetSpider.DataFlow.Parser;
using DotnetSpider.DataFlow.Parser.Attribute;
using DotnetSpider.DataFlow.Parser.Formatter;
using DotnetSpider.DataFlow.Storage;
using DotnetSpider.DataFlow.Storage.Model;
using DotnetSpider.Downloader;
using DotnetSpider.Sample.GrandNode;
using DotnetSpider.Scheduler;
using DotnetSpider.Selector;
using Newtonsoft.Json;

namespace DotnetSpider.Sample.samples
{
	public class OnlineStoreVinmartCategorySpider : Spider
	{
		public OnlineStoreVinmartCategorySpider(SpiderParameters parameters) : base(parameters)
		{
		}

		protected override async Task Initialize()
		{
			NewGuidId();
			Scheduler = new QueueDistinctBfsScheduler();
			Speed = 1;
			Depth = 3;
			AddDataFlow(new VinmartCategorySpiderDataParser())
				.AddDataFlow(GetDefaultStorage())
				.AddDataFlow(new GrandNodeCategoryStorage());
			await AddRequests(
				new Request("https://vinmart.com/", new Dictionary<string, string> {{"Home", "Home_Categories"}})
				{
					UseProxy = false
				}
			);
		}
	}

	class VinmartCategorySpiderDataParser : DataParser<VinmartCategoryEntry>
	{
		protected override Task<DataFlowResult> Parse(DataFlowContext context)
		{
			var level1MenuNodes = context.Selectable.XPath("//ul[@id='categoriesSubMenu']/div/li")
				.Nodes();

			var categories = new ParseResult<VinmartCategoryEntry>();
			var typeName = typeof(VinmartCategoryEntry).FullName;
			var entity = new VinmartCategoryEntry();
			context.Add(typeName, entity.GetTableMetadata());

			foreach (var node in level1MenuNodes)
			{
				var name = node.XPath(".//a[@id]").GetValue();
				var detailUrl = node.XPath(".//a[@id]/@href").GetValue();
				if (string.IsNullOrEmpty(name))
				{
					continue;
				}

				var categoryId = ParseCategoryId(detailUrl);

				var categoryImageUrl = GetCategoryImageUrl(context.Selectable, context.Response.Request.Url, detailUrl);

				categories.Add(new VinmartCategoryEntry
				{
					Name = name.Trim(),
					DetailUrl = detailUrl,
					CategoryId = categoryId,
					ParentCategoryId = null,
					Level = 1,
					ImageUrl = categoryImageUrl,
					Guid = Guid.NewGuid().ToString(),
					CreationTime = DateTime.Now
				});

				// Get categories level 2
				var level2CategoryNodes = node.XPath(".//ul/div/li").Nodes();

				foreach (var level2Node in level2CategoryNodes)
				{
					var level2Name = level2Node.XPath(".//a[@id]").GetValue();
					var level2DetailUrl = level2Node.XPath(".//a[@id]/@href").GetValue();
					if (string.IsNullOrEmpty(level2Name))
					{
						continue;
					}

					var level2CategoryId = ParseCategoryId(level2DetailUrl);

					categories.Add(new VinmartCategoryEntry
					{
						Name = level2Name.Trim(),
						DetailUrl = level2DetailUrl,
						CategoryId = level2CategoryId,
						ParentCategoryId = categoryId,
						Level = 2,
						Guid = Guid.NewGuid().ToString(),
						CreationTime = DateTime.Now
					});
				}
			}

			if (categories.Any())
			{
				context.AddParseData(typeName, categories);
			}

			return Task.FromResult(DataFlowResult.Success);
		}

		private static int ParseCategoryId(string detailUrl)
		{
			if (string.IsNullOrEmpty(detailUrl))
			{
				return 0;
			}

			var parts = detailUrl.Split("-", StringSplitOptions.RemoveEmptyEntries);
			var lastPart = parts.Any() ? parts.Last() : "";
			return string.IsNullOrEmpty(lastPart) ? 0 : Convert.ToInt32(lastPart.Trim('/'));
		}

		private static string GetCategoryImageUrl(ISelectable selectable, string baseUrl, string categoryUrl)
		{
			var url = categoryUrl.Replace(baseUrl, "");
			var xpath = $".//a[contains(@href, '{url}')]/div/img/@src";
			return selectable.XPath(xpath).GetValue();
		}
	}

	[Schema("vinmart", "categories")]
	[EntitySelector(Expression = ".//li[@class='hs-has-sub-menu']", Type = SelectorType.XPath)]
	[GlobalValueSelector(Expression = ".//ul[@id='categoriesSubMenu']", Name = "Category", Type = SelectorType.XPath)]
	public class VinmartCategoryEntry : EntityBase<VinmartCategoryEntry>
	{
		protected override void Configure()
		{
			HasIndex(x => x.Name);
		}

		public int Id { get; set; }

		public int CategoryId { get; set; }

		public int? ParentCategoryId { get; set; }

		public int Level { get; set; }

		[Required]
		[Column(TypeName = "nvarchar(255)")]
		[StringLength(255)]
		[ValueSelector(Expression = ".//a[@id]")]
		public string Name { get; set; }

		[Column(TypeName = "nvarchar(255)")]
		[StringLength(255)]
		[ValueSelector(Expression = ".//a[@id]/@href")]
		public string DetailUrl { get; set; }

		[Column(TypeName = "nvarchar(255)")]
		[StringLength(255)]
		public string ImageUrl { get; set; }

		[StringLength(40)]
		[ValueSelector(Expression = "GUID", Type = SelectorType.Enviroment)]
		public string Guid { get; set; }

		[ValueSelector(Expression = "DATETIME", Type = SelectorType.Enviroment)]
		public DateTime CreationTime { get; set; }
	}

	/// <summary>
	/// A data flow that store parsed data into grandnode categories
	/// </summary>
	class GrandNodeCategoryStorage : EntityStorageBase
	{
		private static readonly HttpClient HttpClient = new HttpClient();

		protected override async Task<DataFlowResult> Store(DataFlowContext context)
		{
			var items = context.GetParseData();
			if(items.Keys.Any() && items["DotnetSpider.Sample.samples.VinmartCategoryEntry"] is ParseResult<VinmartCategoryEntry> categories)
			{
				var token = await GrandNodeOdataApiServices.GenerateToken();

				if(string.IsNullOrEmpty(token))
					return DataFlowResult.Failed;

				GrandNodeOdataApiServices.InitContainer(token, GrandNodeOdataApiServices.StoreUrl);

				await DeleteExistingAutoCreatedCategoriesAsync();

				foreach (var category in categories.Where(x => x.Level == 1))
				{
					try
					{
						var gnCategory = new Default.CategoryDto
						{
							Id = "",
							Name = category.Name,
							Description = category.Name,
							CategoryTemplateId = "5e3c32c8504e9c00e8424767",
							PageSizeOptions = "6, 3, 9",
							PageSize = 10,
							AllowCustomersToSelectPageSize = true,
							ShowOnHomePage = true,
							FeaturedProductsOnHomaPage = false,
							IncludeInTopMenu = true,
							DisplayOrder = 0,
							HideOnCatalog = false,
							ShowOnSearchBox = true,
							SearchBoxDisplayOrder = 0,
							Published = true,
							Flag = "auto"
						};

						if (!string.IsNullOrEmpty(category.ImageUrl))
						{
							var imageContent = await DownloadImageAsync(category.ImageUrl);
							var newPicture = await GrandNodeOdataApiServices.InsertPicture(imageContent);
							if (newPicture != null && !string.IsNullOrEmpty(newPicture.Id))
							{
								gnCategory.PictureId = newPicture.Id;
							}
						}

						GrandNodeOdataApiServices.InsertCategory(gnCategory);
					}
					catch (Exception e)
					{
						Console.WriteLine(e);
					}
				}
			}
			return DataFlowResult.Success;
		}

		private async Task DeleteExistingAutoCreatedCategoriesAsync()
		{
			var categories = await GrandNodeOdataApiServices.GetCategories();
			foreach (var category in categories.Where(x => x.DisplayOrder == 0 || x.Flag == "auto"))
			{
				GrandNodeOdataApiServices.DeleteCategory(category);
			}
		}

		private async Task<byte[]> DownloadImageAsync(string imageUrl)
		{
			var content = await HttpClient.GetByteArrayAsync(imageUrl);
			return content;
		}
	}
}
