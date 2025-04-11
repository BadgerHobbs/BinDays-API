namespace BinDays.Api.Collectors.Collectors.Councils
{
    using System.Collections.ObjectModel;
    using BinDays.Api.Collectors.Models;

    /// <summary>
    /// Collector implementation for Manchester City Council.
    /// </summary>
    internal sealed class ManchesterCityCouncil : GovUkCollectorBase, ICollector
    {
        /// <inheritdoc/>
        public string Name => "Manchester City Council";

        /// <inheritdoc/>
        public Uri WebsiteUrl => new("https://www.manchester.gov.uk/bincollections");

        /// <inheritdoc/>
        public override string GovUkId => "manchester";

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