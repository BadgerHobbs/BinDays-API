namespace BinDays.Api.Collectors.Exceptions
{
	using System;

	/// <summary>
	/// Exception thrown when a gov.uk ID is not found for a given postcode.
	/// </summary>
	public sealed class GovUkIdNotFoundException : Exception
	{
		/// <summary>
		/// The postcode that a gov.uk ID was not found for.
		/// </summary>
		public string Postcode { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="GovUkIdNotFoundException"/> class.
		/// </summary>
		/// <param name="postcode">The postcode that a gov.uk ID was not found for.</param>
		public GovUkIdNotFoundException(string postcode)
			: base($"No gov.uk ID found for postcode: {postcode}")
		{
			Postcode = postcode;
		}
	}
}