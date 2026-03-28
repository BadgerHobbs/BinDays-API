namespace BinDays.Api.Cache;

using System;
using System.Collections.Generic;

/// <summary>
/// Abstraction over distributed caching that supports key enumeration via glob patterns.
/// </summary>
public interface ICacheStore
{
	/// <summary>
	/// Gets the string value for the specified key.
	/// </summary>
	/// <param name="key">The cache key.</param>
	/// <returns>The cached string, or null if not found.</returns>
	string? GetString(string key);

	/// <summary>
	/// Sets a string value with an optional absolute expiration.
	/// </summary>
	/// <param name="key">The cache key.</param>
	/// <param name="value">The string value to cache.</param>
	/// <param name="absoluteExpiration">Optional absolute expiration time.</param>
	void SetString(string key, string value, DateTimeOffset? absoluteExpiration = null);

	/// <summary>
	/// Removes the entry for the specified key.
	/// </summary>
	/// <param name="key">The cache key.</param>
	void Remove(string key);

	/// <summary>
	/// Finds all keys matching a glob-style pattern. Supports <c>*</c> as a wildcard.
	/// </summary>
	/// <param name="pattern">The glob pattern (e.g. <c>addresses-E09000001-*</c>).</param>
	/// <returns>The matching keys.</returns>
	IReadOnlyList<string> FindKeys(string pattern);
}
