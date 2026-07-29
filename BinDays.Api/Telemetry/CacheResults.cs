namespace BinDays.Api.Telemetry;

/// <summary>
/// Cache result label values used on metrics, recording the outcome of a
/// distributed cache read.
/// </summary>
public static class CacheResults
{
	/// <summary>
	/// The cache held a usable entry.
	/// </summary>
	public const string Hit = "hit";

	/// <summary>
	/// The cache held no entry.
	/// </summary>
	public const string Miss = "miss";

	/// <summary>
	/// The cache held an entry that could not be deserialised.
	/// </summary>
	public const string Corrupt = "corrupt";
}
