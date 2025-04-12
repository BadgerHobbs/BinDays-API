namespace BinDays.Api.Controllers
{
    using BinDays.Api.Collectors.Collectors;
    using BinDays.Api.Collectors.Services;
    using Microsoft.AspNetCore.Mvc;

    /// <summary>
    /// Controller for managing collectors.
    /// </summary>
    [ApiController]
    [Route("collectors")]
    public class CollectorsController(CollectorService collectorService) : ControllerBase
    {
        /// <summary>
        /// Service for returning specific or all collectors.
        /// </summary>
        private readonly CollectorService collectorService = collectorService;

        /// <summary>
        /// Gets all the collectors.
        /// </summary>
        /// <returns>A list of collectors.</returns>
        [HttpGet]
        public IEnumerable<ICollector> GetCollectors()
        {
            return this.collectorService.GetCollectors();
        }
    }
}
