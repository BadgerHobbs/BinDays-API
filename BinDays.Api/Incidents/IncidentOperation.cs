namespace BinDays.Api.Incidents
{
	using System.Text.Json.Serialization;

	/// <summary>
	/// Indicates which collector operation was being performed when an incident occurred.
	/// </summary>
	[JsonConverter(typeof(JsonStringEnumConverter))]
	public enum IncidentOperation
	{
		/// <summary>
		/// Incident occurred during the collector lookup flow.
		/// </summary>
		GetCollector,

		/// <summary>
		/// Incident occurred while retrieving addresses.
		/// </summary>
		GetAddresses,

		/// <summary>
		/// Incident occurred while retrieving bin days.
		/// </summary>
		GetBinDays,
	}
}
