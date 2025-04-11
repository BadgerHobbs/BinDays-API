namespace BinDays.Api.Collectors.Collectors.Councils
{
    using System.Collections.ObjectModel;
    using BinDays.Api.Collectors.Models;

    /// <summary>
    /// Collector implementation for West Devon Borough Council.
    /// </summary>
    internal sealed class WestDevonBoroughCouncil : CollectorBase, ICollector
    {
        /// <inheritdoc/>
        public override string Name => "West Devon Borough Council";

        /// <inheritdoc/>
        public override Uri WebsiteUrl => new ("https://westdevon.fccenvironment.co.uk/mycollections");

        /// <inheritdoc/>
        public override string GovUkId => "west-devon";

        /// <inheritdoc/>
        public override async Task<ReadOnlyCollection<Address>> GetPostcodeAddresses(string postcode)
        {
            await Task.FromResult(string.Empty);
            throw new NotImplementedException("GetPostcodeAddresses not implemented.");
        }

        /// <inheritdoc/>
        public override async Task<ReadOnlyCollection<BinDay>> GetAddressBinDays(Address address)
        {
            await Task.FromResult(string.Empty);
            throw new NotImplementedException("GetPostcodeAddresses not implemented.");
        }
    }
}