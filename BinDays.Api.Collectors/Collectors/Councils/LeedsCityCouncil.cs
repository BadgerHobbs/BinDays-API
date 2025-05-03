namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Globalization;
	using System.Text.Json;

	/// <summary>
	/// Collector implementation for Leeds City Council.
	/// </summary>
	internal sealed partial class LeedsCityCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "Leeds City Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://www.leeds.gov.uk/bins-and-recycling/check-your-bin-day");

		/// <inheritdoc/>
		public override string GovUkId => "leeds";

		/// <summary>
		/// The API subscription key required for Leeds City Council API requests.
		/// </summary>
		private const string ApiSubscriptionKey = "ad8dd80444fe45fcad376f82cf9a5ab4";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly ReadOnlyCollection<Bin> binTypes = new List<Bin>()
		{
			new()
			{
				Name = "General Waste",
				Colour = "Black",
				Keys = new List<string>() { "Black" }.AsReadOnly(),
			},
			new()
			{
				Name = "Garden Waste",
				Colour = "Brown",
				Keys = new List<string>() { "Brown" }.AsReadOnly(),
			},
			new()
			{
				Name = "Recycling",
				Colour = "Green",
				Keys = new List<string>() { "Green" }.AsReadOnly(),
			},
		}.AsReadOnly();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting addresses
			if (clientSideResponse == null)
			{
				var requestUrl = $"https://api.leeds.gov.uk/public/addresses/v1/addresses?query={postcode}";

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = requestUrl,
					Method = "GET",
					Headers = new Dictionary<string, string>() {
						{"Ocp-Apim-Subscription-Key", ApiSubscriptionKey}
					},
					Body = string.Empty,
				};

				var getAddressesResponse = new GetAddressesResponse()
				{
					Addresses = null,
					NextClientSideRequest = clientSideRequest
				};

				return getAddressesResponse;
			}
			// Process addresses from response
			else if (clientSideResponse.RequestId == 1)
			{
				// Parse response content as JSON array
				using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);

				// Iterate through each address json, and create a new address object
				var addresses = new List<Address>();
				foreach (var addressElement in jsonDoc.RootElement.EnumerateArray())
				{
					string? property = addressElement.GetProperty("displayAddress").GetString();
					string? uprn = addressElement.GetProperty("uprn").GetString();

					var address = new Address()
					{
						Property = property?.Trim(),
						Street = null,
						Town = null,
						Postcode = postcode,
						Uid = uprn,
					};

					addresses.Add(address);
				}

				var getAddressesResponse = new GetAddressesResponse()
				{
					Addresses = addresses.AsReadOnly(),
					NextClientSideRequest = null
				};

				return getAddressesResponse;
			}

			// Throw exception for invalid request
			throw new InvalidOperationException("Invalid client-side request.");
		}

		/// <inheritdoc/>
		public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting bin days
			if (clientSideResponse == null)
			{
				var requestUrl = $"https://api.leeds.gov.uk/public/waste/v1/BinsDays?uprn={address.Uid}";

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = requestUrl,
					Method = "GET",
					Headers = new Dictionary<string, string>() {
						{"Ocp-Apim-Subscription-Key", ApiSubscriptionKey}
					},
					Body = string.Empty,
				};

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					BinDays = null,
					NextClientSideRequest = clientSideRequest
				};

				return getBinDaysResponse;
			}
			// Process bin days from response
			else if (clientSideResponse.RequestId == 1)
			{
				// Parse response content as JSON array
				using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);

				// Iterate through each bin day json, and create a new bin day object
				var binDays = new List<BinDay>();
				foreach (var binDayElement in jsonDoc.RootElement.EnumerateArray())
				{
					string type = binDayElement.GetProperty("type").GetString()!;
					string dateString = binDayElement.GetProperty("date").GetString()!;

					// Parse the date 
					var date = DateOnly.ParseExact(
						dateString,
						"yyyy-MM-dd'T'HH:mm:ss",
						CultureInfo.InvariantCulture,
						DateTimeStyles.None
					);

					// Get matching bin types from the type using the keys
					var matchedBinTypes = binTypes.Where(x => x.Keys.Any(y => type.Contains(y)));

					var binDay = new BinDay()
					{
						Date = date,
						Address = address,
						Bins = matchedBinTypes.ToList().AsReadOnly()
					};

					binDays.Add(binDay);
				}

				// Filter out bin days in the past
				binDays = [.. ProcessingUtilities.GetFutureBinDays(binDays)];

				// Merge the bin days
				binDays = [.. ProcessingUtilities.MergeBinDays(binDays)];

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					BinDays = binDays.AsReadOnly(),
					NextClientSideRequest = null
				};

				return getBinDaysResponse;
			}

			// Throw exception for invalid request
			throw new InvalidOperationException("Invalid client-side request.");
		}
	}
}
