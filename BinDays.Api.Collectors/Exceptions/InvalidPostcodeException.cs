namespace BinDays.Api.Collectors.Exceptions
{
	/// <summary>
	/// Exception thrown when an invalid postcode is provided.
	/// </summary>
	public sealed class InvalidPostcodeException : Exception
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="InvalidPostcodeException"/> class.
		/// </summary>
		/// <param name="postcode">The invalid postcode value.</param>
		public InvalidPostcodeException(string postcode)
			: base($"Invalid postcode: {postcode}")
		{
			Postcode = postcode;
		}

		/// <summary>
		/// Gets the invalid postcode value.
		/// </summary>
		public string Postcode { get; }
	}
}
