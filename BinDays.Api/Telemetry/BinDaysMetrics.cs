namespace BinDays.Api.Telemetry;

using BinDays.Api.Collectors.Telemetry;
using System.Diagnostics.Metrics;

/// <summary>
/// Application metric instruments for the BinDays API.
/// Registered as a single instance and injected into controllers.
/// </summary>
/// <remarks>
/// The API is stateless and does not contact councils itself. A single user lookup produces
/// several HTTP requests, as the client executes each returned client-side request in turn.
/// Steps and lookups are therefore recorded separately: <see cref="RecordStep"/> fires on every
/// request, whereas <see cref="RecordLookup"/> fires only once a terminal outcome is reached.
/// </remarks>
public sealed class BinDaysMetrics : ICollectorMetrics
{
	/// <summary>
	/// The OpenTelemetry service name reported for this application.
	/// </summary>
	public const string ServiceName = "bindays-api";

	/// <summary>
	/// The name of the meter that owns every instrument on this class.
	/// </summary>
	public const string MeterName = "BinDays.Api";

	/// <summary>
	/// Label value substituted for any gov.uk identifier that does not match a registered
	/// collector, bounding the cardinality of user-supplied route values.
	/// </summary>
	public const string UnknownGovUkId = "unknown";

	/// <summary>
	/// The name of the addresses returned instrument, used to configure its histogram
	/// boundaries.
	/// </summary>
	public const string AddressesReturnedInstrument = "bindays.addresses.returned";

	/// <summary>
	/// The name of the bin days returned instrument, used to configure its histogram
	/// boundaries.
	/// </summary>
	public const string BinDaysReturnedInstrument = "bindays.bin_days.returned";

	/// <summary>
	/// The upper bound on a client supplied collector version accepted as a label value,
	/// beyond which the version is reported as other.
	/// </summary>
	private const int MaximumLabelledVersion = 20;

	/// <summary>
	/// The meter owning every instrument on this class.
	/// </summary>
	private readonly Meter _meter = new(MeterName);

	/// <summary>
	/// Counts inbound HTTP requests forming part of a lookup.
	/// </summary>
	private readonly Counter<long> _lookupSteps;

	/// <summary>
	/// Counts lookups reaching a terminal outcome.
	/// </summary>
	private readonly Counter<long> _lookups;

	/// <summary>
	/// Counts distributed cache reads by result.
	/// </summary>
	private readonly Counter<long> _cacheOperations;

	/// <summary>
	/// Counts requests rejected because the client collector version is stale.
	/// </summary>
	private readonly Counter<long> _versionMismatches;

	/// <summary>
	/// Records the address count of completed address lookups.
	/// </summary>
	private readonly Histogram<long> _addressesReturned;

	/// <summary>
	/// Records the bin day count of completed bin day lookups.
	/// </summary>
	private readonly Histogram<long> _binDaysReturned;

	/// <summary>
	/// Counts bin days dropped for matching none of a collector's bin types.
	/// </summary>
	private readonly Counter<long> _binDaysUnmatched;

	/// <summary>
	/// Initialises a new instance of the <see cref="BinDaysMetrics"/> class.
	/// </summary>
	public BinDaysMetrics()
	{
		_lookupSteps = _meter.CreateCounter<long>(
			"bindays.lookup.steps",
			unit: "{step}",
			description: "HTTP requests handled as part of a multi-step lookup."
		);

		_lookups = _meter.CreateCounter<long>(
			"bindays.lookups",
			unit: "{lookup}",
			description: "Lookups that reached a terminal outcome."
		);

		_cacheOperations = _meter.CreateCounter<long>(
			"bindays.cache.operations",
			unit: "{operation}",
			description: "Distributed cache reads by result."
		);

		_versionMismatches = _meter.CreateCounter<long>(
			"bindays.collector.version_mismatches",
			unit: "{request}",
			description: "Requests rejected because the client collector version is stale."
		);

		_addressesReturned = _meter.CreateHistogram<long>(
			AddressesReturnedInstrument,
			unit: "{address}",
			description: "Addresses returned on a completed address lookup."
		);

		_binDaysReturned = _meter.CreateHistogram<long>(
			BinDaysReturnedInstrument,
			unit: "{bin_day}",
			description: "Bin days returned on a completed bin day lookup."
		);

		_binDaysUnmatched = _meter.CreateCounter<long>(
			"bindays.bin_days.unmatched",
			unit: "{bin_day}",
			description: "Bin days dropped for matching none of a collector's bin types."
		);
	}

