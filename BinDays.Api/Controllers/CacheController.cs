namespace BinDays.Api.Controllers;

using BinDays.Api.Cache;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

/// <summary>
/// Provides endpoints for viewing and clearing cached data. All endpoints require an API key
/// via the <c>X-Api-Key</c> header.
/// </summary>
[ApiController]
[Route("cache")]
[ServiceFilter(typeof(ApiKeyAuthFilter))]
public sealed class CacheController : ControllerBase
{
	/// <summary>
	/// The cache store.
	/// </summary>
	private readonly ICacheStore _cache;

	/// <summary>
	/// The logger instance.
	/// </summary>
	private readonly ILogger<CacheController> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="CacheController"/> class.
	/// </summary>
	/// <param name="cache">The cache store.</param>
	/// <param name="logger">Logger instance.</param>
	public CacheController(ICacheStore cache, ILogger<CacheController> logger)
	{
		_cache = cache;
		_logger = logger;
	}

	/// <summary>
	/// Gets cached entries matching the specified filters.
	/// </summary>
	/// <param name="govUkId">Optional gov.uk ID filter.</param>
	/// <param name="postcode">Optional postcode filter.</param>
	/// <param name="uid">Optional address UID filter (applies to collections only).</param>
	/// <param name="type">Optional comma-separated type filter: collectors, addresses, collections.</param>
	/// <returns>The matching cached entries.</returns>
	[HttpGet]
	public IActionResult GetCachedEntries(string? govUkId, string? postcode, string? uid, string? type)
	{
		if (string.IsNullOrWhiteSpace(govUkId) && string.IsNullOrWhiteSpace(postcode) && string.IsNullOrWhiteSpace(uid))
		{
			return BadRequest("At least one of 'govUkId', 'postcode', or 'uid' must be provided.");
		}

		if (!TryParseTypes(type, out var types, out var errorMessage))
		{
			return BadRequest(errorMessage);
		}

		var formattedPostcode = postcode != null
			? CollectorsController.FormatPostcodeForCacheKey(postcode)
			: null;

		var keys = FindMatchingKeys(govUkId, formattedPostcode, uid, types);
		var entries = new List<object>();

		foreach (var key in keys)
		{
			var rawValue = _cache.GetString(key);
			if (rawValue == null)
			{
				continue;
			}

			entries.Add(new
			{
				key,
				data = JToken.Parse(rawValue),
			});
		}

		return Ok(new { entries });
	}

	/// <summary>
	/// Clears cached entries matching the specified filters.
	/// </summary>
	/// <param name="govUkId">Optional gov.uk ID filter.</param>
	/// <param name="postcode">Optional postcode filter.</param>
	/// <param name="uid">Optional address UID filter (applies to collections only).</param>
	/// <param name="type">Optional comma-separated type filter: collectors, addresses, collections.</param>
	/// <returns>The number of keys removed.</returns>
	[HttpDelete]
	public IActionResult ClearCache(string? govUkId, string? postcode, string? uid, string? type)
	{
		if (string.IsNullOrWhiteSpace(govUkId) && string.IsNullOrWhiteSpace(postcode) && string.IsNullOrWhiteSpace(uid))
		{
			return BadRequest("At least one of 'govUkId', 'postcode', or 'uid' must be provided.");
		}

		if (!TryParseTypes(type, out var types, out var errorMessage))
		{
			return BadRequest(errorMessage);
		}

		var formattedPostcode = postcode != null
			? CollectorsController.FormatPostcodeForCacheKey(postcode)
			: null;

		var keys = FindMatchingKeys(govUkId, formattedPostcode, uid, types);
		var keysRemoved = 0;

		foreach (var key in keys)
		{
			_cache.Remove(key);
			keysRemoved++;
		}

		_logger.LogInformation("Cleared {KeysRemoved} cache entries matching govUkId: {GovUkId}, postcode: {Postcode}, uid: {Uid}, type: {Type}.",
			keysRemoved, govUkId, postcode, uid, type);

		return Ok(new { keysRemoved });
	}

	/// <summary>
	/// Builds glob patterns from the filters and returns all matching cache keys.
	/// </summary>
	private List<string> FindMatchingKeys(string? govUkId, string? postcode, string? uid, HashSet<CacheType>? types)
	{
		var keys = new List<string>();
		var includeAll = types == null || types.Count == 0;

		// collector-{POSTCODE}
		// Collectors are keyed by postcode only, so govUkId-only or uid-only queries skip them.
		if ((includeAll || types!.Contains(CacheType.Collectors)) && postcode != null && uid == null)
		{
			keys.AddRange(_cache.FindKeys($"collector-{postcode}"));
		}

		// addresses-{govUkId}-{POSTCODE}
		// Addresses have no uid component, so uid-only queries skip them.
		if ((includeAll || types!.Contains(CacheType.Addresses)) && uid == null)
		{
			var pattern = (govUkId, postcode) switch
			{
				(not null, not null) => $"addresses-{govUkId}-{postcode}",
				(not null, null) => $"addresses-{govUkId}-*",
				(null, not null) => $"addresses-*-{postcode}",
				_ => null,
			};

			if (pattern != null)
			{
				keys.AddRange(_cache.FindKeys(pattern));
			}
		}

		// bin-days-{govUkId}-{POSTCODE}-{uid}
		if (includeAll || types!.Contains(CacheType.Collections))
		{
			var govPart = govUkId ?? "*";
			var postcodePart = postcode ?? "*";
			var uidPart = uid ?? "*";
			keys.AddRange(_cache.FindKeys($"bin-days-{govPart}-{postcodePart}-{uidPart}"));
		}

		return keys;
	}

	/// <summary>
	/// Parses the comma-separated type query parameter.
	/// </summary>
	private static bool TryParseTypes(string? type, out HashSet<CacheType>? types, out string? errorMessage)
	{
		types = null;
		errorMessage = null;

		if (string.IsNullOrWhiteSpace(type))
		{
			return true;
		}

		types = [];

		foreach (var segment in type.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			var mapped = segment.ToLowerInvariant() switch
			{
				"collectors" => CacheType.Collectors,
				"addresses" => CacheType.Addresses,
				"collections" => CacheType.Collections,
				_ => (CacheType?)null,
			};

			if (mapped == null)
			{
				errorMessage = $"Unknown cache type '{segment}'. Valid types are: collectors, addresses, collections.";
				types = null;
				return false;
			}

			types.Add(mapped.Value);
		}

		return true;
	}

	private enum CacheType
	{
		Collectors,
		Addresses,
		Collections,
	}
}
