namespace BinDays.Api.Controllers;

using BinDays.Api.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using StackExchange.Redis;
using System.Linq;

/// <summary>
/// API controller for managing the cache.
/// </summary>
[ApiController]
[CacheApiKey]
[GenerateMcpTools]
public class CacheController : ControllerBase
{
	/// <summary>
	/// Distributed cache for storing responses.
	/// </summary>
	private readonly IDistributedCache _cache;

	/// <summary>
	/// Redis connection multiplexer for key scanning.
	/// </summary>
	private readonly IConnectionMultiplexer _multiplexer;

	/// <summary>
	/// Logger for the controller.
	/// </summary>
	private readonly ILogger<CacheController> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="CacheController"/> class.
	/// </summary>
	/// <param name="cache">Distributed cache for storing responses.</param>
	/// <param name="multiplexer">Redis connection multiplexer for key scanning.</param>
	/// <param name="logger">Logger for the controller.</param>
	public CacheController(IDistributedCache cache, IConnectionMultiplexer multiplexer, ILogger<CacheController> logger)
	{
		_cache = cache;
		_multiplexer = multiplexer;
		_logger = logger;
	}

	/// <summary>
	/// Formats a postcode string for use in a cache key by converting it to uppercase and removing spaces.
	/// </summary>
	/// <param name="postcode">The postcode string to format.</param>
	/// <returns>The formatted postcode string for cache key usage.</returns>
	private static string FormatPostcodeForCacheKey(string postcode)
	{
		return postcode.ToUpperInvariant().Replace(" ", string.Empty);
	}

	/// <summary>
	/// Removes all cache keys matching the given Redis glob pattern.
	/// </summary>
	/// <param name="pattern">The Redis glob pattern to match keys against.</param>
	private void RemoveByPattern(string pattern)
	{
		var server = _multiplexer.GetServers().First();
		foreach (var key in server.Keys(pattern: pattern))
		{
			_cache.Remove((string)key!);
		}
	}

	/// <summary>
	/// Returns a JSON object mapping each cache key matching the given Redis glob pattern to its cached value.
	/// </summary>
	/// <param name="pattern">The Redis glob pattern to match keys against.</param>
	/// <returns>A <see cref="JObject"/> mapping cache keys to their deserialized values.</returns>
	private JObject GetByPattern(string pattern)
	{
		var server = _multiplexer.GetServers().First();
		var result = new JObject();
		foreach (var key in server.Keys(pattern: pattern))
		{
			var stringKey = (string)key!;
			var value = _cache.GetString(stringKey);
			result[stringKey] = value != null ? JToken.Parse(value) : JValue.CreateNull();
		}
		return result;
	}

	/// <summary>
	/// Gets collector cache entries. Scans all entries when no postcode is provided,
	/// otherwise returns the entry for the given postcode.
	/// </summary>
	/// <param name="postcode">Optional postcode to retrieve a specific entry.</param>
	/// <returns>A JSON object mapping cache keys to their values, or 404 for an exact lookup with no result.</returns>
	[HttpGet]
	[Route("/cache/collectors")]
	public IActionResult GetCollectors(string? postcode = null)
	{
		var formattedPostcode = postcode != null ? FormatPostcodeForCacheKey(postcode) : null;
		var result = GetByPattern($"collector-{formattedPostcode ?? "*"}");

		if (postcode != null && result.Count == 0)
		{
			return NotFound();
		}

		return Content(result.ToString(), "application/json");
	}

	/// <summary>
	/// Gets address cache entries. Any combination of parameters may be provided to narrow the scope;
	/// omitting all parameters returns all address entries.
	/// </summary>
	/// <param name="govUkId">Optional gov.uk identifier to filter by.</param>
	/// <param name="postcode">Optional postcode to filter by.</param>
	/// <returns>A JSON object mapping cache keys to their values, or 404 for an exact lookup with no result.</returns>
	[HttpGet]
	[Route("/cache/addresses")]
	public IActionResult GetAddresses(string? govUkId = null, string? postcode = null)
	{
		var formattedPostcode = postcode != null ? FormatPostcodeForCacheKey(postcode) : null;
		var result = GetByPattern($"addresses-{govUkId ?? "*"}-{formattedPostcode ?? "*"}");

		if (govUkId != null && postcode != null && result.Count == 0)
		{
			return NotFound();
		}

		return Content(result.ToString(), "application/json");
	}

