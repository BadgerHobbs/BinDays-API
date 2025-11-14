namespace BinDays.Api.Incidents
{
	using StackExchange.Redis;
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Text.Json;
	using System.Text.Json.Serialization;

	/// <summary>
	/// Redis-backed implementation of <see cref="IIncidentStore"/>.
	/// </summary>
	internal sealed class RedisIncidentStore : IIncidentStore
	{
		private const string IndexKey = "health:incidents:index";
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

			var score = ToScore(incident.OccurredUtc);
			db.SortedSetAdd(IndexKey, incident.IncidentId.ToString("N", CultureInfo.InvariantCulture), score);

			// Prevent the index from sticking around indefinitely
			db.KeyExpire(IndexKey, RetentionWindow);
		}

		/// <inheritdoc/>
		public IReadOnlyList<IncidentRecord> GetIncidents()
		{
			var db = _connectionMultiplexer.GetDatabase();
			var ids = db.SortedSetRangeByScore(IndexKey, order: Order.Descending);

			var incidents = ids
				.Where(id => id.HasValue)
				.Select(id => id.ToString())
				.Where(id => Guid.TryParseExact(id, "N", out _))
				.Select(id => db.StringGet(GetIncidentKey(Guid.ParseExact(id!, "N"))))
				.Where(value => value.HasValue)
				.Select(value => JsonSerializer.Deserialize<IncidentRecord>(value!, SerializerOptions))
				.Where(incident => incident != null)
				.Cast<IncidentRecord>();

			return [.. incidents];
		}

		/// <summary>
		/// Converts the supplied UTC date to a sorted-set score.
		/// </summary>
		/// <param name="utcDateTime">The UTC timestamp.</param>
		/// <returns>The numeric score.</returns>
		private static double ToScore(DateTime utcDateTime)
		{
			var utc = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
			return new DateTimeOffset(utc).ToUnixTimeMilliseconds();
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
