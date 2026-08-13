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
/// Collector implementation for City of Lincoln Council.
/// </summary>
internal sealed partial class CityOfLincolnCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "City of Lincoln Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.lincoln.gov.uk/bins-recycling");

	/// <inheritdoc/>
	public override string GovUkId => "lincoln";

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
			Name = "Recycling",
			Colour = BinColour.Brown,
			Keys = [ "Recycling" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Orange,
			Keys = [ "Food" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Garden" ],
		},
	];

	/// <summary>
	/// The base URL for Achieve service requests.
	/// </summary>
	private const string _baseUrl = "https://contact.lincoln.gov.uk";

	/// <summary>
	/// The identifier for the process used in requests.
	/// </summary>
	private const string _processId = "AF-Process-d75f39bf-afe9-41b6-be0f-f8038f8e2f20";

	/// <summary>
	/// The URI of the form used in requests.
	/// </summary>
	private const string _formUri = $"sandbox-publish://{_processId}/AF-Stage-15741955-491e-4cc5-97c2-82c09dfbc5c6/definition.json";

	/// <summary>
	/// Regex to extract the session identifier (sid) from HTML.
	/// </summary>
	[GeneratedRegex(@"sid=(?<sessionId>[a-f0-9]+)")]
	private static partial Regex SessionIdRegex();

	/// <summary>
	/// Regex to extract the bin name and collection date from the entries HTML fragment.
	/// </summary>
	[GeneratedRegex(@"<strong>(?<name>[^<]+)</strong>\s*</td>\s*<tr>\s*<td[^>]*>\s*<strong>Collection On:\s*</strong>(?<date>[^<]+)<p")]
	private static partial Regex BinDaysRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting session id
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
			var (sessionId, cookies) = ExtractSessionData(clientSideResponse);

			var requestBody = $$"""
			{
				"formValues": {
					"Section 1": {
						"street_search": { "value": "{{postcode}}" }
					}
				}
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseUrl}/apibroker/runLookup?id=69b7cf48e09e0&app_name=AF-Renderer::Self&sid={sessionId}",
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
				var results = row.Elements("result").ToDictionary(result => result.Attribute("column")!.Value, result => result.Value);
				var uprn = results["name"].Trim();
				var display = results["display"].Trim();

				var address = new Address
				{
					Property = display,
					Postcode = postcode,
					Uid = uprn,
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
		// Prepare client-side request for getting session id
		if (clientSideResponse == null)
		{
			var clientSideRequest = CreateInitialRequest();

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting bin days
		else if (clientSideResponse.RequestId == 1)
		{
			var (sessionId, cookies) = ExtractSessionData(clientSideResponse);

			var today = DateTime.Today;
			var fromDate = today.ToString("yyyy-MM-dd");
			var toDate = today.AddDays(60).ToString("yyyy-MM-dd");

			var requestBody = $$"""
			{
				"formValues": {
					"Section 1": {
						"uprndisplay": { "value": "{{address.Uid}}" },
						"fromdate": { "value": "{{fromDate}}" },
						"todate": { "value": "{{toDate}}" }
					}
				}
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseUrl}/apibroker/runLookup?id=69372c3785443&app_name=AF-Renderer::Self&sid={sessionId}",
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
			var entriesHtml = xml.Descendants("result")
				.FirstOrDefault(result => result.Attribute("column")!.Value == "entries")?.Value ?? string.Empty;

			var rawBinDays = BinDaysRegex().Matches(entriesHtml)!;

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var service = rawBinDay.Groups["name"].Value.Trim();
				var dateString = rawBinDay.Groups["date"].Value.Trim();

				var date = DateUtilities.ParseDateExact(dateString, "dddd d MMMM yyyy");

				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, service);

				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = [.. matchedBins],
				};

				binDays.Add(binDay);
			}

			var getBinDaysResponse = new GetBinDaysResponse
			{
				BinDays = ProcessingUtilities.ProcessBinDays(binDays),
			};

			return getBinDaysResponse;
		}

		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <summary>
	/// Creates the initial client-side request used to obtain a session id.
	/// </summary>
	private static ClientSideRequest CreateInitialRequest()
	{
		var clientSideRequest = new ClientSideRequest
		{
			RequestId = 1,
			Url = $"{_baseUrl}/AchieveForms/?mode=fill&consentMessage=yes&form_uri={_formUri}&process=1&process_uri=sandbox-processes://{_processId}&process_id={_processId}",
			Method = "GET",
		};

		return clientSideRequest;
	}

	/// <summary>
	/// Extracts the session identifier and cookies from the client-side response.
	/// </summary>
	private static (string SessionId, string Cookies) ExtractSessionData(ClientSideResponse clientSideResponse)
	{
		var setCookieHeader = clientSideResponse.Headers["set-cookie"];
		var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

		var sessionId = SessionIdRegex().Match(clientSideResponse.Content).Groups["sessionId"].Value;

		return (sessionId, cookies);
	}
}
