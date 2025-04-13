namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Models;

	/// <summary>
	/// Collector implementation for Wiltshire Council.
	/// </summary>
	internal sealed class WiltshireCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "Wiltshire Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://ilforms.wiltshire.gov.uk/WasteCollectionDays/");

		/// <inheritdoc/>
		public override string GovUkId => "wiltshire";

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
