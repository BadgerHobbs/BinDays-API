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
	/// Records a single bin day dropped because it matched none of the collector's bin types.
	/// </summary>
	/// <param name="govUkId">The gov.uk identifier of the collector that produced it.</param>
	void RecordBinDayUnmatched(string govUkId);
}
