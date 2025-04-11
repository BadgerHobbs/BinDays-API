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
        /// Gets the client side request for a given postcode.
        /// </summary>
        /// <param name="postcode">The postcode to search for.</param>
        /// <returns>The client side request.</returns>
        public ClientSideRequest GetPostcodeAddressesClientSideRequest(string postcode);

        /// <summary>
        /// Gets the addresses for a given postcode client-side response.
        /// </summary>
        /// <param name="clientSideResponse">The client side response.</param>
        /// <returns>A read only collection of addresses.</returns>
        public Task<ReadOnlyCollection<Address>> GetPostcodeAddresses(ClientSideResponse clientSideResponse);

        /// <summary>
        /// Gets the client side request for a given address.
        /// </summary>
        /// <param name="address">The address to search for.</param>
        /// <returns>The client side request.</returns>
        public ClientSideRequest GetAddressBinDaysClientSideRequest(Address address);

        /// <summary>
        /// Gets the bin days for a given address client-side response.
        /// </summary>
        /// <param name="clientSideResponse">The client side response.</param>
        /// <returns>A read only collection of bin days.</returns>
        public Task<ReadOnlyCollection<BinDay>> GetAddressBinDays(ClientSideResponse clientSideResponse);
    }
}
