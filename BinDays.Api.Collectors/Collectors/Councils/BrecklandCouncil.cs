namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Collector implementation for Breckland Council.
/// </summary>
internal sealed class BrecklandCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Breckland Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.breckland.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "breckland";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes = [
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Refuse Collection Service" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Green,
			Keys = [ "Recycling Collection Service" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden Waste Collection Service" ],
			Type = BinType.Sack,
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Grey,
			Keys = [ "Food Waste Collection Service" ],
			Type = BinType.Caddy,
		},
	];

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var requestBody = JsonSerializer.Serialize(new
			{
				jsonrpc = "2.0",
				id = "1",
				method = "Breckland.Whitespace.JointWasteAPI.GetSiteIDsByPostcode",
				@params = new
				{
					postcode,
					environment = "live",
				},
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://www.breckland.gov.uk/apiserver/ajaxlibrary",
				Method = "POST",
				Headers = new()
				{
					{ "Content-Type", "application/json" },
					{ "X-Requested-With", "XMLHttpRequest" },
					{ "User-Agent", Constants.UserAgent },
				},
				Body = requestBody,
			};

			var response = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return response;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 1)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var addressElement in jsonDoc.RootElement.GetProperty("result").EnumerateArray())
			{
				var number = addressElement.GetProperty("number").GetString()?.Trim();
				var name = addressElement.GetProperty("name").GetString()?.Trim();
				var address1 = addressElement.GetProperty("address1").GetString()?.Trim();
				var address2 = addressElement.GetProperty("address2").GetString()?.Trim();
				var town = addressElement.GetProperty("town").GetString()?.Trim();
				var county = addressElement.GetProperty("county").GetString()?.Trim();

				var propertyParts = new[] { number, name, address1, address2, town, county }
					.Where(part => !string.IsNullOrWhiteSpace(part));

				var address = new Address
				{
					Property = string.Join(", ", propertyParts),
					Postcode = postcode,
					Uid = addressElement.GetProperty("uprn").GetString()!.Trim(),
				};

				addresses.Add(address);
			}

			var response = new GetAddressesResponse
			{
				Addresses = [.. addresses],
			};

			return response;
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
			var requestBody = JsonSerializer.Serialize(new
			{
				jsonrpc = "2.0",
				id = "1",
				method = "Breckland.Whitespace.JointWasteAPI.GetBinCollectionsByUprn",
				@params = new
				{
					uprn = address.Uid,
					environment = "live",
				},
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://www.breckland.gov.uk/apiserver/ajaxlibrary",
				Method = "POST",
				Headers = new()
				{
					{ "Content-Type", "application/json" },
					{ "X-Requested-With", "XMLHttpRequest" },
					{ "User-Agent", Constants.UserAgent },
				},
				Body = requestBody,
			};

			var response = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return response;
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 1)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var binDayElement in jsonDoc.RootElement.GetProperty("result").EnumerateArray())
			{
				var binType = binDayElement.GetProperty("collectiontype").GetString()!.Trim();
				var collectionDate = binDayElement.GetProperty("nextcollection").GetString()!.Trim();

				var parsedDate = DateTime.ParseExact(
					collectionDate,
					"dd/MM/yyyy HH:mm:ss",
					CultureInfo.InvariantCulture,
					DateTimeStyles.None
				);

				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, binType);

				var binDay = new BinDay
				{
					Date = DateOnly.FromDateTime(parsedDate),
					Address = address,
					Bins = matchedBins,
				};

				binDays.Add(binDay);
			}

			var response = new GetBinDaysResponse
			{
				BinDays = ProcessingUtilities.ProcessBinDays(binDays),
			};

			return response;
		}

		// Throw exception for invalid request
		throw new InvalidOperationException("Invalid client-side request.");
	}
}
