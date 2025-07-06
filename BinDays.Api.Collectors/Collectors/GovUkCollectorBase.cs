namespace BinDays.Api.Collectors.Collectors
{
	using BinDays.Api.Collectors.Exceptions;
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Services;
	using System.Text.Json;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Abstract base class for a gov.uk collector.
	/// </summary>
	public abstract partial class GovUkCollectorBase
	{
		/// <summary>
		/// Base url for gov.uk bin/rubbish collection days.
		/// </summary>
		private const string GovUkBaseUrl = "https://www.gov.uk/rubbish-collection-day";

		/// <summary>
		/// Gets the gov.uk id of the collector.
		/// </summary>
		public abstract string GovUkId { get; }

		/// <summary>
		/// Gets the gov.uk url of the collector.
		/// </summary>
		public virtual Uri GovUkUrl => new($"{GovUkBaseUrl}/{GovUkId}");

		/// <summary>
		/// Regex for the gov.uk ID from the html.
		/// </summary>
		[GeneratedRegex(@"value=""https://www.gov.uk/.*?/(?<GovUkId>[\w-]+)""")]
		private static partial Regex GovUkIdRegex();

		/// <summary>
		/// Gets the collector for a given postcode, potentially requiring multiple steps via client-side responses.
		/// </summary>
		/// <param name="collectorService">The collector service.</param>
		/// <param name="postcode">The postcode to search for.</param>
		/// <param name="clientSideResponse">The response from a previous client-side request, if applicable.</param>
		/// <returns>The response containing either the next client-side request to make or the collector.</returns>
		public static GetCollectorResponse GetCollector(CollectorService collectorService, string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting collector
			if (clientSideResponse == null)
			{
				// Prepare client-side request
				var requestBody = JsonSerializer.Serialize(new { postcode });
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = GovUkBaseUrl,
					Method = "POST",
					Headers = [],
					Body = requestBody,
				};

				var getCollectorResponse = new GetCollectorResponse()
				{
					Collector = null,
					NextClientSideRequest = clientSideRequest
				};

				return getCollectorResponse;
			}
			// Process collector from response
			else if (clientSideResponse.RequestId == 1)
			{
				// Try to get gov.uk ID from response header
				var govUkId = clientSideResponse.Headers.GetValueOrDefault("location")?.Split("/").Last().Trim();

				// If null, try to get gov.uk ID from response html
				govUkId ??= GovUkIdRegex().Match(clientSideResponse.Content).Groups["GovUkId"].Value;

				if (govUkId == null)
				{
					throw new GovUkIdNotFoundException(postcode);
				}

				// Get collector with matching gov.uk id
				var collector = collectorService.GetCollector(govUkId);

				// Build response, no next client-side request required
				var getCollectorResponse = new GetCollectorResponse()
				{
					Collector = collector,
					NextClientSideRequest = null
				};

				return getCollectorResponse;
			}

			// Throw exception for invalid request
			throw new InvalidOperationException("Invalid client-side request.");
		}
	}
}
