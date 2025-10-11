namespace BinDays.Api.Collectors.Models
{
	using System.Collections.Generic;

	/// <summary>
	/// Model which represents a HTTP request executed client-side.
	/// </summary>
	public sealed class ClientSideRequest
	{
		/// <summary>
		/// Gets the request id, used for determining the next client-side request (if required).
		/// </summary>
		required public int RequestId { get; init; }

		/// <summary>
		/// Gets the URL of the request.
		/// </summary>
		required public string Url { get; init; }

		/// <summary>
		/// Gets the HTTP method of the request.
		/// </summary>
		required public string Method { get; init; }

		/// <summary>
		/// Gets the headers of the request.
		/// </summary>
		public Dictionary<string, string> Headers { get; init; } = [];

		/// <summary>
		/// Gets the body of the request.
		/// </summary>
		public string? Body { get; init; }

		/// <summary>
		/// Gets the options of the request.
		/// </summary>
		public ClientSideOptions Options { get; init; } = new();
	}
}
