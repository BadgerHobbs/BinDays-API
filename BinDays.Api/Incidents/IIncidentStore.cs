namespace BinDays.Api.Incidents
{
	using System.Collections.Generic;

	/// <summary>
	/// Persists incident records for health monitoring.
	/// </summary>
	public interface IIncidentStore
	{
		/// <summary>
		/// Records a new incident.
		/// </summary>
		/// <param name="incident">The incident to store.</param>
		void RecordIncident(IncidentRecord incident);

		/// <summary>
		/// Retrieves all recorded incidents ordered newest-first.
		/// </summary>
		/// <returns>The incidents ordered from newest to oldest.</returns>
		IReadOnlyList<IncidentRecord> GetIncidents();
	}
}