	/// <summary>
	/// Gets bin day cache entries. Any combination of parameters may be provided to narrow the scope;
	/// omitting all parameters returns all bin day entries.
	/// </summary>
	/// <param name="govUkId">Optional gov.uk identifier to filter by.</param>
	/// <param name="postcode">Optional postcode to filter by.</param>
	/// <param name="uid">Optional address UID to filter by.</param>
	/// <returns>A JSON object mapping cache keys to their values, or 404 for an exact lookup with no result.</returns>
	[HttpGet]
	[Route("/cache/bin-days")]
	public IActionResult GetBinDays(string? govUkId = null, string? postcode = null, string? uid = null)
	{
		var formattedPostcode = postcode != null ? FormatPostcodeForCacheKey(postcode) : null;
		var result = GetByPattern($"bin-days-{govUkId ?? "*"}-{formattedPostcode ?? "*"}-{uid ?? "*"}");

		if (govUkId != null && postcode != null && uid != null && result.Count == 0)
		{
			return NotFound();
		}

		return Content(result.ToString(), "application/json");
	}

	/// <summary>
	/// Clears all cache entries created by this application.
	/// </summary>
	/// <returns>No content.</returns>
	[HttpDelete]
	[Route("/cache")]
	public IActionResult ClearAll()
	{
		RemoveByPattern("collector-*");
		RemoveByPattern("addresses-*");
		RemoveByPattern("bin-days-*");
		_logger.LogInformation("Cleared all cache entries.");
		return NoContent();
	}

	/// <summary>
	/// Clears collector cache entries. Clears all collector entries when no postcode is provided,
	/// otherwise clears the entry for the given postcode.
	/// </summary>
	/// <param name="postcode">Optional postcode to limit clearing to a specific entry.</param>
	/// <returns>No content.</returns>
	[HttpDelete]
	[Route("/cache/collectors")]
	public IActionResult ClearCollectors(string? postcode = null)
	{
		if (postcode != null)
		{
			var formattedPostcode = FormatPostcodeForCacheKey(postcode);
			_cache.Remove($"collector-{formattedPostcode}");
		}
		else
		{
			RemoveByPattern("collector-*");
		}

		_logger.LogInformation(
			"Cleared collector cache entries for postcode: {Postcode}.",
			postcode ?? "*"
		);

		return NoContent();
	}

	/// <summary>
	/// Clears address cache entries. Any combination of parameters may be provided to narrow the scope;
	/// omitting all parameters clears all address entries.
	/// </summary>
	/// <param name="govUkId">Optional gov.uk identifier to filter by.</param>
	/// <param name="postcode">Optional postcode to filter by.</param>
	/// <returns>No content.</returns>
	[HttpDelete]
	[Route("/cache/addresses")]
	public IActionResult ClearAddresses(string? govUkId = null, string? postcode = null)
	{
		var formattedPostcode = postcode != null ? FormatPostcodeForCacheKey(postcode) : null;

		if (govUkId != null && formattedPostcode != null)
		{
			_cache.Remove($"addresses-{govUkId}-{formattedPostcode}");
		}
		else
		{
			RemoveByPattern($"addresses-{govUkId ?? "*"}-{formattedPostcode ?? "*"}");
		}

		_logger.LogInformation(
			"Cleared address cache entries for gov.uk ID: {GovUkId}, postcode: {Postcode}.",
			govUkId ?? "*",
			postcode ?? "*"
		);

		return NoContent();
	}

	/// <summary>
	/// Clears bin day cache entries. Any combination of parameters may be provided to narrow the scope;
	/// omitting all parameters clears all bin day entries.
	/// </summary>
	/// <param name="govUkId">Optional gov.uk identifier to filter by.</param>
	/// <param name="postcode">Optional postcode to filter by.</param>
	/// <param name="uid">Optional address UID to filter by.</param>
	/// <returns>No content.</returns>
	[HttpDelete]
	[Route("/cache/bin-days")]
	public IActionResult ClearBinDays(string? govUkId = null, string? postcode = null, string? uid = null)
	{
		var formattedPostcode = postcode != null ? FormatPostcodeForCacheKey(postcode) : null;

		if (govUkId != null && formattedPostcode != null && uid != null)
		{
			_cache.Remove($"bin-days-{govUkId}-{formattedPostcode}-{uid}");
		}
		else
		{
			RemoveByPattern($"bin-days-{govUkId ?? "*"}-{formattedPostcode ?? "*"}-{uid ?? "*"}");
		}

		_logger.LogInformation(
			"Cleared bin day cache entries for gov.uk ID: {GovUkId}, postcode: {Postcode}, UID: {Uid}.",
			govUkId ?? "*",
			postcode ?? "*",
			uid ?? "*"
		);

		return NoContent();
	}
}
