namespace BinDays.Api.Controllers
{
	using BinDays.Api.Collectors.Exceptions;
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Services;
	using Microsoft.AspNetCore.Mvc;
	using Microsoft.Extensions.Logging;
	using Microsoft.Extensions.Caching.Memory;
	using Microsoft.AspNetCore.Http;

	/// <summary>
	/// API controller for managing collectors.
	/// </summary>
	[ApiController]
	public class CollectorsController : ControllerBase
	{
		/// <summary>
		/// Service for returning specific or all collectors.
		/// </summary>
		private readonly CollectorService _collectorService;

		/// <summary>
		/// Logger for the controller.
		/// </summary>
		private readonly ILogger<CollectorsController> _logger;

		/// <summary>
		/// Memory cache for storing responses.
		/// </summary>
		private readonly IMemoryCache _cache;

		/// <summary>
		/// Initializes a new instance of the <see cref="CollectorsController"/> class.
		/// </summary>
		/// <param name="collectorService">Service for retrieving collector information.</param>
		/// <param name="logger">Logger for the controller.</param>
		/// <param name="cache">Memory cache for storing responses.</param>
		public CollectorsController(CollectorService collectorService, ILogger<CollectorsController> logger, IMemoryCache cache)
		{
			_collectorService = collectorService;
			_logger = logger;
			_cache = cache;
		}

		/// <summary>
		/// Formats a postcode string for use in a cache key by converting it to uppercase and removing spaces.
		/// </summary>
		/// <param name="postcode">The postcode string to format.</param>
		/// <returns>The formatted postcode string for cache key usage.</returns>
		private static string FormatPostcodeForCacheKey(string postcode)
		{
			return postcode.ToUpperInvariant().Replace(" ", string.Empty);
		}

		/// <summary>
		/// Gets all the collectors.
		/// </summary>
		/// <returns>An enumerable collection of collectors or an error response.</returns>
		[HttpGet]
		[Route("/collectors")]
		public IActionResult GetCollectors()
		{
			try
			{
				var result = _collectorService.GetCollectors();
				return Ok(result);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An unexpected error occurred while retrieving all collectors.");
				return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred while fetching collectors. Please try again later.");
			}
		}

		/// <summary>
		/// Gets the collector for a given postcode, potentially requiring multiple steps via client-side responses.
		/// </summary>
		/// <param name="postcode">The postcode to search for.</param>
		/// <param name="clientSideResponse">The response from a previous client-side request, if applicable.</param>
		/// <returns>The response containing either the next client-side request to make or the collector, or an error response.</returns>
		[HttpPost]
		[Route("/collector")]
		public IActionResult GetCollector(string postcode, [FromBody] ClientSideResponse? clientSideResponse)
		{
			string cacheKey = $"collector-{FormatPostcodeForCacheKey(postcode)}";
			if (_cache.TryGetValue(cacheKey, out GetCollectorResponse? cachedResult))
			{
				_logger.LogInformation("Returning cached collector {CollectorName} for postcode: {Postcode}.", cachedResult!.Collector!.Name, postcode);
				return Ok(cachedResult);
			}

			try
			{
				var result = _collectorService.GetCollector(postcode, clientSideResponse);

				// Cache result if successful and no next client-side request
				if (result.NextClientSideRequest == null)
				{
					_logger.LogInformation("Successfully retrieved collector {CollectorName} for postcode: {Postcode}.", result.Collector!.Name, postcode);

					var cacheEntryOptions = new MemoryCacheEntryOptions { AbsoluteExpiration = DateTimeOffset.UtcNow.Date.AddDays(90) };
					_cache.Set(cacheKey, result, cacheEntryOptions);
				}

				return Ok(result);
			}
			catch (GovUkIdNotFoundException ex)
			{
				_logger.LogWarning(ex, "No gov.uk ID found for postcode: {Postcode}.", postcode);
				return NotFound("No collector found for the specified postcode.");
			}
			catch (SupportedCollectorNotFoundException ex)
			{
				_logger.LogWarning(ex, "No supported collector found for gov.uk ID: {GovUkId}, postcode: {Postcode}.", ex.GovUkId, postcode);
				return NotFound("No supported collector found for the specified postcode.");
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An unexpected error occurred while retrieving collector for postcode: {Postcode}.", postcode);
				return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred while fetching the collector for the specified postcode. Please try again later.");
			}
		}

