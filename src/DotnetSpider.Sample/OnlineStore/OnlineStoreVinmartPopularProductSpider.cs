using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Default;
using DotnetSpider.DataFlow;
using DotnetSpider.DataFlow.Parser;
using DotnetSpider.DataFlow.Parser.Attribute;
using DotnetSpider.DataFlow.Parser.Formatter;
using DotnetSpider.DataFlow.Storage;
using DotnetSpider.DataFlow.Storage.Model;
using DotnetSpider.Downloader;
using DotnetSpider.Sample.GrandNode;
using DotnetSpider.Sample.Utils;
using DotnetSpider.Scheduler;
using DotnetSpider.Selector;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DotnetSpider.Sample.samples
{
	public class OnlineStoreVinmartPopularProductSpider : Spider
	{
		public OnlineStoreVinmartPopularProductSpider(SpiderParameters spiderParameters) : base(spiderParameters)
		{
		}

		protected override async Task Initialize()
		{
			NewGuidId();
			// Scheduler = new QueueDistinctDfsScheduler();
			Scheduler = new QueueDistinctBfsScheduler();
			Speed = 1;
			Depth = 3;
			AddDataFlow(new VinmartPopularProductListDataParser())
				.AddDataFlow(new VinmartProductDetailDataParser())
				// .AddDataFlow(new ConsoleStorage())
				.AddDataFlow(GetDefaultStorage())
				.AddDataFlow(new GrandNodePopularProductStorage());
			await AddRequests(
				new Request("https://vinmart.com/products/collection/san-pham-ban-chay-38/", new Dictionary<string, string> {{"PopularProducts", "PopularProducts"}})
				{
					UseProxy = false
				}
			);
		}
	}

	public class OnlineStoreVinmartProductDetailSpider : Spider
	{
		public OnlineStoreVinmartProductDetailSpider(SpiderParameters spiderParameters) : base(spiderParameters)
		{
		}

		protected override async Task Initialize()
		{
			NewGuidId();
			Scheduler = new QueueDistinctBfsScheduler();
			Speed = 1;
			Depth = 3;
			AddDataFlow(new VinmartPopularProductListDataParser())
				.AddDataFlow(new VinmartProductDetailDataParser())
				.AddDataFlow(new GrandNodePopularProductStorage());
			await AddRequests(
				new Request("https://vinmart.com/products/collection/san-pham-ban-chay-38/", new Dictionary<string, string> {{"PopularProducts", "PopularProducts"}})
				{
					UseProxy = false
				}
			);
		}
	}

	[Schema("vinmart", "products")]
	public class VinmartProductEntry : EntityBase<VinmartProductEntry>
	{
		protected override void Configure()
		{
			HasIndex(x => x.Name);
		}

		public int Id { get; set; }

		public int ProductId { get; set; }

		[Required]
		[Column(TypeName = "nvarchar(255)")]
		[StringLength(255)]
		public string Name { get; set; }

		[Column(TypeName = "nvarchar(4000)")]
		[StringLength(4000)]
		public string ShortDescription { get; set; }

		[Column(TypeName = "ntext")]
		[StringLength(10000)]
		public string FullDescription { get; set; }

		public decimal OldPrice { get; set; }

		public decimal Price { get; set; }

		public decimal DiscountPercent { get; set; }

		[Column(TypeName = "nvarchar(255)")]
		[StringLength(255)]
		public string Manufacturer { get; set; }

		[Column(TypeName = "nvarchar(255)")]
		[StringLength(255)]
		public string DetailUrl { get; set; }

		[Column(TypeName = "nvarchar(8000)")]
		[StringLength(8000)]
		public string ImageUrls { get; set; }

		[Column(TypeName = "nvarchar(255)")]
		[StringLength(255)]
		public string Flag { get; set; }

		[StringLength(40)]
		[ValueSelector(Expression = "GUID", Type = SelectorType.Enviroment)]
		public string Guid { get; set; }

		[ValueSelector(Expression = "DATETIME", Type = SelectorType.Enviroment)]
		public DateTime CreationTime { get; set; }
	}

	class VinmartPopularProductListDataParser : DataParser<VinmartProductEntry>
	{
		public VinmartPopularProductListDataParser()
		{
			Required = DataParserHelper.CheckIfRequiredByRegex("vinmart\\.com/products/collection/\\w+");
			// FollowRequestQuerier =
			// 	BuildFollowRequestQuerier(DataParserHelper.QueryFollowRequestsByXPath(".//div[@class='pager']"));
		}

		protected override Task<DataFlowResult> Parse(DataFlowContext context)
		{
			Logger.LogDebug($"Process url {0}", context.Response.Request.Url);

			var productFormListSelector = "//form[@id='product-form']";
			var productListNodes = context.Selectable.XPath(productFormListSelector).Nodes();

			foreach (var node in productListNodes.Take(100))
			{
				var name = node.XPath(".//a[contains(@class, 'product-name')]").GetValue();
				var detailUrl = node.XPath(".//a[contains(@class, 'product-name')]/@href").GetValue();

				var request = CreateFromRequest(context.Response.Request, detailUrl);

				request.AddProperty("name", name);
				request.AddProperty("detailUrl", detailUrl);
				request.AddProperty("flag", "popular");

				context.AddExtraRequests(request);
			}

			return Task.FromResult(DataFlowResult.Success);
		}
	}

	class VinmartProductDetailDataParser : DataParser<VinmartProductEntry>
	{
		public VinmartProductDetailDataParser()
		{
			Required = DataParserHelper.CheckIfRequiredByRegex("vinmart\\.com/products/((\\w+)-(\\w+))*-(\\d+)/");
		}

		protected override Task<DataFlowResult> Parse(DataFlowContext context)
		{
			Logger.LogDebug($"Process url {0}", context.Response.Request.Url);

			var name = context.Selectable.XPath(".//h1[contains(@class, 'product__info__name')]").GetValue();

			var detailUrl = context.Response.Request.Url;

			var flag = "auto";

			if (context.Response.Request.Properties.Keys.Contains("flag"))
			{
				flag = context.Response.Request.Properties["flag"];
			}

			var shortDescription = context.Selectable.XPath(".//div[contains(@class, 'product__short__description')]")
				.GetValue(ValueOption.OuterHtml);

			var fullDescription = context.Selectable.XPath(".//div[contains(@class, 'product__info__description')]").GetValue(ValueOption.OuterHtml);

			var imageUrlList = context.Selectable.XPath(".//div[@id='sliderSyncingNav']//img[@class='img-fluid']/@src").GetValues();
			var imageUrls = "";
			if (imageUrlList.Any())
			{
				imageUrls = string.Join(";", imageUrlList);
			}

			var price = context.Selectable.XPath(".//div[contains(@class, 'product__info__price')]/span/@data-product-price").GetValue();
			var oldPrice = context.Selectable.XPath(".//p[contains(@class, 'product__info__price__undiscounted')]/del").GetValue();
			var discountPercent = context.Selectable.XPath(".//div[contains(@class, 'product__info__price')]/div[@class='product-sales-badge']").GetValue();
			var manufacturer = context.Selectable.XPath(".//div[contains(@class, 'product__info')]/div[1]/p/span").GetValue();

			var productEntity = new VinmartProductEntry
			{
				Guid = Guid.NewGuid().ToString(),
				CreationTime = DateTime.Now,
				ProductId = ParseUtils.ParseIdFromUrl(detailUrl),
				Name = name,
				ShortDescription = shortDescription,
				FullDescription = fullDescription,
				DetailUrl = detailUrl,
				ImageUrls = imageUrls,
				Flag = flag,
				Price = ParseUtils.ParsePrice(price),
				OldPrice = ParseUtils.ParsePrice(oldPrice),
				DiscountPercent = ParseUtils.ParsePercent(discountPercent),
				Manufacturer = manufacturer
			};

			var typeName = typeof(VinmartProductEntry).FullName;

			context.Add(typeName, productEntity.GetTableMetadata());

			var products = new ParseResult<VinmartProductEntry> {productEntity};

			context.AddParseData(typeName, products);

			return Task.FromResult(DataFlowResult.Success);
		}
	}

	class GrandNodePopularProductStorage : EntityStorageBase
	{
		private static readonly HttpClient HttpClient = new HttpClient();

		protected override async Task<DataFlowResult> Store(DataFlowContext context)
		{
			var items = context.GetParseData();
			if(items.Keys.Any() && items["DotnetSpider.Sample.samples.VinmartProductEntry"] is ParseResult<VinmartProductEntry> products)
			{
				var token = await GrandNodeOdataApiServices.GenerateToken();

				if(string.IsNullOrEmpty(token))
					return DataFlowResult.Failed;

				GrandNodeOdataApiServices.InitContainer(token, GrandNodeOdataApiServices.StoreUrl);

				await DeleteExistingAutoCreatedProductsAsync();

				foreach (var product in products)
				{
					try
					{
						var gnProduct = new ProductDto
						{
							Id = "",
							ProductType = ProductType.SimpleProduct,
							VisibleIndividually = true,
							Name = product.Name,
							SeName = product.Name.RemoveDiacritics().Replace(" ", "-"),
							ShortDescription = product.Name,
							FullDescription = $"{product.ShortDescription} {product.FullDescription}",
							ProductTemplateId = "5e3c32c8504e9c00e8424765",
							ShowOnHomePage = true,
							DisplayOrder = 0,
							Published = true,
							Flag = "auto",
							AllowCustomerReviews = true,
							ApprovedRatingSum = 0,
							NotApprovedRatingSum = 0,
							ApprovedTotalReviews = 0,
							NotApprovedTotalReviews = 0,
							IsGiftCard = false,
							GiftCardType = GiftCardType.Virtual,
							RequireOtherProducts = false,
							AutomaticallyAddRequiredProducts = false,
							IsDownload = false,
							UnlimitedDownloads = false,
							DownloadActivationType = DownloadActivationType.Manually,
							MaxNumberOfDownloads = 0,
							HasSampleDownload = false,
							HasUserAgreement = false,
							IsRecurring = false,
							RecurringCycleLength = 0,
							RecurringTotalCycles = 0,
							RecurringCyclePeriod = RecurringProductCyclePeriod.Days,
							IncBothDate = false,
							Interval = 0,
							IntervalUnitType = IntervalUnit.Day,
							IsShipEnabled = true,
							IsFreeShipping = false,
							ShipSeparately = false,
							AdditionalShippingCharge = 0,
							IsTaxExempt = false,
							IsTele = false,
							UseMultipleWarehouses = false,
							StockQuantity = 100,
							ManageInventoryMethod = ManageInventoryMethod.ManageStock,
							DisplayStockAvailability = true,
							DisplayStockQuantity = true,
							MinStockQuantity = 10,
							LowStock = false,
							LowStockActivity = LowStockActivity.DisableBuyButton,
							NotifyAdminForQuantityBelow = 10,
							BackorderMode = BackorderMode.NoBackorders,
							AllowBackInStockSubscriptions = false,
							Price = product.Price,
							OldPrice = product.OldPrice,
							CatalogPrice = product.Price,
							ProductCost = product.Price,
							CreatedOnUtc = DateTime.Now,
							UpdatedOnUtc = DateTime.Now,
							OrderMinimumQuantity = 1,
							OrderMaximumQuantity = 100
						};

						var newProduct = GrandNodeOdataApiServices.InsertProduct(gnProduct);

						var productToUpdate = await GrandNodeOdataApiServices.GetProductAsync(newProduct.Id);

						await GrandNodeOdataApiServices.UpdateStock(productToUpdate, "", 1000);

						if (!string.IsNullOrEmpty(product.ImageUrls))
						{
							var imageUrls = product.ImageUrls.Split(";");

							foreach (var url in imageUrls)
							{
								var imageContent = await HttpClient.GetByteArrayAsync(url);
								var newPicture = await GrandNodeOdataApiServices.InsertPicture(imageContent);
								if (newPicture != null && !string.IsNullOrEmpty(newPicture.Id))
								{
									productToUpdate.CreateProductPicture(newPicture.Id, newPicture.MimeType,
										newPicture.SeoFilename, product.Name, 0, product.Name)
									.GetValueAsync().Wait();
								}
							}
						}
					}
					catch (Exception e)
					{
						Logger.LogError(e.ToString());
					}
				}
			}
			return DataFlowResult.Success;
		}

		private async Task DeleteExistingAutoCreatedProductsAsync()
		{
			var products = await GrandNodeOdataApiServices.GetProductsAsync();
			foreach (var product in products)
			{
				GrandNodeOdataApiServices.DeleteProduct(product);
			}
		}
	}

}
