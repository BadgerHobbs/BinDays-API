namespace BinDays.Api.Cache;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// In-memory cache store backed by a <see cref="ConcurrentDictionary{TKey, TValue}"/>.
/// Supports key enumeration and glob-style pattern matching.
/// </summary>
internal sealed class InMemoryCacheStore : ICacheStore
{
	private readonly ConcurrentDictionary<string, CacheEntry> _entries = new();

	/// <inheritdoc/>
	public string? GetString(string key)
	{
		if (_entries.TryGetValue(key, out var entry))
		{
			if (entry.AbsoluteExpiration.HasValue && entry.AbsoluteExpiration.Value <= DateTimeOffset.UtcNow)
			{
				_entries.TryRemove(key, out _);
				return null;
			}

			return entry.Value;
		}

		return null;
	}

	/// <inheritdoc/>
	public void SetString(string key, string value, DateTimeOffset? absoluteExpiration = null)
	{
		_entries[key] = new CacheEntry(value, absoluteExpiration);
	}

	/// <inheritdoc/>
	public void Remove(string key)
	{
		_entries.TryRemove(key, out _);
	}

	/// <inheritdoc/>
	public IReadOnlyList<string> FindKeys(string pattern)
	{
		var regex = GlobToRegex(pattern);

		return
		[
			.. _entries.Keys.Where(key => regex.IsMatch(key)),
		];
	}

	/// <summary>
	/// Converts a glob pattern with <c>*</c> wildcards to a compiled <see cref="Regex"/>.
	/// </summary>
	private static Regex GlobToRegex(string pattern)
	{
		var escaped = Regex.Escape(pattern).Replace("\\*", ".*");
		return new Regex($"^{escaped}$", RegexOptions.Compiled);
	}

	private sealed record CacheEntry(string Value, DateTimeOffset? AbsoluteExpiration);
}
