namespace BinDays.Api.Controllers
{
	using BinDays.Api.Collectors.Collectors;
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Services;
	using Microsoft.AspNetCore.Mvc;

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
		private readonly CollectorService collectorService;

		/// <summary>
		/// Initializes a new instance of the <see cref="CollectorsController"/> class.
		/// </summary>
		/// <param name="collectorService">Service for retrieving collector information.</param>
		public CollectorsController(CollectorService collectorService)
		{
			this.collectorService = collectorService;
		}

		/// <summary>
		/// Gets all the collectors.
		/// </summary>
		/// <returns>An enumerable collection of collectors.</returns>
		[HttpGet]
		[Route("/collectors")]
		public IEnumerable<ICollector> GetCollectors()
		{
			return this.collectorService.GetCollectors();
		}

		/// <summary>
		/// Gets the collector for a given postcode, potentially requiring multiple steps via client-side responses.
		/// </summary>
		/// <param name="postcode">The postcode to search for.</param>
		/// <param name="clientSideResponse">The response from a previous client-side request, if applicable.</param>
		/// <returns>The response containing either the next client-side request to make or the collector.</returns>
		[HttpPost]
		[Route("/collector")]
		public GetCollectorResponse GetCollector(string postcode, [FromBody] ClientSideResponse? clientSideResponse)
		{
			return GovUkCollectorBase.GetCollector(collectorService, postcode, clientSideResponse);
		}
	}
}
