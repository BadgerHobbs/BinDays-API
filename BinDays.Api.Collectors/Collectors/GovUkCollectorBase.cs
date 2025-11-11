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
	internal abstract partial class GovUkCollectorBase
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
		/// Regex for the gov.uk ID from the first address.
		/// </summary>
		[GeneratedRegex(@"<option value=""(?<GovUkId>[^""]+)""")]
		private static partial Regex FirstAddressGovUkIdRegex();

		/// <summary>
		/// Regex for the gov.uk ID from the html.
		/// </summary>
		[GeneratedRegex(@"value=""https://www.gov.uk/.*?/(?<GovUkId>[\w-]+)""")]
		private static partial Regex GovUkIdRegex();

		/// <summary>
		/// Regex for the collector name from the html.
		/// </summary>
		[GeneratedRegex(@"<span class=""local-authority"">(?<CollectorName>.*?)<\/span>")]
		private static partial Regex CollectorNameRegex();

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
					Body = requestBody
				};

				var getCollectorResponse = new GetCollectorResponse()
				{
					NextClientSideRequest = clientSideRequest
				};

				return getCollectorResponse;
			}
			// Process collector from response
			else if (clientSideResponse.RequestId == 1)
			{
				// Check if multiple addresses returned, if so get the first Gov UK ID
				var firstAddressGovUkId = FirstAddressGovUkIdRegex().Match(clientSideResponse.Content).Groups["GovUkId"].Value;

				GetCollectorResponse getCollectorResponse;

				// If we found a gov.uk ID from address, make a second request to the collector page.
				if (!string.IsNullOrWhiteSpace(firstAddressGovUkId))
				{
					// Prepare client-side request
					var clientSideRequest = new ClientSideRequest()
					{
						RequestId = 2,
						Url = $"{GovUkBaseUrl}/{firstAddressGovUkId}",
						Method = "GET",
					};

					getCollectorResponse = new GetCollectorResponse()
					{
						NextClientSideRequest = clientSideRequest
					};

					return getCollectorResponse;
				}
				// If no gov.uk ID found from address, it should already be in the response.
				else
				{
					var collector = ExtractCollector(collectorService, postcode, clientSideResponse);

					getCollectorResponse = new GetCollectorResponse()
					{
						Collector = collector,
					};
				}

				return getCollectorResponse;
			}
			// Prepare client-side request for getting collector
			else if (clientSideResponse.RequestId == 2)
			{
				var collector = ExtractCollector(collectorService, postcode, clientSideResponse);

				var getCollectorResponse = new GetCollectorResponse()
				{
					Collector = collector,
				};

				return getCollectorResponse;
			}

			// Throw exception for invalid request
			throw new InvalidOperationException("Invalid client-side request.");
		}

		/// <summary>
		/// Extract the gov.uk collector ID and name from the response.
		/// </summary>
		private static ICollector ExtractCollector(CollectorService collectorService, string postcode, ClientSideResponse clientSideResponse)
		{
			// Try to get gov.uk ID from response header
			var govUkId = clientSideResponse.Headers.GetValueOrDefault("location")?.Split("/").Last().Trim();

			// If null, try to get gov.uk ID from response html
			govUkId ??= GovUkIdRegex().Match(clientSideResponse.Content).Groups["GovUkId"].Value;

			if (govUkId == null)
			{
				throw new GovUkIdNotFoundException(postcode);
			}

			var collectorName = CollectorNameRegex().Match(clientSideResponse.Content).Groups["CollectorName"].Value;
			if (string.IsNullOrWhiteSpace(collectorName))
			{
				throw new InvalidOperationException($"No collector name found in gov.uk response for ID: {govUkId}.");
			}

			// Get collector with matching gov.uk id
			ICollector collector;
			try
			{
				collector = collectorService.GetCollector(govUkId);
			}
			catch (SupportedCollectorNotFoundException)
			{
				throw new UnsupportedCollectorException(govUkId, collectorName);
			}

			return collector;
		}
	}
}
