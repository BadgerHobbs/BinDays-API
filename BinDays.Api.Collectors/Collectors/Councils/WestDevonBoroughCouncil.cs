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
        public async Task<GetAddressesResponse> GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
        {
            await Task.FromResult(string.Empty);
            throw new NotImplementedException("GetAddresses not implemented.");
        }

        /// <inheritdoc/>
        public async Task<GetBinDaysResponse> GetBinDays(Address address, ClientSideResponse? clientSideResponse)
        {
            await Task.FromResult(string.Empty);
            throw new NotImplementedException("GetBinDays not implemented.");
        }
    }
}