namespace BinDays.Api.Collectors.Telemetry;

/// <summary>
/// An <see cref="ICollectorMetrics"/> that records nothing, for tests and any host that does
/// not export metrics. Mirrors the role of NullLogger.
/// </summary>
public sealed class NullCollectorMetrics : ICollectorMetrics
{
	/// <summary>
	/// The shared instance.
	/// </summary>
	public static NullCollectorMetrics Instance { get; } = new();

	/// <summary>
	/// Prevents external instantiation, as the type is stateless.
	/// </summary>
	private NullCollectorMetrics()
	{
	}

	/// <inheritdoc/>
	public void RecordBinDayUnmatched(string govUkId)
	{
	}
}
