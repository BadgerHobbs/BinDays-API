namespace BinDays.Api.Telemetry;

/// <summary>
/// Step label values used on metrics, distinguishing a request that begins a
/// lookup from one continuing a lookup already in progress.
/// </summary>
public static class Steps
{
	/// <summary>
	/// A request beginning a lookup.
	/// </summary>
	public const string Initial = "initial";

	/// <summary>
	/// A request continuing a lookup already in progress.
	/// </summary>
	public const string Continuation = "continuation";
}
