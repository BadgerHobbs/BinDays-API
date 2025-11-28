namespace BinDays.Api.Collectors.Models
{
	/// <summary>
	/// Represents the response from an address lookup request.
	/// This response can either contain the next client-side request to be made,
	/// or the final list of addresses found.
	/// </summary>
	public sealed class GetAddressesResponse
	{
		/// <summary>
		/// Gets the next client-side request to be made, if further requests are required.
		/// </summary>
		public ClientSideRequest? NextClientSideRequest { get; init; }

		/// <summary>
		/// Gets the list of addresses found, if no further client-side requests are required.
		/// </summary>
		public IReadOnlyCollection<Address>? Addresses { get; init; }
	}
}
