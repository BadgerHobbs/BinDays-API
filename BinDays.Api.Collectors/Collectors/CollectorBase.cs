namespace BinDays.Api.Collectors.Collectors
{
    using System.Collections.ObjectModel;
    using BinDays.Api.Collectors.Models;

    /// <summary>
    /// Abstract base class for a collector.
    /// </summary>
    internal abstract class CollectorBase
    {
        /// <summary>
        /// Base url for gov.uk bin/rubbish collection days.
        /// </summary>
        private const string GovUkBaseUrl = "https://www.gov.uk/rubbish-collection-day/";

        /// <summary>
        /// Gets the name of the collector.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Gets the website url of the collector.
        /// </summary>
        public abstract Uri WebsiteUrl { get; }

        /// <summary>
        /// Gets the gov.uk id of the collector.
        /// </summary>
        public abstract string GovUkId { get; }

        /// <summary>
        /// Gets the gov.uk url of the collector.
        /// </summary>
        public virtual Uri GovUkUrl => new ($"{GovUkBaseUrl}{this.GovUkId}");

        /// <summary>
        /// Gets the addresses for a given postcode.
        /// </summary>
        /// <param name="postcode">The postcode to search for.</param>
        /// <returns>A read only collection of addresses.</returns>
        public abstract Task<ReadOnlyCollection<Address>> GetPostcodeAddresses(string postcode);

        /// <summary>
        /// Gets the bin days for a given address.
        /// </summary>
        /// <param name="address">The address to search for.</param>
        /// <returns>A read only collection of bin days.</returns>
        public abstract Task<ReadOnlyCollection<BinDay>> GetAddressBinDays(Address address);
    }
}