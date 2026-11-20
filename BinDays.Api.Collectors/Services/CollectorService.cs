namespace BinDays.Api.Collectors.Services;

using BinDays.Api.Collectors.Collectors;
using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Exceptions;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Telemetry;
using BinDays.Api.Collectors.Utilities;
using Microsoft.Extensions.Logging;

/// <summary>
/// Service for returning specific or all collectors.
/// </summary>
public sealed class CollectorService
{
	/// <summary>
	/// The list of collectors acquired via dependency injection.
	/// </summary>
	private readonly IReadOnlyCollection<ICollector> _collectors;

	/// <summary>
	/// The set of registered gov.uk identifiers, used for membership checks.
	/// </summary>
	private readonly HashSet<string> _govUkIds;

	/// <summary>
	/// The logger.
	/// </summary>
	private readonly ILogger<CollectorService> _logger;

	/// <summary>
	/// The metric instruments recorded from inside the pipeline.
	/// </summary>
	private readonly ICollectorMetrics _metrics;

	/// <summary>
	/// Initializes a new instance of the <see cref="CollectorService"/> class.
	/// </summary>
	/// <param name="collectors">The collectors.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="metrics">The metric instruments.</param>
	/// <exception cref="ArgumentNullException">Thrown when collectors is null.</exception>
	public CollectorService(IEnumerable<ICollector> collectors, ILogger<CollectorService> logger, ICollectorMetrics metrics)
	{
		_collectors = [.. collectors];
		_govUkIds = [.. _collectors.Select(collector => collector.GovUkId)];
		_logger = logger;
		_metrics = metrics;
	}

	/// <summary>
	/// Determines whether a gov.uk identifier corresponds to a registered collector.
	/// Used to bound metric label cardinality, as the identifier arrives as a
	/// user-supplied route parameter and is otherwise unbounded.
	/// </summary>
	/// <param name="govUkId">The gov.uk identifier.</param>
	/// <returns>True if a collector is registered for the identifier, otherwise false.</returns>
	public bool IsRegistered(string govUkId) => _govUkIds.Contains(govUkId);

	/// <summary>
	/// Gets the collectors.
	/// </summary>
	/// <returns>The collectors.</returns>
	public IReadOnlyCollection<ICollector> GetCollectors()
	{
		return _collectors;
	}

	/// <summary>
	/// Gets the collector for a given gov.uk identifier.        
	/// </summary>
	/// <param name="govUkId">The gov.uk identifier.</param>
	/// <returns>The collector if found.</returns>
	/// <exception cref="SupportedCollectorNotFoundException">Thrown when no collector matches the given govUkId.</exception>
	public ICollector GetCollector(string govUkId)
	{
		var collector = _collectors.SingleOrDefault(collector => collector.GovUkId == govUkId);
		return collector ?? throw new SupportedCollectorNotFoundException(govUkId);
	}

	/// <summary>
	/// Gets the collector for a given postcode, potentially requiring multiple steps via client-side responses.
	/// </summary>
	/// <param name="postcode">The postcode to search for.</param>
	/// <param name="clientSideResponse">The response from a previous client-side request, if applicable.</param>
	/// <returns>The response containing either the next client-side request to make or the collector.</returns>
	public GetCollectorResponse GetCollector(string postcode, ClientSideResponse? clientSideResponse)
	{
		return GovUkCollectorBase.GetCollector(this, postcode, clientSideResponse);
	}

	/// <summary>
	/// Gets the addresses for a given postcode.
	/// </summary>
	/// <param name="postcode">The postcode.</param>
	/// <param name="govUkId">The gov.uk identifier for the collector.</param>
	/// <param name="clientSideResponse">The response from a previous client-side request, if applicable.</param>
	/// <returns>The response containing either the next client-side request to make or the addresses.</returns>
	/// <exception cref="AddressesNotFoundException">Thrown when no addresses are found and no next client-side request.</exception>
	public GetAddressesResponse GetAddresses(string govUkId, string postcode, ClientSideResponse? clientSideResponse)
	{
		var collector = GetCollector(govUkId);
		var result = collector.GetAddresses(postcode, clientSideResponse);

		// Throw exception if no next client-side request and no addresses
		if (result.NextClientSideRequest == null && result.Addresses?.Count == 0)
		{
			throw new AddressesNotFoundException(govUkId, postcode);
		}

		return result;
	}