		/// <summary>
		/// Gets addresses for a given gov.uk ID and postcode.
		/// </summary>
		/// <param name="govUkId">The gov.uk identifier for the collector.</param>
		/// <param name="postcode">The postcode to search addresses for.</param>
		/// <param name="clientSideResponse">The response from a previous client-side request, if applicable.</param>
		/// <returns>A response containing addresses, or an error response.</returns>
		[HttpPost]
		[Route("/{govUkId}/addresses")]
		public IActionResult GetAddresses(string govUkId, string postcode, [FromBody] ClientSideResponse? clientSideResponse)
		{
			string cacheKey = $"addresses-{govUkId}-{FormatPostcodeForCacheKey(postcode)}";
			if (_cache.TryGetValue(cacheKey, out GetAddressesResponse? cachedResult))
			{
				_logger.LogInformation("Returning {AddressCount} cached addresses for gov.uk ID: {GovUkId}, postcode: {Postcode}.", cachedResult!.Addresses!.Count, govUkId, postcode);
				return Ok(cachedResult);
			}

			try
			{
				var result = _collectorService.GetAddresses(govUkId, postcode, clientSideResponse);

				// Cache result if successful and no next client-side request
				if (result.NextClientSideRequest == null)
				{
					_logger.LogInformation("Successfully retrieved {AddressCount} addresses for gov.uk ID: {GovUkId}, postcode: {Postcode}.", result.Addresses!.Count, govUkId, postcode);

					var cacheEntryOptions = new MemoryCacheEntryOptions { AbsoluteExpiration = DateTimeOffset.UtcNow.Date.AddDays(30) };
					_cache.Set(cacheKey, result, cacheEntryOptions);
				}

				return Ok(result);
			}
			catch (SupportedCollectorNotFoundException ex)
			{
				_logger.LogWarning(ex, "No supported collector found for gov.uk ID: {GovUkId}.", govUkId);
				return NotFound("No supported collector found for the specified gov.uk ID.");
			}
			catch (AddressesNotFoundException ex)
			{
				_logger.LogWarning(ex, "No addresses found for gov.uk ID: {GovUkId}, postcode: {Postcode}.", govUkId, postcode);
				return NotFound("No addresses found for the specified postcode.");
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An unexpected error occurred while retrieving addresses for gov.uk ID: {GovUkId}, postcode: {Postcode}.", govUkId, postcode);
				return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred while fetching addresses. Please try again later.");
			}
		}

		/// <summary>
		/// Gets bin days for a given gov.uk ID, postcode, and unique address identifier.
		/// </summary>
		/// <param name="govUkId">The gov.uk identifier for the collector.</param>
		/// <param name="postcode">The postcode of the address.</param>
		/// <param name="uid">The unique identifier of the address.</param>
		/// <param name="clientSideResponse">The response from a previous client-side request, if applicable.</param>
		/// <returns>A response containing bin days, or an error response.</returns>
		[HttpPost]
		[Route("/{govUkId}/bin-days")]
		public IActionResult GetBinDays(string govUkId, string postcode, string uid, [FromBody] ClientSideResponse? clientSideResponse)
		{
			string cacheKey = $"bin-days-{govUkId}-{FormatPostcodeForCacheKey(postcode)}-{uid}";
			if (_cache.TryGetValue(cacheKey, out GetBinDaysResponse? cachedResult))
			{
				_logger.LogInformation("Returning {BinDayCount} cached bin days for gov.uk ID: {GovUkId}, postcode: {Postcode}, UID: {Uid}.", cachedResult!.BinDays!.Count, govUkId, postcode, uid);
				return Ok(cachedResult);
			}

			try
			{
				var address = new Address()
				{
					Postcode = postcode,
					Uid = uid
				};

				var result = _collectorService.GetBinDays(govUkId, address, clientSideResponse);

				// Cache result if successful and no next client-side request
				if (result.NextClientSideRequest == null)
				{
					_logger.LogInformation("Successfully retrieved {BinDayCount} bin days for gov.uk ID: {GovUkId}, postcode: {Postcode}, UID: {Uid}.", result.BinDays!.Count, govUkId, postcode, uid);

					// Cache until the day after the first bin day, or for 1 day if no bin days are returned.
					var firstBinDayDate = result.BinDays.FirstOrDefault()?.Date.ToDateTime(TimeOnly.MinValue);
					var cacheExpiration = (firstBinDayDate ?? DateTimeOffset.UtcNow.Date).AddDays(1);

					var cacheEntryOptions = new MemoryCacheEntryOptions { AbsoluteExpiration = cacheExpiration };
					_cache.Set(cacheKey, result, cacheEntryOptions);
				}

				return Ok(result);
			}
			catch (SupportedCollectorNotFoundException ex)
			{
				_logger.LogWarning(ex, "No supported collector found for gov.uk ID: {GovUkId}.", govUkId);
				return NotFound("No supported collector found for the specified gov.uk ID.");
			}
			catch (BinDaysNotFoundException ex)
			{
				_logger.LogWarning(ex, "No bin days found for gov.uk ID: {GovUkId}, postcode: {Postcode}, UID: {Uid}.", govUkId, postcode, uid);
				return NotFound("No bin days found for the specified address.");
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An unexpected error occurred while retrieving bin days for gov.uk ID: {GovUkId}, postcode: {Postcode}, UID: {Uid}.", govUkId, postcode, uid);
				return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred while fetching bin days. Please try again later.");
			}
		}
	}
}
