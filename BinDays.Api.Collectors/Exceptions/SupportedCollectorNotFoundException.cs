namespace BinDays.Api.Collectors.Exceptions
{
	using System;

	/// <summary>
	/// Exception thrown when a supported collector is not found for a given identifier.
	/// </summary>
	public sealed class SupportedCollectorNotFoundException : Exception
	{
		/// <summary>
		/// The gov.uk identifier that was not found.
		/// </summary>
		public string GovUkId { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="SupportedCollectorNotFoundException"/> class.
		/// </summary>
		/// <param name="govUkId">The gov.uk identifier that was not found.</param>
		public SupportedCollectorNotFoundException(string govUkId)
			: base($"No supported collector found for gov.uk ID: {govUkId}")
		{
			GovUkId = govUkId;
		}
	}
}
