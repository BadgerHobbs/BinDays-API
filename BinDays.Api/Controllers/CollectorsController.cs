namespace BinDays.Api.Controllers
{
	using BinDays.Api.Collectors.Collectors;
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Services;
	using Microsoft.AspNetCore.Mvc;
	using Microsoft.Extensions.Logging;
	using Microsoft.AspNetCore.Http;

	/// <summary>
	/// API controller for managing collectors.
	/// </summary>
	[ApiController]
	[Route("collectors")]
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
		/// Initializes a new instance of the <see cref="CollectorsController"/> class.
		/// </summary>
		/// <param name="collectorService">Service for retrieving collector information.</param>
		/// <param name="logger">Logger for the controller.</param>
		public CollectorsController(CollectorService collectorService, ILogger<CollectorsController> logger)
		{
			_collectorService = collectorService;
			_logger = logger;
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
			try
			{
				var result = GovUkCollectorBase.GetCollector(_collectorService, postcode, clientSideResponse);
				return Ok(result);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An unexpected error occurred while retrieving collector for postcode: {Postcode}.", postcode);
				return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred while fetching the collector for the specified postcode. Please try again later.");
			}
		}

		/// <summary>
		/// Gets addresses for a given Gov.uk ID and postcode.
		/// </summary>
		/// <param name="govUkId">The Gov.uk identifier for the collector.</param>
		/// <param name="postcode">The postcode to search addresses for.</param>
		/// <param name="clientSideResponse">The response from a previous client-side request, if applicable.</param>
		/// <returns>A response containing addresses, or an error response.</returns>
		[HttpPost]
		[Route("/{govUkId}/addresses")]
		public IActionResult GetAddresses(string govUkId, string postcode, [FromBody] ClientSideResponse? clientSideResponse)
		{
			try
			{
				ICollector collector = _collectorService.GetCollector(govUkId);
				var result = collector.GetAddresses(postcode, clientSideResponse);
				return Ok(result);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An unexpected error occurred while retrieving addresses for Gov.uk ID: {GovUkId}, Postcode: {Postcode}.", govUkId, postcode);
				return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred while fetching addresses. Please try again later.");
			}
		}

		/// <summary>
		/// Gets bin days for a given Gov.uk ID, postcode, and unique address identifier.
		/// </summary>
		/// <param name="govUkId">The Gov.uk identifier for the collector.</param>
		/// <param name="postcode">The postcode of the address.</param>
		/// <param name="uid">The unique identifier of the address.</param>
		/// <param name="clientSideResponse">The response from a previous client-side request, if applicable.</param>
		/// <returns>A response containing bin days, or an error response.</returns>
		[HttpPost]
		[Route("/{govUkId}/bin-days")]
		public IActionResult GetBinDays(string govUkId, string postcode, string uid, [FromBody] ClientSideResponse? clientSideResponse)
		{
			try
			{
				var address = new Address()
				{
					Postcode = postcode,
					Uid = uid
				};

				ICollector collector = _collectorService.GetCollector(govUkId);
				var result = collector.GetBinDays(address, clientSideResponse);
				return Ok(result);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An unexpected error occurred while retrieving bin days for Gov.uk ID: {GovUkId}, Postcode: {Postcode}, UID: {Uid}.", govUkId, postcode, uid);
				return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred while fetching bin days. Please try again later.");
			}
		}
	}
}
