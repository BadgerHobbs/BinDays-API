namespace BinDays.Api.Collectors.Utilities
{
	using BinDays.Api.Collectors.Models;
	using System.Collections.ObjectModel;
	using System.Web;

	/// <summary>
	/// Provides utility methods for processing data.
	/// </summary>
	internal static class ProcessingUtilities
	{
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
	}
}
