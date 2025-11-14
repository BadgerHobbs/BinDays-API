namespace BinDays.Api.Incidents
{
	using StackExchange.Redis;
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Text.Json;
	using System.Text.Json.Serialization;

	/// <summary>
	/// Redis-backed implementation of <see cref="IIncidentStore"/>.
	/// </summary>
	internal sealed class RedisIncidentStore : IIncidentStore
	{
		private const string IncidentKeyPrefix = "health:incident:";
		private static readonly TimeSpan RetentionWindow = TimeSpan.FromDays(90);
		private static readonly JsonSerializerOptions SerializerOptions = new()
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			Converters = { new JsonStringEnumConverter() },
		};

		private readonly IConnectionMultiplexer _connectionMultiplexer;

		/// <summary>
		/// Initializes a new instance of the <see cref="RedisIncidentStore"/> class.
		/// </summary>
		/// <param name="multiplexer">The Redis connection.</param>
		public RedisIncidentStore(IConnectionMultiplexer multiplexer)
		{
			_connectionMultiplexer = multiplexer;
		}

		/// <inheritdoc/>
		public void RecordIncident(IncidentRecord incident)
		{
			ArgumentNullException.ThrowIfNull(incident);

			var db = _connectionMultiplexer.GetDatabase();
			var payload = JsonSerializer.Serialize(incident, SerializerOptions);
			var incidentKey = GetIncidentKey(incident.IncidentId);

			db.StringSet(incidentKey, payload, RetentionWindow);
		}

		/// <inheritdoc/>
		public IReadOnlyList<IncidentRecord> GetIncidents()
		{
			var db = _connectionMultiplexer.GetDatabase();
			var server = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().First());

			var incidents = new List<IncidentRecord>();
			foreach (var key in server.Keys(pattern: $"{IncidentKeyPrefix}*"))
			{
				var value = db.StringGet(key);
				if (!value.HasValue)
				{
					continue;
				}

				var incident = JsonSerializer.Deserialize<IncidentRecord>(value!, SerializerOptions);
				if (incident != null)
				{
					incidents.Add(incident);
				}
			}

			return [.. incidents.OrderByDescending(incident => incident.OccurredUtc)];
		}

		/// <summary>
		/// Builds the Redis key for a specific incident identifier.
		/// </summary>
		/// <param name="incidentId">The incident identifier.</param>
		/// <returns>The Redis key.</returns>
		private static string GetIncidentKey(Guid incidentId)
		{
			return $"{IncidentKeyPrefix}{incidentId.ToString("N", CultureInfo.InvariantCulture)}";
		}

	}
}
