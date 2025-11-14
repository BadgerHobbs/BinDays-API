namespace BinDays.Api.Incidents
{
	using System;
	using System.Text.Json.Serialization;

	/// <summary>
	/// Value object persisted for each incident.
	/// </summary>
	public sealed class IncidentRecord
	{
		/// <summary>
		/// Gets or sets the incident identifier.
		/// </summary>
		public Guid IncidentId { get; set; }

		/// <summary>
		/// Gets or sets the collector gov.uk identifier associated with this incident.
		/// </summary>
		public string GovUkId { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the timestamp (UTC) when the incident occurred.
		/// </summary>
		public DateTime OccurredUtc { get; set; }

		/// <summary>
		/// Gets or sets the incident category.
		/// </summary>
		[JsonConverter(typeof(JsonStringEnumConverter))]
		public IncidentCategory Category { get; set; }

		/// <summary>
		/// Gets or sets the operation that was being executed.
		/// </summary>
		[JsonConverter(typeof(JsonStringEnumConverter))]
		public IncidentOperation Operation { get; set; }

		/// <summary>
		/// Gets or sets the hashed signature of the error message/stack trace.
		/// </summary>
		public string MessageHash { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the name of the exception type that triggered the incident.
		/// </summary>
		public string ExceptionType { get; set; } = string.Empty;
	}
}
