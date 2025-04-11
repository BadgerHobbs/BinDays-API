namespace BinDays.Api.Collectors.Collectors
{
    using System.Collections.ObjectModel;
    using BinDays.Api.Collectors.Models;

    /// <summary>
    /// Interface for a collector.
    /// </summary>
    internal interface ICollector
    {
        /// <summary>
        /// Gets the name of the collector.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the website url of the collector.
        /// </summary>
        public Uri WebsiteUrl { get; }

        /// <summary>
        /// Gets the gov.uk id of the collector.
        /// </summary>
        public string GovUkId { get; }

        /// <summary>
        /// Gets the gov.uk url of the collector.
        /// </summary>
        public Uri GovUkUrl { get; }

        /// <summary>
        /// Gets the addresses for a given postcode.
        /// </summary>
        /// <param name="postcode">The postcode to search for.</param>
        /// <returns>A read only collection of addresses.</returns>
        public Task<ReadOnlyCollection<Address>> GetPostcodeAddresses( string postcode);

        /// <summary>
        /// Gets the bin days for a given address.
        /// </summary>
        /// <param name="address">The address to search for.</param>
        /// <returns>A read only collection of bin days.</returns>
        public Task<ReadOnlyCollection<BinDay>> GetAddressBinDays(Address address);
    }
}
