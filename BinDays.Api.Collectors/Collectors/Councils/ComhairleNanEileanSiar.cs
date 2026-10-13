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
/// Collector implementation for Comhairle nan Eilean Siar.
/// </summary>
internal sealed class ComhairleNanEileanSiar : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Comhairle nan Eilean Siar";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.cne-siar.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "eilean-siar";

	/// <summary>
	/// The list of bin types for this collector. Lewis and Harris, and Uist and Barra, have different services,
	/// and bins which share a colour are told apart by a coloured sticker, so the sticker colour is used.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Purple,
			Keys = [ "NonRecycle" ],
		},
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Residual" ],
		},
		new()
		{
			Name = "Food & Garden Waste",
			Colour = BinColour.Yellow,
			Keys = [ "Organic" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Recyclable" ],
		},
		new()
		{
			Name = "Glass Recycling",
			Colour = BinColour.Green,
			Keys = [ "Glass" ],
		},
		new()
		{
			Name = "Paper & Card Recycling",
			Colour = BinColour.Green,
			Keys = [ "Paper and Card" ],
		},
		new()
		{
			Name = "Plastics & Cans Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Plastic and Tin" ],
		},
	];

	/// <summary>
	/// The base URL of the council's self-service site.
	/// </summary>
	private const string _baseUrl = "https://eileansiar-self.achieveservice.com";

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting the session
		if (clientSideResponse == null)
		{
			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = CreateSessionRequest(),
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for getting addresses
		else if (clientSideResponse.RequestId == 1)
		{
			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = CreateLookupRequest(clientSideResponse, "680794b27bd2e", "postcode_search", postcode),
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 2)
		{
			var rawAddresses = GetRows(clientSideResponse.Content);

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var rawAddress in rawAddresses)
			{
				var address = new Address
				{
					Property = rawAddress["display"].Trim(),
					Postcode = postcode,
					Uid = rawAddress["uprn"],
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
		// Prepare client-side request for getting the session
		if (clientSideResponse == null)
		{
			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = CreateSessionRequest(),
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting bin days
		else if (clientSideResponse.RequestId == 1)
		{
			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = CreateLookupRequest(clientSideResponse, "6a4e485593f14", "uprnToCheck", address.Uid!),
			};

			return getBinDaysResponse;
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 2)
		{
			var rawBinDays = GetRows(clientSideResponse.Content);

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var rawBinDay in rawBinDays)
			{
				var service = rawBinDay["CollectionName"];
				var date = DateUtilities.ParseDateExact(rawBinDay["CollectionDate"], "dd/MM/yyyy HH:mm:ss");

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

	/// <summary>
	/// Creates the client-side request for getting the session.
	/// </summary>
	private static ClientSideRequest CreateSessionRequest()
	{
		var clientSideRequest = new ClientSideRequest
		{
			RequestId = 1,
			Url = $"{_baseUrl}/authapi/isauthenticated",
			Method = "GET",
		};

		return clientSideRequest;
	}

	/// <summary>
	/// Creates the client-side request for a lookup with a single form value, using the session from the session response.
	/// </summary>
	private static ClientSideRequest CreateLookupRequest(ClientSideResponse clientSideResponse, string lookupId, string field, string value)
	{
		using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
		var sessionId = jsonDoc.RootElement.GetProperty("auth-session").GetString()!;
		var cookie = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);

		var requestBody = $$"""
		{
			"formValues": {
				"Section 1": {
					"{{field}}": { "value": "{{value}}" }
				}
			}
		}
		""";

		var clientSideRequest = new ClientSideRequest
		{
			RequestId = 2,
			Url = $"{_baseUrl}/apibroker/runLookup?id={lookupId}&sid={sessionId}",
			Method = "POST",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
				{ "content-type", Constants.ApplicationJson },
				{ "cookie", cookie },
			},
			Body = requestBody,
		};

		return clientSideRequest;
	}

	/// <summary>
	/// Gets the rows from a lookup response, each as its values by column name. The rows are read from the XML data,
	/// as it keeps the order the council lists them in.
	/// </summary>
	private static List<Dictionary<string, string>> GetRows(string content)
	{
		using var jsonDoc = JsonDocument.Parse(content);
		var xml = XDocument.Parse(jsonDoc.RootElement.GetProperty("data").GetString()!);

		var rows = new List<Dictionary<string, string>>();
		foreach (var row in xml.Descendants("Row"))
		{
			rows.Add(row.Elements("result").ToDictionary(result => result.Attribute("column")!.Value, result => result.Value));
		}

		return rows;
	}
}
