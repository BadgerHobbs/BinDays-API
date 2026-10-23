namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

/// <summary>
/// Collector implementation for North Warwickshire Borough Council.
/// </summary>
internal sealed partial class NorthWarwickshireBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "North Warwickshire Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.northwarks.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "north-warwickshire";

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
			Colour = BinColour.Red,
			Keys = [ "Recycling" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Green,
			Keys = [ "Food Waste" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Green" ],
		},
	];

	/// <summary>
	/// The base URL for the council's Achieve Forms service.
	/// </summary>
	private const string _baseUrl = "https://nwarks-ss.achieveservice.com";

	/// <summary>
	/// The collection lookups for each service (refuse, recycling, food and garden waste), which are queried in turn.
	/// </summary>
	private static readonly string[] _collectionLookupIds = ["6964f19d080c5", "6964f19bc2e2e", "6964f19aac313", "695fc85344bb3"];

	/// <summary>
	/// Regex for runs of whitespace, as the council's addresses contain repeated spaces.
	/// </summary>
	[GeneratedRegex(@"\s+")]
	private static partial Regex WhitespaceRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting session cookies
		if (clientSideResponse == null)
		{
			var clientSideRequest = CreateInitialRequest();

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for getting addresses
		else if (clientSideResponse.RequestId == 1)
		{
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);

			var clientSideRequest = CreateLookupRequest(2, "58bd4de0ac7b8", cookies, $$"""
			{
				"postcode_search": { "value": "{{postcode}}" }
			}
			""");

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 2)
		{
			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var results in GetRows(clientSideResponse.Content))
			{
				var address = new Address
				{
					Property = WhitespaceRegex().Replace(results["display"], " ").Trim(),
					Postcode = postcode,
					Uid = results["uprn"].Trim(),
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
		// Prepare client-side request for getting session cookies
		if (clientSideResponse == null)
		{
			var clientSideRequest = CreateInitialRequest();

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting the Bartec authentication token
		else if (clientSideResponse.RequestId == 1)
		{
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);

			var clientSideRequest = CreateLookupRequest(2, "695fc5d469d65", cookies, """
			{
				"testOrLive": { "value": "Live" }
			}
			""");

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side requests for each service's collections in turn, collecting the rows as they are returned
		else if (clientSideResponse.RequestId >= 2 && clientSideResponse.RequestId <= _collectionLookupIds.Length + 1)
		{
			var cookies = clientSideResponse.Options.Metadata["cookie"];

			string token;
			string collections;

			if (clientSideResponse.RequestId == 2)
			{
				token = GetRows(clientSideResponse.Content).Single()["AuthenticateResponse"];
				collections = string.Empty;
			}
			else
			{
				token = clientSideResponse.Options.Metadata["token"];
				collections = clientSideResponse.Options.Metadata["collections"] + GetCollections(clientSideResponse.Content);
			}

			var today = DateOnly.FromDateTime(DateTime.Today);

			var clientSideRequest = CreateLookupRequest(clientSideResponse.RequestId + 1, _collectionLookupIds[clientSideResponse.RequestId - 2], cookies, $$"""
			{
				"uprn": { "value": "{{address.Uid}}" },
				"AuthenticateResponse": { "value": "{{token}}" },
				"dateTodayFormatted": { "value": "{{today:yyyy-MM-dd}}" },
				"date4WeeksFormatted": { "value": "{{today.AddDays(80):yyyy-MM-dd}}" }
			}
			""");

			clientSideRequest.Options.Metadata.Add("token", token);
			clientSideRequest.Options.Metadata.Add("collections", collections);

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin days from the collected rows
		else if (clientSideResponse.RequestId == _collectionLookupIds.Length + 2)
		{
			var collections = clientSideResponse.Options.Metadata["collections"] + GetCollections(clientSideResponse.Content);

			// Iterate through each collection, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var collection in collections.Split('\n', StringSplitOptions.RemoveEmptyEntries))
			{
				var parts = collection.Split('|');

				var binDay = new BinDay
				{
					Date = DateUtilities.ParseDateExact(parts[1], "dd/MM/yyyy"),
					Address = address,
					Bins = ProcessingUtilities.GetMatchingBins(_binTypes, parts[0]),
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
	/// Creates the initial client-side request, which opens the form to start a session.
	/// </summary>
	private static ClientSideRequest CreateInitialRequest()
	{
		var clientSideRequest = new ClientSideRequest
		{
			RequestId = 1,
			Url = $"{_baseUrl}/service/Waste_Collection_Calendar",
			Method = "GET",
		};

		return clientSideRequest;
	}

	/// <summary>
	/// Creates a client-side request for an Achieve Forms lookup, keeping the session cookies in the metadata.
	/// </summary>
	private static ClientSideRequest CreateLookupRequest(int requestId, string lookupId, string cookies, string fields)
	{
		var clientSideRequest = new ClientSideRequest
		{
			RequestId = requestId,
			Url = $"{_baseUrl}/apibroker/runLookup?id={lookupId}",
			Method = "POST",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
				{ "content-type", Constants.ApplicationJson },
				{ "cookie", cookies },
			},
			Body = $$"""
			{
				"formValues": {
					"Collection Details": {{fields}}
				}
			}
			""",
			Options = new ClientSideOptions
			{
				Metadata =
				{
					{ "cookie", cookies },
				},
			},
		};

		return clientSideRequest;
	}

	/// <summary>
	/// Gets the rows from a lookup response as dictionaries of column values.
	/// </summary>
	private static List<Dictionary<string, string>> GetRows(string content)
	{
		using var jsonDoc = JsonDocument.Parse(content);
		var xmlData = jsonDoc.RootElement.GetProperty("data").GetString()!;

		return [.. XDocument.Parse(xmlData)
			.Descendants("Row")
			.Select(row => row.Elements("result").ToDictionary(result => result.Attribute("column")!.Value, result => result.Value))];
	}

	/// <summary>
	/// Gets the collections from a collection lookup response, as "job name|date" lines.
	/// </summary>
	private static string GetCollections(string content)
	{
		var collections = string.Empty;
		foreach (var results in GetRows(content))
		{
			collections += $"{results["JobName"].Trim()}|{results["Date"].Trim()}\n";
		}

		return collections;
	}
}
