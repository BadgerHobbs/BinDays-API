namespace BinDays.Api.Collectors.Utilities;

/// <summary>
/// Provides constant values used throughout collector implementations.
/// </summary>
internal static class Constants
{
	/// <summary>
	/// The user agent string used for HTTP requests.
	/// This is because some collectors fail requests if it missing or empty.
	/// </summary>
	public const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:137.0) Gecko/20100101 Firefox/137.0";

	/// <summary>
	/// The content-type value for form URL-encoded request bodies.
	/// </summary>
	public const string FormUrlEncoded = "application/x-www-form-urlencoded";

	/// <summary>
	/// The content-type value for JSON request bodies.
	/// </summary>
	public const string ApplicationJson = "application/json";

	/// <summary>
	/// The value for the X-Requested-With header used to indicate AJAX requests.
	/// </summary>
	public const string XmlHttpRequest = "XMLHttpRequest";
}
