using System;
using System.Linq;

namespace DotnetSpider.Sample.Utils
{
	public static class ParseUtils
	{
		public static int ParseIdFromUrl(string detailUrl)
		{
			if (string.IsNullOrEmpty(detailUrl))
			{
				return 0;
			}

			var parts = detailUrl.Split("-", StringSplitOptions.RemoveEmptyEntries);
			var lastPart = parts.Any() ? parts.Last() : "";
			return string.IsNullOrEmpty(lastPart) ? 0 : Convert.ToInt32(lastPart.Trim('/'));
		}

		public static decimal ParsePrice(string price)
		{
			if (string.IsNullOrEmpty(price))
				return 0;

			var s = price.Replace("&nbsp;", "");

			if (decimal.TryParse(s, out var p))
				return p;

			return 0;
		}

		public static decimal ParsePercent(string s)
		{
			if (string.IsNullOrEmpty(s))
				return 0;

			var p = s.Replace("-", "")
				.Replace("+", "")
				.Replace("%", "")
				.Replace("&nbsp;", "");

			if (decimal.TryParse(p, out var r))
				return r;

			return 0;
		}
	}
}
