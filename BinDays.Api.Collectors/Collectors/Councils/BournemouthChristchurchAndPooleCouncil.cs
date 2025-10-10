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

	/// <summary>
	/// Collector implementation for Bournemouth, Christchurch and Poole Council.
	/// </summary>
	internal sealed partial class BournemouthChristchurchAndPooleCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "Bournemouth, Christchurch and Poole Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://bcpportal.bcpcouncil.gov.uk/checkyourbincollection/");

		/// <inheritdoc/>
		public override string GovUkId => "bournemouth-christchurch-poole";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly ReadOnlyCollection<Bin> _binTypes = new List<Bin>()
		{
			new()
			{
				Name = "Rubbish",
				Colour = BinColour.Black,
				Keys = new List<string>() { "Rubbish" }.AsReadOnly(),
			},
			new()
			{
				Name = "Recycling",
				Colour = BinColour.Blue,
				Keys = new List<string>() { "Recycling" }.AsReadOnly(),
			},
			new()
			{
				Name = "Food Waste",
				Colour = BinColour.Brown,
				Keys = new List<string>() { "Food waste" }.AsReadOnly(),
				Type = BinType.Caddy,
			},
			new()
			{
				Name = "Garden Waste",
				Colour = BinColour.Green,
				Keys = new List<string>() { "Garden Waste" }.AsReadOnly(),
			},
		}.AsReadOnly();

		/// <summary>
		/// Used for the Address API call.
		/// </summary>
		private const string _apiKey = "f5a8f110545e4d009411c908b25b7596";

		/// <summary>
		/// Used for the Bin Day API call
		/// </summary>
		private const string _signature = "TAvYIUFj6dzaP90XQCm2ElY6Cd34ze05I3ba7LKTiBs";

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting addresses
			if (clientSideResponse == null)
			{
				var encodedPostcode = Uri.EscapeDataString(postcode);
				var requestUrl = $"https://apim-uks-cepprod-int-01.azure-api.net/LLPGSearch?searchText={encodedPostcode}&Subscription-Key={_apiKey}";

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = requestUrl,
					Method = "GET",
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
				// Parse response content as JSON array
				using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);

				// Iterate through each address json, and create a new address object
				var addresses = new List<Address>();
				var resultsElement = jsonDoc.RootElement.GetProperty("Results");
				foreach (var addressElement in resultsElement.EnumerateArray())
				{
					var address = new Address()
					{
						Property = addressElement.GetProperty("FULL_ADDRESS").GetString()!.Trim(),
						Postcode = postcode,
						Uid = addressElement.GetProperty("UPRN").GetString()!.Trim(),
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
				var requestUrl = $"https://prod-17.uksouth.logic.azure.com/workflows/58253d7b7d754447acf9fe5fcf76f493/triggers/manual/paths/invoke?api-version=2016-06-01&sp=%2Ftriggers%2Fmanual%2Frun&sv=1.0&sig={_signature}";

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = requestUrl,
					Method = "POST",
					Headers = new Dictionary<string, string>()
					{
						{ "user-agent", Constants.UserAgent }, { "content-type", "application/json" },
					},
					Body = JsonSerializer.Serialize(new { uprn = address.Uid }),
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
				using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);

				var binDays = new List<BinDay>();
				if (jsonDoc.RootElement.TryGetProperty("data", out JsonElement resultsElement))
				{
					foreach (var binTypeElement in resultsElement.EnumerateArray())
					{
						// Determine matching bin types from the description
						var description = binTypeElement.GetProperty("wasteContainerUsageTypeDescription").GetString()!;
						var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, description);

						var rangeEl = binTypeElement.GetProperty("scheduleDateRange");
						foreach (var dateEl in rangeEl.EnumerateArray())
						{
							var date = DateOnly.ParseExact(
								dateEl.GetString()!,
								"yyyy-MM-dd",
								CultureInfo.InvariantCulture,
								DateTimeStyles.None
							);

							var binDay = new BinDay()
							{
								Date = date,
								Address = address,
								Bins = matchedBinTypes,
							};

							binDays.Add(binDay);
						}
					}
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
