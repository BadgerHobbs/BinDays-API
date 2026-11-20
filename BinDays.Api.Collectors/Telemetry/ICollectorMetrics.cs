namespace BinDays.Api.Collectors.Telemetry;

/// <summary>
/// Metric instruments recorded from inside the collector pipeline.
/// </summary>
/// <remarks>
/// The instruments themselves live in the API project, which owns the meter and its exporter
/// configuration and which references this project rather than the other way round. This
/// interface exists so the pipeline can record an event it is the only thing able to observe,
/// without the dependency having to point the wrong way.
/// </remarks>
public interface ICollectorMetrics
{
	/// <summary>
	/// Records collection service entries dropped because they matched none of the collector's
	/// bin types.
	/// </summary>
	/// <param name="govUkId">The gov.uk identifier of the collector that produced them.</param>
	/// <param name="count">The number of entries dropped, one per occurrence of a service.</param>
	void RecordBinDaysUnmatched(string govUkId, int count);
}
