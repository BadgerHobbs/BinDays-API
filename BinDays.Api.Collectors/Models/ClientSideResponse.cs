namespace BinDays.Api.Collectors.Models
{
	using System.Collections.Generic;

	/// <summary>
	/// Model which represents a HTTP response from a request executed client-side.
	/// </summary>
	public sealed class ClientSideResponse
	{
		/// <summary>
		/// Gets the request id, used for determining the next client-side request (if required).
		/// </summary>
		required public int RequestId { get; init; }

		/// <summary>
		/// Gets the HTTP status code of the response.
		/// </summary>
		required public int StatusCode { get; init; }

		/// <summary>
		/// Gets the headers of the response.
		/// </summary>
		required public Dictionary<string, string> Headers { get; init; }

		/// <summary>
		/// Gets the content of the response as a string.
		/// </summary>
		required public string Content { get; init; }

		/// <summary>
		/// Gets the reason phrase of the response.
		/// </summary>
		required public string ReasonPhrase { get; init; }

		/// <summary>
		/// Gets a value indicating whether the request was successful.
		/// </summary>
		public bool IsSuccessStatusCode => this.StatusCode >= 200 && this.StatusCode <= 299;
	}
}
