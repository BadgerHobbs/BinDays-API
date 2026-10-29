namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Mole Valley District Council.
/// </summary>
internal sealed partial class MoleValleyDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Mole Valley District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.molevalley.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "mole-valley";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Refuse" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Green,
			Keys = [ "Recycling" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden Waste" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Green,
			Keys = [ "Food Waste" ],
			Type = BinType.Caddy,
		},
	];

	/// <summary>
	/// Regex for the services and their next collection dates from the property's bins panel.
	/// </summary>
	[GeneratedRegex(@"<strong>(?<service>[^<]+)</strong>\s*<p>Next collection - <strong>\([A-Za-z]+\) (?<date>\d{2}/\d{2}/\d{4})</strong></p>")]
	private static partial Regex BinDaysRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = CreateAddressSearchRequest(postcode);

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
			var features = jsonDoc.RootElement.GetProperty("results").GetProperty("features");

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var feature in features.EnumerateArray())
			{
				var properties = feature.GetProperty("properties");

				var address = new Address
				{
					Property = properties.GetProperty("address_string").GetString()!.Trim(),
					Postcode = postcode,
					Uid = properties.GetProperty("blpu_uprn").GetString()!,
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
		// Prepare client-side request for getting the postcode's properties, as the lookup can only search by postcode or address
		if (clientSideResponse == null)
		{
			var clientSideRequest = CreateAddressSearchRequest(address.Postcode!);

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin days from the matching property's bins panel
		else if (clientSideResponse.RequestId == 1)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var features = jsonDoc.RootElement.GetProperty("results").GetProperty("features");

			// Iterate through each property, and find the matching property's panel
			var panelHtml = string.Empty;
			foreach (var feature in features.EnumerateArray())
			{
				var properties = feature.GetProperty("properties");

				if (properties.GetProperty("blpu_uprn").GetString() == address.Uid)
				{
					panelHtml = properties.GetProperty("three_column_layout_html").GetString()!;
					break;
				}
			}

			if (string.IsNullOrEmpty(panelHtml))
			{
				throw new InvalidOperationException("Unable to find matching address for this Uid.");
			}

			var rawBinDays = BinDaysRegex().Matches(panelHtml)!;

			// Iterate through each service, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var binDay = new BinDay
				{
					Date = DateUtilities.ParseDateExact(rawBinDay.Groups["date"].Value, "dd/MM/yyyy"),
					Address = address,
					Bins = ProcessingUtilities.GetMatchingBins(_binTypes, rawBinDay.Groups["service"].Value.Trim()),
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

	/// <summary>
	/// Creates the client-side request for searching the properties in a postcode.
	/// </summary>
	private static ClientSideRequest CreateAddressSearchRequest(string postcode)
	{
		// Results are paged ten at a time by default, so a large page size is requested to get the whole postcode,
		// and the JSON format is requested as browsers otherwise get the API's HTML view
		var clientSideRequest = new ClientSideRequest
		{
			RequestId = 1,
			Url = $"https://myproperty.molevalley.gov.uk/molevalley/api/live_addresses/{postcode}?format=json&page_size=1000",
			Method = "GET",
		};

		return clientSideRequest;
	}
}
