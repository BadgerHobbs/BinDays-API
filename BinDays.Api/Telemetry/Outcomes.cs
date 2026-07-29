namespace BinDays.Api.Telemetry;

/// <summary>
/// Terminal outcome label values used on metrics, recorded once a lookup has
/// finished rather than on every request it took to get there.
/// </summary>
public static class Outcomes
{
	/// <summary>
	/// The lookup completed against the collector.
	/// </summary>
	public const string Success = "success";

	/// <summary>
	/// The lookup was served from cache.
	/// </summary>
	public const string CacheHit = "cache_hit";

	/// <summary>
	/// The supplied postcode was rejected as invalid.
	/// </summary>
	public const string InvalidPostcode = "invalid_postcode";

	/// <summary>
	/// The collector for the postcode is not currently supported.
	/// </summary>
	public const string UnsupportedCollector = "unsupported_collector";

	/// <summary>
	/// No gov.uk identifier was found for the postcode.
	/// </summary>
	public const string GovUkIdNotFound = "gov_uk_id_not_found";

	/// <summary>
	/// No supported collector was found for the gov.uk identifier.
	/// </summary>
	public const string CollectorNotFound = "collector_not_found";

	/// <summary>
	/// The request was rate limited by gov.uk.
	/// </summary>
	public const string RateLimited = "rate_limited";

	/// <summary>
	/// No addresses were found for the postcode.
	/// </summary>
	public const string AddressesNotFound = "addresses_not_found";

	/// <summary>
	/// No bin days were found for the address.
	/// </summary>
	public const string BinDaysNotFound = "bin_days_not_found";

	/// <summary>
	/// The client supplied a stale collector version.
	/// </summary>
	public const string VersionMismatch = "version_mismatch";

	/// <summary>
	/// Bin days were found but none matched a bin type, indicating a broken collector.
	/// </summary>
	public const string BinDaysUnmatched = "bin_days_unmatched";

	/// <summary>
	/// An unexpected error occurred.
	/// </summary>
	public const string Error = "error";
}