	/// <summary>
	/// Gets the bin collection days for a given address.
	/// </summary>
	/// <param name="govUkId">The gov.uk identifier for the collector.</param>
	/// <param name="address">The address to get bin days for.</param>
	/// <param name="clientSideResponse">The response from a previous client-side request, if applicable.</param>
	/// <returns>The response containing either the next client-side request to make or the bin days.</returns>
	/// <exception cref="BinDaysNotFoundException">Thrown when no bin days are found and no next client-side request.</exception>
	/// <exception cref="AllBinDaysUnmatchedException">Thrown when collection services are found but none matched a bin type.</exception>
	public GetBinDaysResponse GetBinDays(string govUkId, Address address, ClientSideResponse? clientSideResponse)
	{
		var collector = GetCollector(govUkId);

		// Record every council service that matches no bin type while the collector runs. Some
		// collectors skip these services and others leave an empty bin day that a same-date merge
		// absorbs, so the collector's response alone can't show what was lost.
		using var unmatchedServices = UnmatchedServiceTracker.BeginScope();
		var result = collector.GetBinDays(address, clientSideResponse);

		ReportUnmatchedServices(govUkId, address, unmatchedServices.Services);

		// Drop any bin days left with no bins, as their services have been reported above
		if (result.BinDays?.Any(binDay => binDay.Bins.Count == 0) == true)
		{
			result = new GetBinDaysResponse
			{
				NextClientSideRequest = result.NextClientSideRequest,
				BinDays = [.. result.BinDays.Where(binDay => binDay.Bins.Count > 0)],
			};
		}

		if (result.NextClientSideRequest == null && (result.BinDays == null || result.BinDays.Count == 0))
		{
			// If the council returned services but none matched a bin type, the collector's bin type
			// keys are broken rather than the address genuinely having no scheduled collections --
			// fail loudly instead of reporting "not found".
			if (unmatchedServices.Services.Count > 0)
			{
				throw new AllBinDaysUnmatchedException(govUkId, address.Postcode!, address.Uid!);
			}

			// Throw exception if no bin days were returned by the collector
			if (result.BinDays != null)
			{
				throw new BinDaysNotFoundException(govUkId, address.Postcode!, address.Uid!);
			}
		}

		return result;
	}

	/// <summary>
	/// Logs and counts the council services that matched none of the collector's bin types.
	/// </summary>
	/// <param name="govUkId">The gov.uk identifier for the collector.</param>
	/// <param name="address">The address the bin days were requested for.</param>
	/// <param name="services">The unmatched services, one entry per occurrence.</param>
	private void ReportUnmatchedServices(string govUkId, Address address, IReadOnlyList<string> services)
	{
		// Log each distinct service once with its count, as a calendar-style council site lists the
		// same service against every date it is collected on.
		foreach (var group in services.GroupBy(service => service, StringComparer.Ordinal))
		{
			_logger.LogWarning(
				"Service \"{Service}\" for gov.uk ID: {GovUkId}, postcode: {Postcode}, UID: {Uid} matched no bin types and was dropped ({Occurrences} occurrence(s)).",
				group.Key, govUkId, address.Postcode, address.Uid, group.Count()
			);
		}

		// Counted once per occurrence as well as logged: a partial drop is the earliest sign of a
		// council adding or renaming a service, and only escalates to AllBinDaysUnmatchedException
		// once nothing matches at all. Labelled by collector alone, since the service name is
		// council-supplied free text and would be unbounded as a metric label.
		if (services.Count > 0)
		{
			_metrics.RecordBinDaysUnmatched(govUkId, services.Count);
		}
	}
}
