namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Collector implementation for Amber Valley Borough Council.
/// </summary>
internal sealed class AmberValleyBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Amber Valley Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.ambervalley.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "amber-valley";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Keys = [ "refuseNextDate" ],
		},
		new()
		{
			Name = "Paper, Plastic, Cans & Cardboard Recycling",
			Colour = BinColour.Grey,
			Keys = [ "recyclingNextDate" ],
		},
		new()
		{
			Name = "Glass Recycling",
			Colour = BinColour.Black,
			Keys = [ "recyclingNextDate" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "greenNextDate" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Grey,
			Keys = [ "NextFoodDate" ],
			Type = BinType.Caddy,
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
				Url = "https://info.ambervalley.gov.uk/WebServices/AVBCFeeds/GazetteerJSON.asmx/PropertyLookupFeed",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
				},
				Body = ProcessingUtilities.ConvertDictionaryToFormData(new()
				{
					{ "srchText", postcode },
				}),
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
					Property = rawAddress.GetProperty("addressComma").GetString()!,
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
				Url = $"https://info.ambervalley.gov.uk/WebServices/AVBCFeeds/WasteCollectionJSON.asmx/GetCollectionDetailsByUPRN?uprn={address.Uid}",
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

			// Iterate through each collection date field, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var service in new[] { "refuseNextDate", "recyclingNextDate", "greenNextDate", "NextFoodDate" })
			{
				var collectionDate = jsonDoc.RootElement.GetProperty(service).GetString()!;

				// Skip services the property does not have (placeholder date)
				if (collectionDate == "0001-01-01T00:00:00")
				{
					continue;
				}

				// Parse the date (e.g. "2026-10-15T00:00:00")
				var date = DateUtilities.ParseDateExact(collectionDate, "yyyy-MM-ddTHH:mm:ss");

				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, service);

				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = matchedBinTypes,
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
