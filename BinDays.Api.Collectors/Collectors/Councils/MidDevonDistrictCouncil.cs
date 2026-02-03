namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Mid Devon District Council.
/// </summary>
internal sealed partial class MidDevonDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Mid Devon District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.middevon.gov.uk/do-it-online/waste-and-recycling/collection-day-lookup/");

	/// <inheritdoc/>
	public override string GovUkId => "mid-devon";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Residual" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Blue,
			Keys = [ "Food Caddy" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden Waste" ],
		},
		new()
		{
			Name = "Glass, Paper, Tins & Plastics Recycling",
			Colour = BinColour.Black,
			Keys = [ "Black Recycling Box" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Cardboard & Cartons Recycling",
			Colour = BinColour.Green,
			Keys = [ "Green Recycling Box" ],
			Type = BinType.Box,
		},
	];

	/// <summary>
	/// The form URL for the collection day lookup.
	/// </summary>
	private static readonly Uri _formUrl = new("https://my.middevon.gov.uk/en/AchieveForms/?form_uri=sandbox-publish://AF-Process-2289dd06-9a12-4202-ba09-857fe756f6bd/AF-Stage-eb382015-001c-415d-beda-84f796dbb167/definition.json&redirectlink=%2Fen&cancelRedirectLink=%2Fen&consentMessage=yes");

	/// <summary>
	/// Regex to extract the sid value from the HTML content.
	/// </summary>
	[GeneratedRegex(@"sid=(?<sid>[a-f0-9]+)")]
	private static partial Regex SidRegex();

	/// <summary>
	/// Regex to extract the PHP session ID from the cookie string.
	/// </summary>
	[GeneratedRegex(@"PHPSESSID=(?<sessionId>[^;]+)")]
	private static partial Regex SessionIdRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting form
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _formUrl.ToString(),
				Method = "GET",
				Headers = new()
				{
					{ "User-Agent", Constants.UserAgent },
					{ "cookie", "CookiesAccepted=true" },
				},
			};

			var response = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return response;
		}
		// Get reference token
		else if (clientSideResponse.RequestId == 1)
		{
			clientSideResponse.Headers.TryGetValue("set-cookie", out var setCookieHeader);
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader!);
			var metadata = new Dictionary<string, string>
			{
				{ "cookie", cookies },
				{ "sid", SidRegex().Match(clientSideResponse.Content).Groups["sid"].Value },
				{ "sessionId", SessionIdRegex().Match(cookies).Groups["sessionId"].Value },
			};

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"https://my.middevon.gov.uk/api/nextref?_={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}&sid={metadata["sid"]}",
				Method = "GET",
				Headers = new()
				{
					{ "X-Requested-With", "XMLHttpRequest" },
					{ "cookie", metadata["cookie"] },
					{ "User-Agent", Constants.UserAgent },
				},
				Options = new ClientSideOptions { Metadata = metadata },
			};

			var response = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return response;
		}
		// Run address lookup
		else if (clientSideResponse.RequestId == 2)
		{
			var metadata = new Dictionary<string, string>(clientSideResponse.Options.Metadata);

			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			metadata["reference"] = jsonDoc.RootElement.GetProperty("data").GetProperty("reference").GetString()!;
			metadata["csrf_token"] = jsonDoc.RootElement.GetProperty("data").GetProperty("csrfToken").GetString()!;

			var payload = BuildLookupPayload(
				postcode,
				metadata["sessionId"],
				metadata["csrf_token"],
				metadata["reference"]
			);

			var clientSideRequest = CreateLookupRequest(3, "64c24c3e7f5bb", payload, metadata);

			var response = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return response;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 3)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var rows = jsonDoc.RootElement.GetProperty("integration").GetProperty("transformed").GetProperty("rows_data");

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var property in rows.EnumerateObject())
			{
				var data = property.Value;

				var address = new Address
				{
					Property = data.GetProperty("display").GetString()!,
					Postcode = postcode,
					Uid = data.GetProperty("uprn").GetString()!,
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
		// Prepare client-side request for getting form
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _formUrl.ToString(),
				Method = "GET",
				Headers = new()
				{
					{ "User-Agent", Constants.UserAgent },
					{ "cookie", "CookiesAccepted=true" },
				},
			};

			var response = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return response;
		}
		// Get reference token
		else if (clientSideResponse.RequestId == 1)
		{
			clientSideResponse.Headers.TryGetValue("set-cookie", out var setCookieHeader);
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader!);
			var metadata = new Dictionary<string, string>
			{
				{ "cookie", cookies },
				{ "sid", SidRegex().Match(clientSideResponse.Content).Groups["sid"].Value },
				{ "sessionId", SessionIdRegex().Match(cookies).Groups["sessionId"].Value },
			};

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"https://my.middevon.gov.uk/api/nextref?_={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}&sid={metadata["sid"]}",
				Method = "GET",
				Headers = new()
				{
					{ "X-Requested-With", "XMLHttpRequest" },
					{ "cookie", metadata["cookie"] },
					{ "User-Agent", Constants.UserAgent },
				},
				Options = new ClientSideOptions { Metadata = metadata },
			};

			var response = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return response;
		}
		// Get organic waste collections
		else if (clientSideResponse.RequestId == 2)
		{
			var metadata = new Dictionary<string, string>(clientSideResponse.Options.Metadata);

			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			metadata["reference"] = jsonDoc.RootElement.GetProperty("data").GetProperty("reference").GetString()!;
			metadata["csrf_token"] = jsonDoc.RootElement.GetProperty("data").GetProperty("csrfToken").GetString()!;

			var payload = BuildLookupPayload(
				address.Postcode!,
				metadata["sessionId"],
				metadata["csrf_token"],
				metadata["reference"],
				address.Uid!
			);

			var clientSideRequest = CreateLookupRequest(3, "641c7ae9b4c96", payload, metadata);

			var response = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return response;
		}
		// Get residual waste collections and prepare for recycling lookup
		else if (clientSideResponse.RequestId == 3)
		{
			var metadata = new Dictionary<string, string>(clientSideResponse.Options.Metadata);

			var (organicDates, organicItems) = ParseCollectionRows(clientSideResponse);
			metadata["dates"] = JsonSerializer.Serialize(organicDates);
			metadata["items"] = JsonSerializer.Serialize(organicItems);

			var payload = BuildLookupPayload(
				address.Postcode!,
				metadata["sessionId"],
				metadata["csrf_token"],
				metadata["reference"],
				address.Uid!,
				organicDates.First()
			);

			var clientSideRequest = CreateLookupRequest(4, "6423144f50ec0", payload, metadata);

			var response = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return response;
		}
		// Get recycling collections and prepare final lookup
		else if (clientSideResponse.RequestId == 4)
		{
			var metadata = new Dictionary<string, string>(clientSideResponse.Options.Metadata);

			var (residualDates, residualItems) = ParseCollectionRows(clientSideResponse);
			var dates = JsonSerializer.Deserialize<List<string>>(metadata["dates"])!;
			var items = JsonSerializer.Deserialize<List<string>>(metadata["items"])!;

			dates.AddRange(residualDates);
			items.AddRange(residualItems);

			metadata["dates"] = JsonSerializer.Serialize(dates);
			metadata["items"] = JsonSerializer.Serialize(items);

			var payload = BuildLookupPayload(
				address.Postcode!,
				metadata["sessionId"],
				metadata["csrf_token"],
				metadata["reference"],
				address.Uid!,
				dates[0],
				residualDates.First(),
				dates[0]
			);

			var clientSideRequest = CreateLookupRequest(5, "642315aacb919", payload, metadata);

			var response = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return response;
		}
		// Process all bin days from response
		else if (clientSideResponse.RequestId == 5)
		{
			var metadata = clientSideResponse.Options.Metadata;

			var (recyclingDates, recyclingItems) = ParseCollectionRows(clientSideResponse);
			var dates = JsonSerializer.Deserialize<List<string>>(metadata["dates"])!;
			var items = JsonSerializer.Deserialize<List<string>>(metadata["items"])!;

			dates.AddRange(recyclingDates);
			items.AddRange(recyclingItems);

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			for (var i = 0; i < dates.Count; i++)
			{
				var binDay = new BinDay
				{
					Date = DateOnly.ParseExact(
						dates[i],
						"dd-MMM-yy",
						CultureInfo.InvariantCulture,
						DateTimeStyles.None
					),
					Address = address,
					Bins = ProcessingUtilities.GetMatchingBins(_binTypes, items[i]),
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

	/// <summary>
	/// Parses collection data from the JSON response.
	/// </summary>
	private static (List<string> Dates, List<string> Items) ParseCollectionRows(ClientSideResponse clientSideResponse)
	{
		using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
		var rows = jsonDoc.RootElement.GetProperty("integration").GetProperty("transformed").GetProperty("rows_data");

		var dates = new List<string>();
		var items = new List<string>();

		foreach (var property in rows.EnumerateObject())
		{
			dates.Add(property.Value.GetProperty("display").GetString()!);
			items.Add(property.Value.GetProperty("CollectionItems").GetString()!);
		}

		return (dates, items);
	}

	/// <summary>
	/// Creates a client-side request for the lookup API.
	/// </summary>
	private static ClientSideRequest CreateLookupRequest(
		int requestId,
		string lookupId,
		string payload,
		Dictionary<string, string> metadata)
	{
		return new ClientSideRequest
		{
			RequestId = requestId,
			Url = $"https://my.middevon.gov.uk/apibroker/runLookup?id={lookupId}&repeat_against=&noRetry=false&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&_={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}&sid={metadata["sid"]}",
			Method = "POST",
			Headers = new()
			{
				{ "Content-Type", "application/json" },
				{ "X-Requested-With", "XMLHttpRequest" },
				{ "cookie", metadata["cookie"] },
				{ "User-Agent", Constants.UserAgent },
				{ "Referer", _formUrl.ToString() },
			},
			Body = payload,
			Options = new ClientSideOptions { Metadata = metadata },
		};
	}

	/// <summary>
	/// Builds the JSON payload for the AchieveForms API lookup request.
	/// </summary>
	private static string BuildLookupPayload(
		string postcode,
		string sessionId,
		string csrfToken,
		string reference,
		string addressUid = "",
		string organicValue = "",
		string residualValue = "",
		string foodValue = "",
		string recyclingValue = "")
	{
		return $$"""
		{
			"formId": "AF-Form-dc8ffbd6-4832-443b-ba3f-5e8d36bf11d4",
			"stage_id": "AF-Stage-eb382015-001c-415d-beda-84f796dbb167",
			"processId": "AF-Process-2289dd06-9a12-4202-ba09-857fe756f6bd",
			"formUri": "sandbox-publish://AF-Process-2289dd06-9a12-4202-ba09-857fe756f6bd/AF-Stage-eb382015-001c-415d-beda-84f796dbb167/definition.json",
			"reference": "{{reference}}",
			"tokens": {
				"session_id": "{{sessionId}}",
				"csrf_token": "{{csrfToken}}",
				"reference": "{{reference}}"
			},
			"formValues": {
				"Your Address": {
					"postcode_search": {
						"id": "AF-Field-7ad337fc-1c73-4658-8c4a-4de6079621b2",
						"value": "{{postcode}}"
					},
					"listAddress": {
						"id": "AF-Field-c2bab0b9-acaa-46a0-a758-8f87e542c71e",
						"value": "{{addressUid}}"
					},
					"OrganicCollections": {
						"id": "AF-Field-9f49b399-86a3-45e2-a670-e3719a9b8d75",
						"value": "{{organicValue}}"
					},
					"ResidualCollections": {
						"id": "AF-Field-12b20af8-6f0d-4b04-9c8d-84b9f5493906",
						"value": "{{residualValue}}"
					},
					"foodCollections": {
						"id": "AF-Field-257ea732-3255-4a20-8662-dca10c96e6da",
						"value": "{{foodValue}}"
					},
					"RecyclingCollections": {
						"id": "AF-Field-b2104a22-bba4-4f78-bf62-ccddc2a0bb48",
						"value": "{{recyclingValue}}"
					}
				}
			}
		}
		""";
	}
