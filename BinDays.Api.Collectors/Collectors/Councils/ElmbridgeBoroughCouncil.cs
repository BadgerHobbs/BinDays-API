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
using System.Xml.Linq;

/// <summary>
/// Collector implementation for Elmbridge Borough Council.
/// </summary>
internal sealed partial class ElmbridgeBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Elmbridge Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.elmbridge.gov.uk/bins-waste-and-recycling/bin-collection-days");

	/// <inheritdoc/>
	public override string GovUkId => "elmbridge";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Keys = [ "Domestic Waste Collection Service" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Domestic Recycling Collection Service" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Green,
			Keys = [ "Food Waste Collection Service" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden Waste Collection Service" ],
		},
		new()
		{
			Name = "Clothes, Textiles and Small Electricals",
			Colour = BinColour.Grey,
			Keys = [ "Textiles and Small WEEE" ],
			Type = BinType.Bag,
		},
	];

	/// <summary>
	/// The base URL for Achieve service requests.
	/// </summary>
	private const string _baseUrl = "https://elmbridge-self.achieveservice.com";

	/// <summary>
	/// The identifier for the form used in requests.
	/// </summary>
	private const string _formId = "AF-Form-5f4b6ba5-d793-4cf1-9fb8-35b97f05a535";

	/// <summary>
	/// The name of the form used in requests.
	/// </summary>
	private const string _formName = "Your bin collection days and calendar";

	/// <summary>
	/// The identifier for the process used in requests.
	/// </summary>
	private const string _processId = "AF-Process-be7d38c3-325c-4028-95c9-140e896d3ad4";

	/// <summary>
	/// The identifier for the stage used in requests.
	/// </summary>
	private const string _stageId = "AF-Stage-6ee0ea94-54aa-4c8b-9286-08c91e00b4b0";

	/// <summary>
	/// The URI of the form used in requests.
	/// </summary>
	private const string _formUri = "sandbox-publish://AF-Process-be7d38c3-325c-4028-95c9-140e896d3ad4/AF-Stage-6ee0ea94-54aa-4c8b-9286-08c91e00b4b0/definition.json";

	/// <summary>
	/// Regex to extract the session identifier (sid) from HTML.
	/// </summary>
	[GeneratedRegex(@"sid=(?<sessionId>[a-f0-9]+)")]
	private static partial Regex SessionIdRegex();

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
			var (sessionId, cookies) = ExtractSessionData(clientSideResponse);

			var requestBody = $$"""
{
  "stopOnFailure": true,
  "usePHPIntegrations": true,
  "stage_id": "{{_stageId}}",
  "stage_name": "Your bin collection days",
  "formId": "{{_formId}}",
  "formValues": {
    "Section 1": {
      "isThereAnError": { "value": "no" },
      "lu_postcode": { "value": "{{postcode}}" },
      "postcodecheck": { "value": "false" }
    }
  },
  "isPublished": true,
  "formName": "{{_formName}}",
  "processId": "{{_processId}}",
  "formUri": "{{_formUri}}"
}
""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseUrl}/apibroker/runLookup?id=673b44613fba3&app_name=AF-Renderer::Self&sid={sessionId}",
				Method = "POST",
				Headers = new()
				{
					{ "content-type", "application/json" },
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
				var results = row.Elements("result").ToList();
				var uprn = results.First(result => result.Attribute("column")!.Value == "UPRN").Value.Trim();
				var display = results.First(result => result.Attribute("column")!.Value == "display").Value.Trim();

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
		// Prepare client-side request for getting bin days
		else if (clientSideResponse.RequestId == 1)
		{
			var (sessionId, cookies) = ExtractSessionData(clientSideResponse);

			var requestBody = $$"""
{
  "stopOnFailure": true,
  "usePHPIntegrations": true,
  "stage_id": "{{_stageId}}",
  "stage_name": "Your bin collection days",
  "formId": "{{_formId}}",
  "formValues": {
    "Section 1": {
      "isThereAnError": { "value": "no" },
      "lu_postcode": { "value": "{{address.Postcode!}}" },
      "postcodecheck": { "value": "true" },
      "osAddress": { "value": "{{address.Uid!}}" },
      "UPRN": { "value": "{{address.Uid!}}" }
    }
  },
  "isPublished": true,
  "formName": "{{_formName}}",
  "processId": "{{_processId}}",
  "formUri": "{{_formUri}}"
}
""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseUrl}/apibroker/runLookup?id=663b557cdaece&app_name=AF-Renderer::Self&sid={sessionId}",
				Method = "POST",
				Headers = new()
				{
					{ "content-type", "application/json" },
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
				var results = row.Elements("result").ToList();
				var dateString = results.First(result => result.Attribute("column")!.Value == "Date").Value.Trim();

				var services = results
					.Where(result => result.Attribute("column")!.Value.StartsWith("Service", StringComparison.OrdinalIgnoreCase))
					.Select(result => result.Value.Trim())
					.Where(result => !string.IsNullOrWhiteSpace(result));

				var date = DateOnly.ParseExact(
					dateString,
					"dd/MM/yyyy HH:mm:ss",
					CultureInfo.InvariantCulture,
					DateTimeStyles.None
				);

				var bins = new List<Bin>();
				foreach (var service in services)
				{
					var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, service);
					bins.AddRange(matchedBins);
				}

				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = [.. bins.DistinctBy(bin => bin.Name)],
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
	/// Creates the initial client-side request used to start the session.
	/// </summary>
	private static ClientSideRequest CreateInitialRequest()
	{
		var clientSideRequest = new ClientSideRequest
		{
			RequestId = 1,
			Url = $"{_baseUrl}/AchieveForms/?mode=fill&consentMessage=yes&form_uri={_formUri}&process=1&process_uri=sandbox-processes://{_processId}&process_id={_processId}",
			Method = "GET",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
			},
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
