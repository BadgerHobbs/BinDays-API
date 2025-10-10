namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Globalization;
	using System.Linq;
	using System.Text.Json;
	using System.Text.Json.Nodes;

	/// <summary>
	/// Collector implementation for Bristol City Council.
	/// </summary>
	internal sealed partial class BristolCityCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "Bristol City Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://bristolcouncil.powerappsportals.com/completedynamicformunauth/?servicetypeid=7dce896c-b3ba-ea11-a812-000d3a7f1cdc");

		/// <inheritdoc/>
		public override string GovUkId => "bristol";

		/// <summary>
		/// The API subscription key required for Bristol City Council API requests.
		/// </summary>
		private const string _apiSubscriptionKey = "47ffd667d69c4a858f92fc38dc24b150";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly ReadOnlyCollection<Bin> _binTypes = new List<Bin>()
		{
			new()
			{
				Name = "General Waste",
				Colour = BinColour.Black,
				Keys = new List<string>() { "General Waste" }.AsReadOnly(),
			},
			new()
			{
				Name = "Cans & Plastics Recycling",
				Colour = BinColour.Green,
				Keys = new List<string>() { "Green Recycling Box" }.AsReadOnly(),
				Type = BinType.Box,
			},
			new()
			{
				Name = "Brown Paper & Cardboard Recycling",
				Colour = BinColour.Blue,
				Keys = new List<string>() { "Blue Sack" }.AsReadOnly(),
				Type = BinType.Bag,
			},
			new()
			{
				Name = "Paper & Glass Recycling",
				Colour = BinColour.Black,
				Keys = new List<string>() { "Black Recycling Box" }.AsReadOnly(),
				Type = BinType.Box,
			},
			new()
			{
				Name = "Food Waste",
				Colour = BinColour.Brown,
				Keys = new List<string>() { "Food Waste Bin" }.AsReadOnly(),
			},
			new()
			{
				Name = "Garden Waste",
				Colour = BinColour.Green,
				Keys = new List<string>() { "Garden Waste Bin" }.AsReadOnly(),
			},
		}.AsReadOnly();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting addresses
			if (clientSideResponse == null)
			{
				var requestUrl = "https://bcprdapidyna002.azure-api.net/bcprdfundyna001-llpg/LLPGService";

				var requestHeaders = new Dictionary<string, string>() {
					{"Ocp-Apim-Subscription-Key", _apiSubscriptionKey},
				};

				var requestBody = JsonSerializer.Serialize(new { postcode });

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = requestUrl,
					Method = "POST",
					Headers = requestHeaders,
					Body = requestBody,
				};

				var getAddressesResponse = new GetAddressesResponse()
				{
					NextClientSideRequest = clientSideRequest
				};

				return getAddressesResponse;
			}
			// Process addresses from response
			else if (clientSideResponse.RequestId == 1)
			{
				// Parse response content as JSON object
				var responseJson = JsonSerializer.Deserialize<JsonObject>(clientSideResponse.Content)!;
				var rawAddresses = responseJson["data"]!.AsArray();

				// Iterate through each address json, and create a new address object
				var addresses = new List<Address>();
				foreach (var rawAddress in rawAddresses)
				{
					string fullAddress = rawAddress!["addressFull"]!.GetValue<string>().Trim();
					string uid = rawAddress!["GAZ_ID"]!.GetValue<string>();

					var address = new Address()
					{
						Property = fullAddress,
						Postcode = postcode,
						Uid = uid.Replace("UPRN", "", StringComparison.OrdinalIgnoreCase),
					};

					addresses.Add(address);
				}

				var getAddressesResponse = new GetAddressesResponse()
				{
					Addresses = addresses.AsReadOnly(),
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
				var requestUrl = "https://bcprdapidyna002.azure-api.net/bcprdfundyna001-alloy/NextCollectionDates";

				var requestHeaders = new Dictionary<string, string>() {
					{"Ocp-Apim-Subscription-Key", _apiSubscriptionKey},
				};

				var requestBody = JsonSerializer.Serialize(new { uprn = address.Uid });

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = requestUrl,
					Method = "POST",
					Headers = requestHeaders,
					Body = requestBody,
				};

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					NextClientSideRequest = clientSideRequest
				};

				return getBinDaysResponse;
			}
			// Process bin days from response
			else if (clientSideResponse.RequestId == 1)
			{
				// Parse response content as JSON object
				var responseJson = JsonSerializer.Deserialize<JsonObject>(clientSideResponse.Content)!;
				var rawBinDayCollections = responseJson["data"]!.AsArray();

				// Iterate through each collection type result
				var binDays = new List<BinDay>();
				foreach (var rawBinDayCollection in rawBinDayCollections)
				{
					var containerName = rawBinDayCollection!["containerName"]!.GetValue<string>();
					var collectionArray = rawBinDayCollection["collection"]!.AsArray();

					var collectionDate = collectionArray[0]!["nextCollectionDate"]!.GetValue<string>();

					// Find matching bin types based on the container name containing a key (case-insensitive)
					var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, containerName);

					// Parse the date string (e.g., "2025-04-15T00:00:00")
					var date = DateOnly.ParseExact(
						collectionDate,
						"yyyy-MM-dd'T'HH:mm:ss",
						CultureInfo.InvariantCulture,
						DateTimeStyles.None
					);

					var binDay = new BinDay()
					{
						Date = date,
						Address = address,
						Bins = matchedBins,
					};

					binDays.Add(binDay);
				}

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					BinDays = ProcessingUtilities.ProcessBinDays(binDays),
				};

				return getBinDaysResponse;
			}

			// Throw exception for invalid request
			throw new InvalidOperationException("Invalid client-side request.");
		}
	}
}
