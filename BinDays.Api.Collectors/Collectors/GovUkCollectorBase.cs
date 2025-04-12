namespace BinDays.Api.Collectors.Collectors
{
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Services;
	using System.Text.Json;

	/// <summary>
	/// Abstract base class for a gov.uk collector.
	/// </summary>
	public abstract class GovUkCollectorBase
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
		public virtual Uri GovUkUrl => new($"{GovUkBaseUrl}/{this.GovUkId}");

		/// <summary>
		/// Gets the collector for a given postcode, potentially requiring multiple steps via client-side responses.
		/// </summary>
		/// <param name="collectorService">The collector service.</param>
		/// <param name="postcode">The postcode to search for.</param>
		/// <param name="clientSideResponse">The response from a previous client-side request, if applicable.</param>
		/// <returns>The response containing either the next client-side request to make or the collector.</returns>
		public static GetCollectorResponse GetCollector(CollectorService collectorService, string postcode, ClientSideResponse? clientSideResponse)
		{

			if (clientSideResponse?.RequestId == 1)
			{
				// Get collector gov.uk id from response header
				var govUkId = clientSideResponse.Headers["location"].Split("/").Last().Trim();

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
			else
			{
				// Prepare client-side request
				var requestBody = JsonSerializer.Serialize(new { postcode });
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = GovUkBaseUrl,
					Method = "POST",
					Headers = [],
					Body = requestBody
				};

				var getCollectorResponse = new GetCollectorResponse()
				{
					Collector = null,
					NextClientSideRequest = clientSideRequest
				};

				return getCollectorResponse;
			}
		}
	}
}
