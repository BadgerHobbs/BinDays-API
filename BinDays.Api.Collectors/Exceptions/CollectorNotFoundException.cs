namespace BinDays.Api.Collectors.Exceptions
{
	using System;

	/// <summary>
	/// Exception thrown when a collector is not found for a given identifier.
	/// </summary>
	public class CollectorNotFoundException : Exception
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="CollectorNotFoundException"/> class.
		/// </summary>
		/// <param name="govUkId">The Gov.uk identifier that was not found.</param>
		public CollectorNotFoundException(string govUkId)
			: base($"No collector found with Gov.uk ID: {govUkId}")
		{
		}
	}
}