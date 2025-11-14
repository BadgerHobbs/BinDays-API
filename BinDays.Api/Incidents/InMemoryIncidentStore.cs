namespace BinDays.Api.Incidents
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>
	/// In-memory fallback for development environments where Redis is not configured.
	/// </summary>
	internal sealed class InMemoryIncidentStore : IIncidentStore
	{
		private readonly object _lock = new();
		private readonly List<IncidentRecord> _records = [];

		/// <inheritdoc/>
		public void RecordIncident(IncidentRecord incident)
		{
			ArgumentNullException.ThrowIfNull(incident);

			lock (_lock)
			{
				_records.Add(incident);
			}
		}

		/// <inheritdoc/>
		public IReadOnlyList<IncidentRecord> GetIncidents()
		{
			lock (_lock)
			{
				return [.. _records.OrderByDescending(record => record.OccurredUtc)];
			}
		}
	}
}
