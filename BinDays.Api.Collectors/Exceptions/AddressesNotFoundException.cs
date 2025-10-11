namespace BinDays.Api.Collectors.Exceptions
{
	using System;

	/// <summary>
	/// Exception thrown when no addresses are found for a given postcode and gov.uk ID.
	/// </summary>
	public sealed class AddressesNotFoundException : Exception
	{
		/// <summary>
		/// The gov.uk identifier for the collector.
		/// </summary>
		public string GovUkId { get; }

		/// <summary>
		/// The postcode for which addresses were not found.
		/// </summary>
		public string Postcode { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="AddressesNotFoundException"/> class.
		/// </summary>
		public AddressesNotFoundException(string govUkId, string postcode)
			: base($"No addresses found for gov.uk ID: {govUkId} and postcode: {postcode}")
		{
			GovUkId = govUkId;
			Postcode = postcode;
		}
	}
}
