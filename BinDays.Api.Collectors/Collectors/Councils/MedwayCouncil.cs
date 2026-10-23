namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Collector implementation for Medway Council.
/// </summary>
internal sealed class MedwayCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Medway Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.medway.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "medway";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	/// <remarks>
	/// All services are collected weekly on the same day, and the council only gives a single next collection date,
	/// so every bin type is returned for that date and no keys are matched.
	/// </remarks>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Paper & Cardboard Recycling",
			Colour = BinColour.Blue,
			Keys = [],
			Type = BinType.Sack,
		},
		new()
		{
			Name = "Glass, Cans & Plastics Recycling",
			Colour = BinColour.White,
			Keys = [],
			Type = BinType.Sack,
		},
		new()
		{
			Name = "Food & Garden Waste",
			Colour = BinColour.Brown,
			Keys = [],
		},
	];

	/// <summary>
	/// The base URL for the council's API.
	/// </summary>
	private const string _apiUrl = "https://api.medway.gov.uk/api";

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_apiUrl}/addressing/getaddresses/{postcode}",
				Method = "GET",
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 1)
		{
			// The lookup returns 404 with an empty body when the postcode has no addresses
			if (clientSideResponse.StatusCode == 404)
			{
				var getAddressesResponseNotFound = new GetAddressesResponse
				{
					Addresses = [],
				};

				return getAddressesResponseNotFound;
			}

			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var result in jsonDoc.RootElement.EnumerateArray())
			{
				var address = new Address
				{
					Property = result.GetProperty("addressText").GetString()!.Trim(),
					Postcode = postcode,
					Uid = result.GetProperty("uprn").GetInt64().ToString(),
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
		// Prepare client-side request for getting bin days
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_apiUrl}/waste/getwasteday/{address.Uid}",
				Method = "GET",
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 1)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var nextCollection = jsonDoc.RootElement.GetProperty("nextCollection").GetDateTimeOffset();

			var binDay = new BinDay
			{
				Date = DateOnly.FromDateTime(nextCollection.Date),
				Address = address,
				Bins = _binTypes,
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				BinDays = ProcessingUtilities.ProcessBinDays([binDay]),
			};

			return getBinDaysResponse;
		}

		// Throw exception for invalid request
		throw new InvalidOperationException("Invalid client-side request.");
	}
}
