namespace BinDays.Api.Collectors.Utilities
{
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
	}
}
