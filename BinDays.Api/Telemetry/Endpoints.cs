namespace BinDays.Api.Telemetry;

/// <summary>
/// Endpoint label values used on metrics, identifying which lookup an
/// observation belongs to.
/// </summary>
public static class Endpoints
{
	/// <summary>
	/// The collector lookup endpoint.
	/// </summary>
	public const string Collector = "collector";

	/// <summary>
	/// The address lookup endpoint.
	/// </summary>
	public const string Addresses = "addresses";

	/// <summary>
	/// The bin day lookup endpoint.
	/// </summary>
	public const string BinDays = "bin_days";
}
