namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

/// <summary>
/// Collector implementation for Milton Keynes City Council.
/// </summary>
internal sealed class MiltonKeynesCityCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Milton Keynes City Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.milton-keynes.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "milton-keynes";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Collect Refuse" ],
		},
		new()
		{
			Name = "Plastic, Metal and Glass Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Collect Recycling Blue" ],
		},
		new()
		{
			Name = "Cardboard and Paper Recycling",
			Colour = BinColour.Red,
			Keys = [ "Collect Recycling Red" ],
		},
		new()
		{
			Name = "Food and Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Collect Food and Garden" ],
		},
	];

	/// <summary>
	/// The base URL for the Milton Keynes City Council API broker.
	/// </summary>
	private const string _baseUrl = "https://mycouncil.milton-keynes.gov.uk";

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for starting the session
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}/en/service/Waste_Collection_Round_Checker",
				Method = "GET",
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for address lookup
		else if (clientSideResponse.RequestId == 1)
		{
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var requestBody = $$"""
			{
				"formValues": {
					"Search": {
						"postcode_search": { "value": "{{postcode}}" }
					}
				}
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseUrl}/apibroker/runLookup?id=56a1f135c2a43",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.ApplicationJson },
					{ "cookie", cookies },
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
			var xmlData = jsonDoc.RootElement.GetProperty("data").GetString()!;

			var xml = XDocument.Parse(xmlData);
			var rows = xml.Descendants("Row");

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var row in rows)
			{
				var results = row.Elements("result").ToDictionary(e => e.Attribute("column")!.Value, e => e.Value.Trim());

				var address = new Address
				{
					Property = results["display"],
					Postcode = postcode,
					Uid = results["uprn"],
				};

				addresses.Add(address);
			}

			var getAddressesResponse = new GetAddressesResponse
			{
				Addresses = [.. addresses],
			};

			return getAddressesResponse;
		}

		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <inheritdoc/>
	public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for starting the session
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}/en/service/Waste_Collection_Round_Checker",
				Method = "GET",
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for bin days lookup
		else if (clientSideResponse.RequestId == 1)
		{
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var requestBody = $$"""
			{
				"formValues": {
					"Section 1": {
						"uprnCore": { "value": "{{address.Uid!}}" }
					}
				}
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseUrl}/apibroker/runLookup?id=64d9feda3a507",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.ApplicationJson },
					{ "cookie", cookies },
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
			var xmlData = jsonDoc.RootElement.GetProperty("data").GetString()!;

			var xml = XDocument.Parse(xmlData);
			var rows = xml.Descendants("Row");

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var row in rows)
			{
				var results = row.Elements("result").ToDictionary(e => e.Attribute("column")!.Value, e => e.Value.Trim());
				var service = results["TaskTypeName"];
				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, service);

				foreach (var dateString in new[] { results["NextInstance"], results["LastInstance"] })
				{
					if (string.IsNullOrWhiteSpace(dateString))
					{
						continue;
					}

					binDays.Add(new BinDay
					{
						Date = DateUtilities.ParseDateExact(dateString, "yyyy-MM-dd"),
						Address = address,
						Bins = matchedBins,
					});
				}
			}

			var getBinDaysResponse = new GetBinDaysResponse
			{
				BinDays = ProcessingUtilities.ProcessBinDays(binDays),
			};

			return getBinDaysResponse;
		}

		throw new InvalidOperationException("Invalid client-side request.");
	}

}
