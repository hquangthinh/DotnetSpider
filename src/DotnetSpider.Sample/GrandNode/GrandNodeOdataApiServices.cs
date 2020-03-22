using Default;
using Microsoft.OData.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DotnetSpider.Sample.GrandNode
{
	public class GrandNodeOdataApiServices
	{
		public static string StoreUrl = "http://localhost:16593";
		private static string UserName = "admin@yourstore.com";
		private static string Password = "123Qwe";
		private static string Token;
		private static Container container;

		public static Container InitContainer(string token, string storeUrl)
		{
			Token = token;
			StoreUrl = storeUrl;
			container = new Default.Container(new Uri(storeUrl + "/odata/"));
			container.BuildingRequest += onBuildingRequest;
			container.MergeOption = MergeOption.AppendOnly;
			return container;
		}

		public static void onBuildingRequest(object sender, Microsoft.OData.Client.BuildingRequestEventArgs e)
		{
			e.Headers.Add("Authorization", "Bearer " + Token);
		}

		public static async Task<string> GenerateToken()
		{
			var client = new HttpClient();
			client.BaseAddress = new Uri(StoreUrl);
			var credentials = new GenerateTokenModel();
			credentials.Email = UserName;
			credentials.Password = Base64Encode(Password);

			var serializedJson = JsonConvert.SerializeObject(credentials);
			var httpContent = new StringContent(serializedJson.ToString(), Encoding.UTF8, "application/json");
			var result = await client.PostAsync("api/token/create", httpContent);
			return result.Content.ReadAsStringAsync().Result;
		}

		public static async Task<PictureDto> InsertPicture(byte[] binary)
		{
			var picture = new PictureDto
			{
				PictureBinary = binary,
				MimeType = "image/jpeg",
				SeoFilename = "",
				IsNew = true,
				Id = ""
			};
			container.AddToPicture(picture);
			await container.SaveChangesAsync();
			return picture;
		}

		private static string Base64Encode(string plainText)
		{
			var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
			return System.Convert.ToBase64String(plainTextBytes);
		}

		public static async Task<IEnumerable<CategoryDto>> GetCategories()
		{
			var list = await container.Category.ExecuteAsync();
			return list;
		}

		public static void DeleteCategory(CategoryDto model)
		{
			container.DeleteObject(model);
			container.SaveChangesAsync().Wait();
		}

		public static CategoryDto InsertCategory(CategoryDto model)
		{
			container.AddToCategory(model);
			container.SaveChangesAsync().Wait();
			return model;
		}

		#region Product

		public static Task<IEnumerable<ProductDto>> GetProductsAsync()
		{
			var list = container.Product
				.AddQueryOption("$filter", "Flag eq 'auto'")
				.ExecuteAsync();
			return list;
		}

		public static void DeleteProduct(ProductDto model)
		{
			container.DeleteObject(model);
			container.SaveChangesAsync().Wait();
		}

		public static ProductDto InsertProduct(ProductDto model)
		{
			container.AddToProduct(model);
			container.SaveChangesAsync().Wait();
			return model;
		}

		public static async Task<ProductDto> GetProductAsync(string id)
		{
			var products = await container.Product
				.AddQueryOption("$filter", $"Id eq '{id}'")
				.ExecuteAsync();
			return products.FirstOrDefault();
		}

		public static async Task UpdateStock(ProductDto product, string warehouseId, int stock)
		{
			await product.UpdateStock(warehouseId, stock).GetValueAsync();
		}

		#endregion
	}

	public class GenerateTokenModel
	{
		public string Email { get; set; }
		public string Password { get; set; }
	}
}
