namespace BinDays.Api.Collectors.Collectors
{
    /// <summary>
    /// Abstract base class for a gov.uk collector.
    /// </summary>
    internal abstract class GovUkCollectorBase
    {
        /// <summary>
        /// Base url for gov.uk bin/rubbish collection days.
        /// </summary>
        private const string GovUkBaseUrl = "https://www.gov.uk/rubbish-collection-day/";

        /// <summary>
        /// Gets the gov.uk id of the collector.
        /// </summary>
        public abstract string GovUkId { get; }

        /// <summary>
        /// Gets the gov.uk url of the collector.
        /// </summary>
        public virtual Uri GovUkUrl => new ($"{GovUkBaseUrl}{this.GovUkId}");
    }
}