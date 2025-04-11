namespace BinDays.Api.Collectors.Collectors.Councils
{
    using System.Collections.ObjectModel;
    using BinDays.Api.Collectors.Models;

    /// <summary>
    /// Collector implementation for West Devon Borough Council.
    /// </summary>
    internal sealed class WestDevonBoroughCouncil : CollectorBase, ICollector
    {
        /// <summary>
        /// Gets the name of the collector.
        /// </summary>
        public override string Name => "West Devon Borough Council";

        /// <summary>
        /// Gets the website url of the collector.
        /// </summary>
        public override Uri WebsiteUrl => new ("https://westdevon.fccenvironment.co.uk/mycollections");

        /// <summary>
        /// Gets the gov.uk id of the collector.
        /// </summary>
        public override string GovUkId => "west-devon";

        /// <summary>
        /// Gets the addresses for a given postcode.
        /// </summary>
        /// <param name="postcode">The postcode to search for.</param>
        /// <returns>A read only collection of addresses.</returns>
        public override async Task<ReadOnlyCollection<Address>> GetPostcodeAddresses(string postcode)
        {
            await Task.FromResult(string.Empty);
            throw new NotImplementedException("GetPostcodeAddresses not implemented.");
        }

        /// <summary>
        /// Gets the bin days for a given address.
        /// </summary>
        /// <param name="address">The address to search for.</param>
        /// <returns>A read only collection of bin days.</returns>
        public override async Task<ReadOnlyCollection<BinDay>> GetAddressBinDays(Address address)
        {
            await Task.FromResult(string.Empty);
            throw new NotImplementedException("GetPostcodeAddresses not implemented.");
        }
    }
}