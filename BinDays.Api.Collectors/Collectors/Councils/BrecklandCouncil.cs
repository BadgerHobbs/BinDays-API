namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
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
			Keys = [ "Refuse Collection Service", "Refuse" ],
			Type = BinType.Bin,
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Green,
			Keys = [ "Recycling Collection Service", "Recycling" ],
			Type = BinType.Bin,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden Waste Collection Service", "Garden Waste" ],
			Type = BinType.Sack,
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Grey,
			Keys = [ "Food Waste Collection Service", "Food Waste" ],
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
				Headers = new Dictionary<string, string>
				{
					{ "Content-Type", "application/json" },
					{ "X-Requested-With", "XMLHttpRequest" },
					{ "User-Agent", Constants.UserAgent },
				},
				Body = requestBody,
			};

			return new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest
			};
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 1)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);

			var addresses = new List<Address>();
			foreach (var addressElement in jsonDoc.RootElement.GetProperty("result").EnumerateArray())
			{
				var propertyParts = new List<string>();

				var number = addressElement.GetProperty("number").GetString();
				if (!string.IsNullOrWhiteSpace(number))
				{
					propertyParts.Add(number);
				}

				var name = addressElement.GetProperty("name").GetString();
				if (!string.IsNullOrWhiteSpace(name))
				{
					propertyParts.Add(name);
				}

				var address1 = addressElement.GetProperty("address1").GetString();
				if (!string.IsNullOrWhiteSpace(address1))
				{
					propertyParts.Add(address1);
				}

				var address2 = addressElement.GetProperty("address2").GetString();
				if (!string.IsNullOrWhiteSpace(address2))
				{
					propertyParts.Add(address2);
				}

				var town = addressElement.GetProperty("town").GetString();
				if (!string.IsNullOrWhiteSpace(town))
				{
					propertyParts.Add(town);
				}

				var county = addressElement.GetProperty("county").GetString();
				if (!string.IsNullOrWhiteSpace(county))
				{
					propertyParts.Add(county);
				}

				var postcodeResult = addressElement.GetProperty("postcode").GetString();
				if (!string.IsNullOrWhiteSpace(postcodeResult))
				{
					propertyParts.Add(postcodeResult);
				}

				var address = new Address
				{
					Property = string.Join(", ", propertyParts),
					Postcode = postcodeResult ?? postcode,
					Uid = addressElement.GetProperty("uprn").GetString()!,
				};

				addresses.Add(address);
			}

			return new GetAddressesResponse
			{
				Addresses = [.. addresses],
			};
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
				Headers = new Dictionary<string, string>
				{
					{ "Content-Type", "application/json" },
					{ "X-Requested-With", "XMLHttpRequest" },
					{ "User-Agent", Constants.UserAgent },
				},
				Body = requestBody,
			};

			return new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest
			};
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 1)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);

			var binDays = new List<BinDay>();
			foreach (var binDayElement in jsonDoc.RootElement.GetProperty("result").EnumerateArray())
			{
				var binType = binDayElement.GetProperty("collectiontype").GetString()!;
				var collectionDate = binDayElement.GetProperty("nextcollection").GetString()!;

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

			return new GetBinDaysResponse
			{
				BinDays = ProcessingUtilities.ProcessBinDays(binDays),
			};
		}

		// Throw exception for invalid request
		throw new InvalidOperationException("Invalid client-side request.");
	}
}
