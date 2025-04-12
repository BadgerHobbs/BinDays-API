namespace BinDays.Api.Collectors.Services
{
    using System.Collections.ObjectModel;
    using BinDays.Api.Collectors.Collectors;

    /// <summary>
    /// Service for returning specific or all collectors.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="CollectorService"/> class.
    /// </remarks>
    /// <param name="collectors">The collectors.</param>
    internal sealed class CollectorService(IEnumerable<ICollector> collectors)
    {
        /// <summary>
        /// The list of collectors acquired via dependency injection.
        /// </summary>
        private readonly ReadOnlyCollection<ICollector> collectors = new([.. collectors]);

        /// <summary>
        /// Gets the collectors.
        /// </summary>
        /// <returns>The collectors.</returns>
        public ReadOnlyCollection<ICollector> GetCollectors()
        {
            return this.collectors;
        }

        /// <summary>
        /// Gets the collector for a given gov.uk identifier.
        /// </summary>
        /// <param name="govUkId">The gov.uk identifier.</param>
        /// <returns>The collector.</returns>
        public ICollector GetCollector(string govUkId)
        {
            return this.collectors.Where(collector => collector.GovUkId == govUkId).Single();
        }
    }
}
