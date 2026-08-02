namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Collector implementation for Denbighshire County Council.
/// </summary>
internal sealed class DenbighshireCountyCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Denbighshire County Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.denbighshire.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "denbighshire";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "refuseDate" ],
		},
		new()
		{
			Name = "Dry Recycling",
			Colour = BinColour.Blue,
			Keys = [ "recyclingDate" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Green,
			Keys = [ "recyclingDate" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "gardenDate" ],
		},
		new()
		{
			Name = "Absorbent Hygiene Products",
			Colour = BinColour.Purple,
			Keys = [ "ahpDate" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Trade Waste",
			Colour = BinColour.Black,
			Keys = [ "tradeDate" ],
		},
		new()
		{
			Name = "Trade Refuse",
			Colour = BinColour.Black,
			Keys = [ "tradeRefuseDate" ],
		},
		new()
		{
			Name = "Trade Recycling",
			Colour = BinColour.Blue,
			Keys = [ "tradeRecyclingDate" ],
		},
	];

	/// <summary>
	/// The base URL for the Denbighshire refuse calendar API.
	/// </summary>
	private const string _apiBaseUrl = "https://refusecalendarapi.denbighshire.gov.uk";

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting a CSRF token
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_apiBaseUrl}/Csrf/token",
				Method = "GET",
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for getting addresses
		else if (clientSideResponse.RequestId == 1)
		{
			using var jsonDocument = JsonDocument.Parse(clientSideResponse.Content);
			var csrfToken = jsonDocument.RootElement.GetProperty("token").GetString()!;

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_apiBaseUrl}/Calendar/addresses/{postcode}",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "x-csrf-token", csrfToken },
				},
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
			using var jsonDocument = JsonDocument.Parse(clientSideResponse.Content);

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var rawAddress in jsonDocument.RootElement.EnumerateArray())
			{
				var address = new Address
				{
					Property = rawAddress.GetProperty("address").GetString()!.Trim(),
					Postcode = postcode,
					Uid = rawAddress.GetProperty("uprn").GetString()!.Trim(),
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
		// Prepare client-side request for getting a CSRF token
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_apiBaseUrl}/Csrf/token",
				Method = "GET",
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting bin days
		else if (clientSideResponse.RequestId == 1)
		{
			using var jsonDocument = JsonDocument.Parse(clientSideResponse.Content);
			var csrfToken = jsonDocument.RootElement.GetProperty("token").GetString()!;

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_apiBaseUrl}/Calendar/{address.Uid!}",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "x-csrf-token", csrfToken },
				},
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
			using var jsonDocument = JsonDocument.Parse(clientSideResponse.Content);
			var collections = jsonDocument.RootElement;

			var collectionFields = new string[]
			{
				"refuseDate",
				"recyclingDate",
				"gardenDate",
				"ahpDate",
				"tradeDate",
				"tradeRefuseDate",
				"tradeRecyclingDate",
			};

			// Iterate through each collection field, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var collectionField in collectionFields)
			{
				var dateString = collections.GetProperty(collectionField).GetString()!;
				if (string.IsNullOrWhiteSpace(dateString))
				{
					continue;
				}

				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, collectionField);

				var binDay = new BinDay
				{
					Date = DateUtilities.ParseDateExact(dateString, "dd/MM/yyyy"),
					Address = address,
					Bins = matchedBins,
				};

				binDays.Add(binDay);
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
