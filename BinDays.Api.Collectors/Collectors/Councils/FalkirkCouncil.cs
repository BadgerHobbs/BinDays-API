namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Collector implementation for Falkirk Council.
/// </summary>
internal sealed class FalkirkCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Falkirk Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://recycling.falkirk.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "falkirk";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Keys = [ "Green bin" ],
		},
		new()
		{
			Name = "Plastics, Cans & Cartons Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Blue bin" ],
		},
		new()
		{
			Name = "Paper & Cardboard Recycling",
			Colour = new("Burgundy", "#B8253F"),
			Keys = [ "Burgundy bin" ],
		},
		new()
		{
			Name = "Glass Recycling",
			Colour = BinColour.Black,
			Keys = [ "Black box" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Grey,
			Keys = [ "Food caddy" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Brown bin" ],
		},
		new()
		{
			Name = "Nappies & Incontinence Products",
			Colour = BinColour.White,
			Keys = [ "AHP bag" ],
			Type = BinType.Bag,
		},
	];

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://recycling.falkirk.gov.uk/osg/search/{postcode}",
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
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var rawAddress in jsonDoc.RootElement.EnumerateArray())
			{
				var address = new Address
				{
					Property = rawAddress.GetProperty("address").GetString()!.Trim(),
					Postcode = postcode,
					Uid = rawAddress.GetProperty("uprn").GetString()!,
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
				Url = $"https://recycling.falkirk.gov.uk/api/collections/{address.Uid}",
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
			var rawCollections = jsonDoc.RootElement.GetProperty("collections");

			// Iterate through each collection, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var rawCollection in rawCollections.EnumerateArray())
			{
				var service = rawCollection.GetProperty("type").GetString()!;
				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, service);

				foreach (var rawDate in rawCollection.GetProperty("dates").EnumerateArray())
				{
					var binDay = new BinDay
					{
						Date = DateUtilities.ParseDateExact(rawDate.GetString()!, "yyyy-MM-dd"),
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
}
