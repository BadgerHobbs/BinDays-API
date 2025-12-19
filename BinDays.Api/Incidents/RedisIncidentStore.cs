namespace BinDays.Api.Incidents
{
	using System;
	using System.Collections.Generic;
	using System.Text.Json;
	using System.Text.Json.Serialization;

	/// <summary>
	/// Redis-backed implementation of <see cref="IIncidentStore"/>.
	/// </summary>
	internal sealed class RedisIncidentStore : IIncidentStore
	{
		private const string IndexKey = "health:incidents";
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

			var cutoffScore = ToScore(DateTime.UtcNow - RetentionWindow);

			var transaction = db.CreateTransaction();
			_ = transaction.SortedSetRemoveRangeByScoreAsync(IndexKey, double.NegativeInfinity, cutoffScore);
			_ = transaction.SortedSetAddAsync(IndexKey, payload, ToScore(incident.OccurredUtc));
			transaction.Execute();
		}

		/// <inheritdoc/>
		public IReadOnlyList<IncidentRecord> GetIncidents()
		{
			var db = _connectionMultiplexer.GetDatabase();
			var entries = db.SortedSetRangeByScore(IndexKey, order: Order.Descending);

			if (entries.Length == 0)
			{
				return [];
			}

			var incidents = entries
				.Where(e => e.HasValue)
				.Select(e => JsonSerializer.Deserialize<IncidentRecord>(e!, SerializerOptions))
				.OfType<IncidentRecord>()
				.ToList();

			return incidents;
		}

		/// <summary>
		/// Converts a UTC timestamp to a sorted-set score.
		/// </summary>
		/// <param name="utcDateTime">The UTC timestamp.</param>
		/// <returns>The numeric score.</returns>
		private static double ToScore(DateTime utcDateTime)
		{
			var utc = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
			return new DateTimeOffset(utc).ToUnixTimeMilliseconds();
		}
	}
}
