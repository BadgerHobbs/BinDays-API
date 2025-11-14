namespace BinDays.Api.Incidents
{
	using System.Text.Json.Serialization;

	/// <summary>
	/// High-level category assigned to incidents recorded for collectors.
	/// </summary>
	[JsonConverter(typeof(JsonStringEnumConverter))]
	public enum IncidentCategory
	{
		/// <summary>
		/// An unexpected failure occurred while calling a council/collector upstream.
		/// </summary>
		CollectorFailure,

		/// <summary>
		/// The platform encountered an infrastructure or configuration problem.
		/// </summary>
		SystemFailure,

		/// <summary>
		/// The collector API contract appears to have changed unexpectedly.
		/// </summary>
		IntegrationChanged,
	}
}
