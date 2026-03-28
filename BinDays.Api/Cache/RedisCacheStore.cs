namespace BinDays.Api.Cache;

using StackExchange.Redis;
using System;
using System.Collections.Generic;

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
		var servers = _connectionMultiplexer.GetServers();
		if (servers.Length == 0)
		{
			return [];
		}

		var keys = new List<string>();
		foreach (var key in servers[0].Keys(pattern: pattern))
		{
			keys.Add((string)key!);
		}
		return keys;
	}
}
