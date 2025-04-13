namespace BinDays.Api.Collectors.Collectors.Councils
{
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
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			throw new NotImplementedException("GetAddresses not implemented.");
		}

		/// <inheritdoc/>
		public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
		{
			throw new NotImplementedException("GetBinDays not implemented.");
		}
	}
}
