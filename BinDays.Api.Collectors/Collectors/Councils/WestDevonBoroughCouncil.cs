namespace BinDays.Api.Collectors.Collectors.Councils
{
    using System.Collections.ObjectModel;
    using BinDays.Api.Collectors.Models;

    /// <summary>
    /// Collector implementation for West Devon Borough Council.
    /// </summary>
    internal sealed class WestDevonBoroughCouncil : GovUkCollectorBase, ICollector
    {
        /// <inheritdoc/>
        public string Name => "West Devon Borough Council";

        /// <inheritdoc/>
        public Uri WebsiteUrl => new ("https://westdevon.fccenvironment.co.uk/mycollections");

        /// <inheritdoc/>
        public override string GovUkId => "west-devon";

        /// <inheritdoc/>
        public ClientSideRequest GetPostcodeAddressesClientSideRequest(string postcode)
        {
            throw new NotImplementedException("GetPostcodeAddressesClientSideRequest not implemented.");
        }

        /// <inheritdoc/>
        public async Task<ReadOnlyCollection<Address>> GetPostcodeAddresses(ClientSideResponse clientSideResponse)
        {
            await Task.FromResult(string.Empty);
            throw new NotImplementedException("GetPostcodeAddresses not implemented.");
        }

        /// <inheritdoc/>
        public ClientSideRequest GetAddressBinDaysClientSideRequest(Address address)
        {
            throw new NotImplementedException("GetAddressBinDaysClientSideRequest not implemented.");
        }

        /// <inheritdoc/>
        public async Task<ReadOnlyCollection<BinDay>> GetAddressBinDays(ClientSideResponse clientSideResponse)
        {
            await Task.FromResult(string.Empty);
            throw new NotImplementedException("GetPostcodeAddresses not implemented.");
        }
    }
}