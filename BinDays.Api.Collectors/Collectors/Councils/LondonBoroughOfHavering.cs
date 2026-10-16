namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

/// <summary>
/// Collector implementation for London Borough of Havering.
/// </summary>
internal sealed partial class LondonBoroughOfHavering : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "London Borough of Havering";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.havering.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "havering";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Service - Domestic Waste" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Orange,
			Keys = [ "Service - Recycling" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Service - Garden Waste" ],
		},
		new()
		{
			Name = "Communal General Waste",
			Colour = BinColour.Black,
			Keys = [ "Service - High Rise Domestic Waste" ],
			Type = BinType.Container,
		},
		new()
		{
			Name = "Communal Mixed Recycling",
			Colour = BinColour.Orange,
			Keys = [ "Service - High Rise Recycling" ],
			Type = BinType.Container,
		},
	];

	/// <summary>
	/// The base URL of the council's resident portal.
	/// </summary>
	private const string _portalBaseUrl = "https://portal.havering.gov.uk";

	/// <summary>
	/// The base URL of the council's API.
	/// </summary>
	private const string _apiBaseUrl = "https://api-prd.havering.gov.uk";

	/// <summary>
	/// Regex for the address search API key embedded in the address search page.
	/// </summary>
	[GeneratedRegex(@"apiKey = '(?<key>[^']+)'")]
	private static partial Regex AddressApiKeyRegex();

	/// <summary>
	/// Regex for the collections API subscription key embedded in the collection days page.
	/// </summary>
	[GeneratedRegex(@"Subscription-Key"", ""(?<key>[^""]+)""")]
	private static partial Regex SubscriptionKeyRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting the address search API key
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_portalBaseUrl}/Process-Waste-Address-CD/",
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
			var apiKey = AddressApiKeyRegex().Match(clientSideResponse.Content).Groups["key"].Value;

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_apiBaseUrl}/Address/Search?gazetteer=LLPG&search={postcode}",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "serviceid", apiKey },
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
			// The API returns XML rather than JSON for the browser accept header the client sends
			var xml = XDocument.Parse(clientSideResponse.Content);
			var ns = XNamespace.Get("http://schemas.datacontract.org/2004/07/DataWarehouseApi.Models.Property");

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var rawAddress in xml.Descendants(ns + "PropertySearchResult"))
			{
				var address = new Address
				{
					Property = rawAddress.Element(ns + "FullAddress")!.Value,
					Postcode = postcode,
					Uid = rawAddress.Element(ns + "NlpgUprn")!.Value,
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
		// Prepare client-side request for getting the collections API subscription key
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_portalBaseUrl}/Process-Waste-CollectionDays/",
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
			var subscriptionKey = SubscriptionKeyRegex().Match(clientSideResponse.Content).Groups["key"].Value;
			var today = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");

			var requestBody = $$"""
			{
				"getCollectionByUprnAndDate": {
					"getCollectionByUprnAndDateInput": {
						"uprn": "{{address.Uid}}",
						"nextCollectionFromDate": "{{today}}"
					}
				}
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_apiBaseUrl}/whitespace/GetCollectionByUprnAndDate",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.ApplicationJson },
					{ "ocp-apim-subscription-key", subscriptionKey },
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
			var rawBinDays = jsonDoc.RootElement
				.GetProperty("getCollectionByUprnAndDateResponse")
				.GetProperty("getCollectionByUprnAndDateResult")
				.GetProperty("Collections")
				.EnumerateArray();

			// Iterate through each collection, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var rawBinDay in rawBinDays)
			{
				var service = rawBinDay.GetProperty("service").GetString()!;

				// Take the date part of the timestamp (e.g. "22/10/2026 00:00:00")
				var date = DateUtilities.ParseDateExact(
					rawBinDay.GetProperty("date").GetString()![..10],
					"dd/MM/yyyy"
				);

				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = ProcessingUtilities.GetMatchingBins(_binTypes, service),
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