	/// <summary>
	/// Records one inbound HTTP request belonging to a lookup.
	/// </summary>
	/// <param name="endpoint">The endpoint handling the request.</param>
	/// <param name="isContinuation">Whether the request carries a previous client-side response.</param>
	public void RecordStep(string endpoint, bool isContinuation)
	{
		_lookupSteps.Add(
			1,
			new("bindays.endpoint", endpoint),
			new("bindays.step", isContinuation ? Steps.Continuation : Steps.Initial)
		);
	}

	/// <summary>
	/// Records a lookup reaching a terminal outcome.
	/// </summary>
	/// <param name="endpoint">The endpoint handling the request.</param>
	/// <param name="govUkId">The gov.uk identifier, already bounded via <see cref="UnknownGovUkId"/>.</param>
	/// <param name="outcome">The terminal outcome.</param>
	public void RecordLookup(string endpoint, string govUkId, string outcome)
	{
		_lookups.Add(
			1,
			new("bindays.endpoint", endpoint),
			new("bindays.gov_uk_id", govUkId),
			new("bindays.outcome", outcome)
		);
	}

	/// <summary>
	/// Records the result of a distributed cache read.
	/// </summary>
	/// <param name="endpoint">The endpoint performing the read.</param>
	/// <param name="result">The cache result.</param>
	public void RecordCache(string endpoint, string result)
	{
		_cacheOperations.Add(
			1,
			new("bindays.endpoint", endpoint),
			new("bindays.cache_result", result)
		);
	}

	/// <summary>
	/// Records a rejected request caused by a stale client collector version.
	/// </summary>
	/// <param name="govUkId">The gov.uk identifier, already bounded via <see cref="UnknownGovUkId"/>.</param>
	/// <param name="clientVersion">The collector version supplied by the client.</param>
	public void RecordVersionMismatch(string govUkId, int clientVersion)
	{
		_versionMismatches.Add(
			1,
			new("bindays.gov_uk_id", govUkId),
			new("bindays.client_version", ClampVersion(clientVersion))
		);
	}

	/// <summary>
	/// Records the address count of a completed address lookup.
	/// </summary>
	/// <param name="govUkId">The gov.uk identifier, already bounded via <see cref="UnknownGovUkId"/>.</param>
	/// <param name="count">The number of addresses returned.</param>
	public void RecordAddressesReturned(string govUkId, int count)
	{
		_addressesReturned.Record(
			count,
			new KeyValuePair<string, object?>("bindays.gov_uk_id", govUkId)
		);
	}

	/// <summary>
	/// Records the bin day count of a completed bin day lookup.
	/// </summary>
	/// <param name="govUkId">The gov.uk identifier, already bounded via <see cref="UnknownGovUkId"/>.</param>
	/// <param name="count">The number of bin days returned.</param>
	public void RecordBinDaysReturned(string govUkId, int count)
	{
		_binDaysReturned.Record(
			count,
			new KeyValuePair<string, object?>("bindays.gov_uk_id", govUkId)
		);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Recorded from <c>CollectorService</c> rather than a controller, as the drop happens inside
	/// the pipeline and nothing downstream can still see it. Not run through the gov.uk identifier
	/// gate used elsewhere: this only ever fires for a collector that resolved and ran, so the
	/// value is already bounded to registered identifiers.
	/// </remarks>
	public void RecordBinDayUnmatched(string govUkId)
	{
		_binDaysUnmatched.Add(
			1,
			new KeyValuePair<string, object?>("bindays.gov_uk_id", govUkId)
		);
	}

	/// <summary>
	/// Clamps a client-supplied collector version to a bounded set of label values,
	/// as the version arrives as an unvalidated query parameter.
	/// </summary>
	/// <param name="clientVersion">The collector version supplied by the client.</param>
	/// <returns>The version as a string, or other if outside the expected range.</returns>
	private static string ClampVersion(int clientVersion)
	{
		if (clientVersion is >= 0 and <= MaximumLabelledVersion)
		{
			return clientVersion.ToString();
		}

		return "other";
	}
}
