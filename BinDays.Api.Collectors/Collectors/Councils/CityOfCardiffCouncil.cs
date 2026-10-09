namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for City of Cardiff Council.
/// </summary>
internal sealed partial class CityOfCardiffCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "City of Cardiff Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.cardiff.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "cardiff";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "General:" ],
		},
		new()
		{
			Name = "Paper & Cardboard Recycling",
			Colour = BinColour.Blue,
			Keys = [ "BlueRedSack", "Paper and cardboard:" ],
			Type = BinType.Sack,
		},
		new()
		{
			Name = "Plastic & Cans Recycling",
			Colour = BinColour.Red,
			Keys = [ "BlueRedSack", "Plastic and cans:" ],
			Type = BinType.Sack,
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Green,
			Keys = [ "Recycling Bag" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Glass Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Glass:" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Brown,
			Keys = [ "Food:" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Garden:" ],
		},
		new()
		{
			Name = "Hygiene Waste",
			Colour = BinColour.Purple,
			Keys = [ "Hygiene:" ],
			Type = BinType.Bag,
		},
	];

	/// <summary>
	/// The base URL of the council's web forms app.
	/// </summary>
	private const string _baseUrl = "https://app-cprd-webformsproxy-prd.azurewebsites.net";

	/// <summary>
	/// Regex for the verification token from the waste collections page.
	/// </summary>
	[GeneratedRegex(@"window\.verificationToken = '(?<token>[^']+)'")]
	private static partial Regex TokenRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting the verification token
		if (clientSideResponse == null)
		{
			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = CreateTokenRequest(),
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for getting addresses
		else if (clientSideResponse.RequestId == 1)
		{
			var token = TokenRegex().Match(clientSideResponse.Content).Groups["token"].Value;

			var requestBody = $$"""
			{
				"query": "{{postcode}}",
				"purpose": "WasteCalendar"
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseUrl}/api/Gazetteer/api/Search/LLPG/Residential",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.ApplicationJson },
					{ "verificationtoken", token },
				},
				Body = requestBody,
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 2)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var rawAddresses = jsonDoc.RootElement.GetProperty("results").EnumerateArray();

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var rawAddress in rawAddresses)
			{
				var address = new Address
				{
					Property = rawAddress.GetProperty("address").GetString()!.Trim(),
					Postcode = postcode,
					Uid = rawAddress.GetProperty("uprn").GetInt64().ToString(),
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
		// Prepare client-side request for getting the verification token
		if (clientSideResponse == null)
		{
			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = CreateTokenRequest(),
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting bin days
		else if (clientSideResponse.RequestId == 1)
		{
			var token = TokenRegex().Match(clientSideResponse.Content).Groups["token"].Value;

			var requestBody = $$"""
			{
				"uprn": {{address.Uid}}
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseUrl}/api/WasteManagement/api/WasteCollection",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.ApplicationJson },
					{ "verificationtoken", token },
				},
				Body = requestBody,
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 2)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var rawBinDays = jsonDoc.RootElement.GetProperty("collectionWeeks").EnumerateArray();

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var rawBinDay in rawBinDays)
			{
				var date = DateUtilities.ParseDateExact(rawBinDay.GetProperty("date").GetString()!, "yyyy-MM-dd'T'HH:mm:ss");

				// Iterate through each bin collected on the date, and create a new bin day object
				foreach (var rawBin in rawBinDay.GetProperty("bins").EnumerateArray())
				{
					// The type alone cannot tell the recycling sacks from mixed recycling bags, so match
					// on the type and container together (e.g. "Recycling: BlueRedSack")
					var service = $"{rawBin.GetProperty("type").GetString()!}: {rawBin.GetProperty("binId").GetString()!}";
					var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, service);

					var binDay = new BinDay
					{
						Date = date,
						Address = address,
						Bins = matchedBinTypes,
					};

					binDays.Add(binDay);
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

	/// <summary>
	/// Creates the initial client-side request for the waste collections page, which holds the verification token.
	/// </summary>
	private static ClientSideRequest CreateTokenRequest()
	{
		var clientSideRequest = new ClientSideRequest
		{
			RequestId = 1,
			Url = $"{_baseUrl}/WASTE_wc",
			Method = "GET",
		};

		return clientSideRequest;
	}
}
