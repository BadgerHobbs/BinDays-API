namespace BinDays.Api.Cache;

using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Redis-backed cache store that uses native <c>SCAN</c> for key enumeration
/// and <c>GET</c>/<c>SET</c>/<c>DEL</c> for value operations.
/// </summary>
internal sealed class RedisCacheStore : ICacheStore
{
	private readonly IConnectionMultiplexer _connectionMultiplexer;

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisCacheStore"/> class.
	/// </summary>
	/// <param name="connectionMultiplexer">The Redis connection.</param>
	public RedisCacheStore(IConnectionMultiplexer connectionMultiplexer)
	{
		_connectionMultiplexer = connectionMultiplexer;
	}

	/// <inheritdoc/>
	public string? GetString(string key)
	{
		var db = _connectionMultiplexer.GetDatabase();
		return db.StringGet(key);
	}

	/// <inheritdoc/>
	public void SetString(string key, string value, DateTimeOffset? absoluteExpiration = null)
	{
		var db = _connectionMultiplexer.GetDatabase();

		if (absoluteExpiration.HasValue)
		{
			var expiry = absoluteExpiration.Value - DateTimeOffset.UtcNow;
			if (expiry > TimeSpan.Zero)
			{
				db.StringSet(key, value, expiry);
			}
		}
		else
		{
			db.StringSet(key, value);
		}
	}

	/// <inheritdoc/>
	public void Remove(string key)
	{
		var db = _connectionMultiplexer.GetDatabase();
		db.KeyDelete(key);
	}

	/// <inheritdoc/>
	public IReadOnlyList<string> FindKeys(string pattern)
	{
		var server = _connectionMultiplexer.GetServers().First();
		return [.. server.Keys(pattern: pattern).Select(k => (string)k!)];
	}
}
