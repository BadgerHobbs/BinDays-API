namespace BinDays.Api.Collectors.Models
{
	/// <summary>
	/// Client side request/response options to preserve across request/response cycles.
	/// </summary>
	public sealed class ClientSideOptions
	{
		/// <summary>
		/// Follow HTTP redirects (3XX)
		/// </summary>
		public bool FollowRedirects { get; init; } = true;

		/// <summary>
		/// Metadata (e.g. tokens, cookies, etc.)
		/// </summary>
		public Dictionary<string, string> Metadata { get; init; } = [];
	}
}
