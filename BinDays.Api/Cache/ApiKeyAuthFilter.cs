namespace BinDays.Api.Cache;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

/// <summary>
/// Action filter that validates the <c>X-Api-Key</c> header against the <c>CacheApiKey</c>
/// configuration value. Returns 503 if the key is not configured, or 401 if the header is
/// missing or does not match.
/// </summary>
internal sealed class ApiKeyAuthFilter : IAsyncActionFilter
{
	private const string _apiKeyHeaderName = "X-Api-Key";
	private const string _configKeyName = "CacheApiKey";

	private readonly IConfiguration _configuration;

	/// <summary>
	/// Initializes a new instance of the <see cref="ApiKeyAuthFilter"/> class.
	/// </summary>
	/// <param name="configuration">The application configuration.</param>
	public ApiKeyAuthFilter(IConfiguration configuration)
	{
		_configuration = configuration;
	}

	/// <inheritdoc/>
	public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
	{
		var configuredKey = _configuration.GetValue<string>(_configKeyName);

		if (string.IsNullOrEmpty(configuredKey))
		{
			context.Result = new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
			return;
		}

		if (!context.HttpContext.Request.Headers.TryGetValue(_apiKeyHeaderName, out var providedKey)
			|| providedKey != configuredKey)
		{
			context.Result = new UnauthorizedResult();
			return;
		}

		await next();
	}
}
