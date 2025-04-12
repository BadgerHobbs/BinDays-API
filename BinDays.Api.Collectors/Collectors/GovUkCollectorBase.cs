namespace BinDays.Api.Collectors.Collectors
{
	using BinDays.Api.Collectors.Models;

	/// <summary>
	/// Abstract base class for a gov.uk collector.
	/// </summary>
	internal abstract class GovUkCollectorBase
	{
		/// <summary>
		/// Base url for gov.uk bin/rubbish collection days.
		/// </summary>
		private const string GovUkBaseUrl = "https://www.gov.uk/rubbish-collection-day/";

		/// <summary>
		/// Gets the gov.uk id of the collector.
		/// </summary>
		public abstract string GovUkId { get; }

		/// <summary>
		/// Gets the gov.uk url of the collector.
		/// </summary>
		public virtual Uri GovUkUrl => new($"{GovUkBaseUrl}{this.GovUkId}");

		/// <summary>
		/// Gets the collector for a given postcode, potentially requiring multiple steps via client-side responses.
		/// </summary>
		/// <param name="postcode">The postcode to search for.</param>
		/// <param name="clientSideResponse">The response from a previous client-side request, if applicable.</param>
		/// <returns>The response containing either the next client-side request to make or the collector.</returns>
		public static async Task<GetCollectorResponse> GetCollectorGovUkId(string postcode, ClientSideResponse? clientSideResponse)
		{
			await Task.FromResult(string.Empty);
			throw new NotImplementedException("GetCollectorGovUkId not implemented.");
		}
	}
}
