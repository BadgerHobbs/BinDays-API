namespace BinDays.Api.Collectors.Utilities
{
	using BinDays.Api.Collectors.Models;
	using System;
	using System.Collections.ObjectModel;
	using System.Text.RegularExpressions;
	using System.Web;

	/// <summary>
	/// Provides utility methods for processing data.
	/// </summary>
	public static partial class ProcessingUtilities
	{
		/// <summary>
		/// Regex to parse set-cookies.
		/// </summary>
		[GeneratedRegex(@"(?:^|,)\s*([^=;\s]+=[^;]+)")]
		private static partial Regex CookieRegex();

		/// <summary>
		/// Regex to match whitespace.
		/// </summary>
		[GeneratedRegex(@"\s+")]
		private static partial Regex WhitespaceRegex();

		/// <summary>
		/// Converts a dictionary of string key-value pairs into a URL-encoded form data string.
		/// </summary>
		/// <param name="dictionary">The dictionary to convert.</param>
		/// <returns>A URL-encoded form data string.</returns>
		public static string ConvertDictionaryToFormData(Dictionary<string, string> dictionary)
		{
			if (dictionary == null || dictionary.Count == 0)
			{
				return string.Empty;
			}

			var formData = string.Join("&", dictionary.Select(kvp =>
				$"{HttpUtility.UrlEncode(kvp.Key)}={HttpUtility.UrlEncode(kvp.Value)}"));

			return formData;
		}

		/// <summary>
		/// Processes a collection of BinDay objects by filtering for future dates and merging by date.
		/// </summary>
		/// <param name="binDays">The collection of BinDay objects to process.</param>
		/// <returns>A read-only collection of processed BinDay objects.</returns>
		public static ReadOnlyCollection<BinDay> ProcessBinDays(IEnumerable<BinDay> binDays)
		{
			// Filter out bin days in the past
			var futureBinDays = GetFutureBinDays(binDays);

			// Merge the bin days
			var mergedBinDays = MergeBinDays(futureBinDays);

			return mergedBinDays;
		}

		/// <summary>
		/// Merges a collection of BinDay objects, combining those with the same date.
		/// </summary>
		/// <param name="binDays">The collection of BinDay objects to merge.</param>
		/// <returns>A read-only collection of merged BinDay objects.</returns>
		public static ReadOnlyCollection<BinDay> MergeBinDays(IEnumerable<BinDay> binDays)
		{
			var mergedBinDays = new List<BinDay>();

			// Group the input BinDay objects by their Date property.
			var groupedBinDays = binDays.GroupBy(bd => bd.Date);

			// Iterate through each group of BinDay objects with the same date.
			foreach (var group in groupedBinDays)
			{
				// Initialize a list to store the merged Bins for the current date.
				var mergedBins = new List<Bin>();

				// Add all Bins from the current BinDay to the mergedBins list.
				foreach (var binDay in group)
				{
					mergedBins.AddRange(binDay.Bins);
				}

				// Create a new BinDay object with the merged Bins, the common date, and the address from the first BinDay in the group.
				var mergedBinDay = new BinDay
				{
					Date = group.Key,
					Address = group.First().Address,
					Bins = mergedBins.DistinctBy(b => b.Name).ToList().AsReadOnly()
				};
				mergedBinDays.Add(mergedBinDay);
			}

			return mergedBinDays.AsReadOnly();
		}

		/// <summary>
		/// Filters a collection of BinDay objects to only include those with dates in the future.
		/// </summary>
		/// <param name="binDays">The collection of BinDay objects to filter.</param>
		/// <returns>A read-only collection of BinDay objects with dates in the future.</returns>
		public static ReadOnlyCollection<BinDay> GetFutureBinDays(IEnumerable<BinDay> binDays)
		{
			var futureBinDays = new List<BinDay>();
			var today = DateOnly.FromDateTime(DateTime.Now);

			foreach (var binDay in binDays)
			{
				if (binDay.Date >= today)
				{
					futureBinDays.Add(binDay);
				}
			}

			// Sort bin days in ascending order by date
			futureBinDays.Sort((a, b) => a.Date.CompareTo(b.Date));

			return futureBinDays.AsReadOnly();
		}

		/// <summary>
		/// Parses a 'Set-Cookie' header string and extracts the cookie key-value pairs
		/// suitable for use in a 'Cookie' request header.
		/// </summary>
		/// <param name="setCookieHeader">The raw 'Set-Cookie' header string containing one or more cookie definitions.</param>
		/// <returns>A string containing the cookie key-value pairs (e.g. "key1=value1; key2=value2")
		/// ready for use in a 'Cookie' request header, or an empty string if the input is null or empty.</returns>
		public static string ParseSetCookieHeaderForRequestCookie(string setCookieHeader)
		{
			if (string.IsNullOrWhiteSpace(setCookieHeader))
			{
				return string.Empty;
			}

			var matches = CookieRegex().Matches(setCookieHeader);

			var cookieValues = matches
				.Cast<Match>()
				.Select(m => m.Groups[1].Value.Trim())
				.Where(cv => !string.IsNullOrWhiteSpace(cv))
				.ToList();


			return string.Join("; ", cookieValues);
		}

		/// <summary>
		/// Formats a postcode by ensuring there is a single space separating the outward and inward codes.
		/// </summary>
		/// <param name="postcode">The postcode string to format. It can be with or without spaces and in any case.</param>
		/// <returns>
		/// The formatted postcode in uppercase with a single space in the correct position (e.g. "SW1A 0AA"),
		/// or the original string if it's null or whitespace.
		/// </returns>
		public static string FormatPostcode(string postcode)
		{
			if (string.IsNullOrWhiteSpace(postcode))
			{
				return postcode;
			}

			// Remove all existing whitespace and convert to uppercase
			string formattedPostcode = WhitespaceRegex().Replace(postcode, "").ToUpper();

			// Insert a space before the last three characters
			if (formattedPostcode.Length > 3)
			{
				formattedPostcode = formattedPostcode.Insert(formattedPostcode.Length - 3, " ");
			}

			return formattedPostcode;
		}

		/// <summary>
		/// Gets a collection of bins that match a given service based on their keys.
		/// </summary>
		/// <param name="bins">The collection of all possible Bin objects.</param>
		/// <param name="service">The service string to match against the bin keys.</param>
		/// <returns>A read-only collection of Bin objects that match the given service.</returns>
		public static ReadOnlyCollection<Bin> GetMatchingBins(IReadOnlyCollection<Bin> bins, string service)
		{
			return bins.Where(bin =>
				bin.Keys.Any(key =>
					service.Contains(key, StringComparison.OrdinalIgnoreCase)
				)
			).ToList().AsReadOnly();
		}
	}
}
