namespace BinDays.Api.Controllers
{
	using BinDays.Api.Collectors.Collectors;
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
		public IEnumerable<ICollector> GetCollectors()
		{
			return this.collectorService.GetCollectors();
		}
	}
}
