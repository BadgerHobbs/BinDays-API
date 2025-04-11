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