namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
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

	/// <inheritdoc/>
	public override int Version => 2;

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes = [
		new()
		{
			Name = "Rubbish",
			Colour = BinColour.Black,
			Keys = [ "Rubbish" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Recycling" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Brown,
			Keys = [ "Food waste" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Garden Waste" ],
		},
	];

	/// <summary>
	/// Used for the Address API call.
	/// </summary>
	private const string _apiKey = "99eca77b615d046fce9b4e65d27d9c53";

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
			var requestUrl = $"https://apim-uks-cepprod-int-01.azure-api.net/statmap/bcp_gazetteer/suggest?token={_apiKey}&searchedTerm={encodedPostcode}";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = requestUrl,
				Method = "GET",
			};

			var getAddressesResponse = new GetAddressesResponse
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
			var resultsElement = jsonDoc.RootElement.GetProperty("items");
			foreach (var addressElement in resultsElement.EnumerateArray())
			{
				var address = new Address
				{
					Property = addressElement.GetProperty("address").GetString()!.Trim(),
					Postcode = postcode,
					Uid = addressElement.GetProperty("id").GetInt64().ToString(),
				};

				addresses.Add(address);
			}

			var getAddressesResponse = new GetAddressesResponse
			{
				Addresses = [.. addresses],
			};

			return getAddressesResponse;
		}

		// Throw exception for invalid request
		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <inheritdoc/>
	public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting address UPRN from detail endpoint
		if (clientSideResponse == null)
		{
			var requestUrl = $"https://apim-uks-cepprod-int-01.azure-api.net/statmap/bcp_gazetteer/{address.Uid}?token={_apiKey}";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = requestUrl,
				Method = "GET",
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getBinDaysResponse;
		}
		// Extract UPRN from address detail and prepare bin days request
		else if (clientSideResponse.RequestId == 1)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var uprn = jsonDoc.RootElement.GetProperty("UPRN").GetString()!;

			var requestUrl = $"https://prod-17.uksouth.logic.azure.com/workflows/58253d7b7d754447acf9fe5fcf76f493/triggers/manual/paths/invoke?api-version=2016-06-01&sp=%2Ftriggers%2Fmanual%2Frun&sv=1.0&sig={_signature}";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = requestUrl,
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.ApplicationJson },
				},
				Body = JsonSerializer.Serialize(new { uprn }),
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getBinDaysResponse;
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 2)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);

			var binDays = new List<BinDay>();
			if (jsonDoc.RootElement.TryGetProperty("data", out var resultsElement))
			{
				foreach (var binTypeElement in resultsElement.EnumerateArray())
				{
					// Determine matching bin types from the description
					var description = binTypeElement.GetProperty("wasteContainerUsageTypeDescription").GetString()!;
					var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, description);

					var rangeEl = binTypeElement.GetProperty("scheduleDateRange");
					foreach (var dateEl in rangeEl.EnumerateArray())
					{
						var date = DateUtilities.ParseDateExact(dateEl.GetString()!, "yyyy-MM-dd");

						var binDay = new BinDay
						{
							Date = date,
							Address = address,
							Bins = matchedBinTypes,
						};

						binDays.Add(binDay);
					}
				}
			}

			var getBinDaysResponse = new GetBinDaysResponse
			{
				BinDays = ProcessingUtilities.ProcessBinDays(binDays),
			};

			return getBinDaysResponse;
		}

		// Throw exception for invalid request
		throw new InvalidOperationException("Invalid client-side request.");
	}
}
