namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

/// <summary>
/// Collector implementation for Reigate and Banstead Borough Council.
/// </summary>
internal sealed partial class ReigateAndBansteadBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Reigate and Banstead Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.reigate-banstead.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "reigate-and-banstead";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Green,
			Keys = [ "Food waste" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Paper and Cardboard Recycling",
			Colour = BinColour.Black,
			Keys = [ "Paper and cardboard" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Grey,
			Keys = [ "Mixed recycling" ],
		},
		new()
		{
			Name = "Refuse",
			Colour = BinColour.Green,
			Keys = [ "Refuse" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden waste" ],
		},
	];

	private const string _baseUrl = "https://my.reigate-banstead.gov.uk";
	private const string _pageUrl = "https://my.reigate-banstead.gov.uk/en/service/Bins_and_recycling___collections_calendar";
	private const string _stageId = "AF-Stage-c13e8566-77fe-41f9-ba05-91d8e0c171cd";
	private const string _processId = "AF-Process-27e2fa40-1ba6-42a4-974e-f9fffea61585";
	private const string _addressFormId = "AF-Form-377b1265-2166-4ead-a498-899d80a21d73";
	private const string _mainFormId = "AF-Form-7ea2eba0-028f-40f7-8eeb-c0d13ef21e48";
	private const string _stageName = "Bin calendar";
	private const string _addressFormName = "Sub - Address LLPG - no free text";
	private const string _mainFormName = "Bins and recycling - collections calendar";
	private const string _formUri = "sandbox-publish://AF-Process-27e2fa40-1ba6-42a4-974e-f9fffea61585/AF-Stage-c13e8566-77fe-41f9-ba05-91d8e0c171cd/definition.json";

	/// <summary>
	/// Regex to extract the session identifier from the HTML.
	/// </summary>
	[GeneratedRegex(@"sid=(?<sessionId>[a-f0-9]+)")]
	private static partial Regex SessionIdRegex();

	/// <summary>
	/// Regex to extract bin day sections from the HTML response.
	/// </summary>
	[GeneratedRegex("<h3[^>]*>\\s*(?<date>[^<]+)\\s*</h3>(?<content>.*?)(?=<h3[^>]*>|\\z)", RegexOptions.Singleline)]
	private static partial Regex BinDaySectionRegex();

	/// <summary>
	/// Regex to extract bin names from the HTML response.
	/// </summary>
	[GeneratedRegex("<span[^>]*>\\s*(?<bin>[^<]+)\\s*</span>", RegexOptions.Singleline)]
	private static partial Regex BinNameRegex();

	/// <summary>
	/// Regex to extract the PHP session identifier from cookies.
	/// </summary>
	[GeneratedRegex(@"PHPSESSID=(?<phpSessionId>[^;]+)")]
	private static partial Regex PhpSessionRegex();

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
		// Prepare client-side request for address lookup
		else if (clientSideResponse.RequestId == 1)
		{
			var (sessionId, cookies) = ExtractSessionData(clientSideResponse);
			var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

			var requestBody = $$"""
			{
				"stopOnFailure": true,
				"usePHPIntegrations": true,
				"stage_id": "{{_stageId}}",
				"stage_name": "{{_stageName}}",
				"formId": "{{_addressFormId}}",
				"formValues": {
					"Section 1": {
						"processName": { "value": "bin calendar" },
						"productName": { "value": "Self" },
						"useDefaultAddress": { "value": "true" },
						"postcode_search": { "value": "{{postcode}}" }
					}
				},
				"isPublished": true,
				"formName": "{{_addressFormName}}",
				"processId": "{{_processId}}",
				"formUri": "{{_formUri}}"
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseUrl}/apibroker/runLookup?id=592fe826d4611&repeat_against=&noRetry=false&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&_={timestamp}&sid={sessionId}",
				Method = "POST",
				Headers = new()
				{
					{ "content-type", "application/json" },
					{ "cookie", cookies },
					{ "user-agent", Constants.UserAgent },
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
				var values = row.Elements("result").ToDictionary(
					element => element.Attribute("column")!.Value,
					element => element.Value.Trim()
				);

				var uprn = values["uprn"];
				var house = values["house"];
				var street = values["street"];
				var town = values["town"];
				var county = values["county"];
				var ward = values["ward"];
				var latitude = values["lat"];
				var longitude = values["lng"];
				var usrn = values["usrn"];

				var address = new Address
				{
					Property = values["display"],
					Postcode = postcode,
					// Uid format: uprn;house;street;town;county;postcode;ward;latitude;longitude;usrn
					Uid = $"{uprn};{house};{street};{town};{county};{values["postcode"]};{ward};{latitude};{longitude};{usrn}",
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
		// Prepare client-side request for property details lookup
		else if (clientSideResponse.RequestId == 1)
		{
			var (sessionId, cookies) = ExtractSessionData(clientSideResponse);
			var phpSessionId = PhpSessionRegex().Match(cookies).Groups["phpSessionId"].Value;

			// Uid format: uprn;house;street;town;county;postcode;ward;latitude;longitude;usrn
			var uidParts = address.Uid!.Split(';');
			var uprn = uidParts[0];
			var house = uidParts[1];
			var street = uidParts[2];
			var town = uidParts[3];
			var county = uidParts[4];
			var postcode = uidParts[5];
			var wardCode = uidParts[6];
			var latitude = uidParts[7];
			var longitude = uidParts[8];
			var usrn = uidParts[9];

			var postcodeSector = postcode.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) switch
			{
				{ Length: > 1 } parts => $"{parts[0]} {parts[1][0]}",
				_ => postcode,
			};

			var metadata = new Dictionary<string, string>
			{
				{ "sid", sessionId },
				{ "cookies", cookies },
				{ "phpSession", phpSessionId },
				{ "pageUrl", _pageUrl },
				{ "uprn", uprn },
				{ "house", house },
				{ "street", street },
				{ "town", town },
				{ "county", county },
				{ "postcode", postcode },
				{ "wardCode", wardCode },
				{ "latitude", latitude },
				{ "longitude", longitude },
				{ "usrn", usrn },
				{ "postcodeSector", postcodeSector },
				{ "lengthOfPostcode", postcode.Length.ToString(CultureInfo.InvariantCulture) },
				{ "lengthMinusTwo", (postcode.Length - 2).ToString(CultureInfo.InvariantCulture) },
				{ "wardShort", wardCode },
				{ "wardName", string.Empty },
				{ "tokenString", string.Empty },
			};

			var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseUrl}/api/nextref?_={timestamp}&sid={sessionId}",
				Method = "GET",
				Headers = new()
				{
					{ "cookie", cookies },
					{ "user-agent", Constants.UserAgent },
					{ "x-requested-with", "XMLHttpRequest" },
				},
				Options = new ClientSideOptions { Metadata = metadata },
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for property details lookup
		else if (clientSideResponse.RequestId == 2)
		{
			var metadata = new Dictionary<string, string>(clientSideResponse.Options.Metadata);

			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			metadata["reference"] = jsonDoc.RootElement.GetProperty("data").GetProperty("reference").GetString()!;
			metadata["csrfToken"] = jsonDoc.RootElement.GetProperty("data").GetProperty("csrfToken").GetString()!;

			var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			var tokenSections = BuildTokenSections(metadata, _mainFormId, _mainFormName);
			var tokenRequestBody = $$"""
			{
				"stopOnFailure": true,
				"usePHPIntegrations": true,
				"stage_id": "{{_stageId}}",
				"stage_name": "{{_stageName}}",
				"formId": "{{_mainFormId}}",
				"formValues": {
					"Section 1": {
						"process": { "value": "bin calendar" }
					}
				},
				"isPublished": true,
				"formName": "{{_mainFormName}}",
				"processId": "{{_processId}}",
				"formUri": "{{_formUri}}",
				{{tokenSections}}
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = $"{_baseUrl}/apibroker/runLookup?id=595ce0f243541&repeat_against=&noRetry=true&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&_={timestamp}&sid={metadata["sid"]}",
				Method = "POST",
				Headers = new()
				{
					{ "content-type", "application/json" },
					{ "cookie", metadata["cookies"] },
					{ "user-agent", Constants.UserAgent },
				},
				Body = tokenRequestBody,
				Options = new ClientSideOptions { Metadata = metadata },
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for property details lookup
		else if (clientSideResponse.RequestId == 3)
		{
			var metadata = new Dictionary<string, string>(clientSideResponse.Options.Metadata);

			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var xmlData = jsonDoc.RootElement.GetProperty("data").GetString()!;

			var xml = XDocument.Parse(xmlData);
			var token = xml
				.Descendants("result")
				.First(element => element.Attribute("column")!.Value.Equals("Token", StringComparison.OrdinalIgnoreCase))
				.Value
				.Trim();

			metadata["tokenString"] = token;

			var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 4,
				Url = $"{_baseUrl}/apibroker/runLookup?id=5e8dd83f68ac0&repeat_against=&noRetry=true&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&_={timestamp}&sid={metadata["sid"]}",
				Method = "POST",
				Headers = new()
				{
					{ "content-type", "application/json" },
					{ "cookie", metadata["cookies"] },
					{ "user-agent", Constants.UserAgent },
				},
				Body = BuildPropertyDetailsBody(metadata),
				Options = new ClientSideOptions { Metadata = metadata },
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for collection week lookup
		else if (clientSideResponse.RequestId == 4)
		{
			var metadata = new Dictionary<string, string>(clientSideResponse.Options.Metadata);

			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var rows = jsonDoc.RootElement.GetProperty("integration").GetProperty("transformed").GetProperty("rows_data");
			var row = rows.EnumerateObject().First().Value;

			metadata["wardShort"] = row.GetProperty("ward").GetString()!.Trim();
			metadata["wardName"] = row.GetProperty("Wardname").GetString()!.Trim();

			var requestBody = BuildScheduleRequestBody(
				metadata,
				string.Empty
			);

			var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 5,
				Url = $"{_baseUrl}/apibroker/runLookup?id=5dd6aef5b68f9&repeat_against=&noRetry=true&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&_={timestamp}&sid={metadata["sid"]}",
				Method = "POST",
				Headers = new()
				{
					{ "content-type", "application/json" },
					{ "cookie", metadata["cookies"] },
					{ "user-agent", Constants.UserAgent },
				},
				Body = requestBody,
				Options = new ClientSideOptions { Metadata = metadata },
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for bin calendar lookup
		else if (clientSideResponse.RequestId == 5)
		{
			var metadata = new Dictionary<string, string>(clientSideResponse.Options.Metadata);

			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var rows = jsonDoc.RootElement.GetProperty("integration").GetProperty("transformed").GetProperty("rows_data");
			var row = rows.EnumerateObject().First().Value;

			metadata["collectionWeek"] = row.GetProperty("collectionWeek").GetString()!.Trim();

			var requestBody = BuildScheduleRequestBody(
				metadata,
				metadata["collectionWeek"]
			);

			var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 6,
				Url = $"{_baseUrl}/apibroker/runLookup?id=609d41ca89251&repeat_against=&noRetry=true&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&_={timestamp}&sid={metadata["sid"]}",
				Method = "POST",
				Headers = new()
				{
					{ "content-type", "application/json" },
					{ "cookie", metadata["cookies"] },
					{ "user-agent", Constants.UserAgent },
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
		else if (clientSideResponse.RequestId == 6)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var xmlData = jsonDoc.RootElement.GetProperty("data").GetString()!;

			var xml = XDocument.Parse(xmlData);
			var htmlEncoded = xml.Descendants("result").First().Value;
			var decodedHtml = WebUtility.HtmlDecode(htmlEncoded);

			var sections = BinDaySectionRegex().Matches(decodedHtml)!;

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match section in sections)
			{
				var dateString = section.Groups["date"].Value.Trim();
				var binsContent = section.Groups["content"].Value;

				var binNames = BinNameRegex().Matches(binsContent)!.Select(match => match.Groups["bin"].Value.Trim()).ToList();

				var date = DateOnly.ParseExact(
					dateString,
					"dddd d MMMM yyyy",
					CultureInfo.InvariantCulture,
					DateTimeStyles.None
				);

				var matchedBins = binNames.SelectMany(binName => ProcessingUtilities.GetMatchingBins(_binTypes, binName)).DistinctBy(bin => bin.Name).ToList();

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
	/// Creates the initial client-side request for the bin calendar form.
	/// </summary>
	private static ClientSideRequest CreateInitialRequest()
	{
		var clientSideRequest = new ClientSideRequest
		{
			RequestId = 1,
			Url = _pageUrl,
			Method = "GET",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
			},
		};

		return clientSideRequest;
	}

	/// <summary>
	/// Builds the request body for the property details lookup.
	/// </summary>
	private static string BuildPropertyDetailsBody(Dictionary<string, string> metadata)
	{
		var house = metadata["house"];
		var street = metadata["street"];
		var town = metadata["town"];
		var county = metadata["county"];
		var postcode = metadata["postcode"];
		var uprn = metadata["uprn"];
		var wardCode = metadata["wardCode"];
		var latitude = metadata["latitude"];
		var longitude = metadata["longitude"];
		var usrn = metadata["usrn"];
		var postcodeSector = metadata["postcodeSector"];
		var tokenSections = BuildTokenSections(metadata, _addressFormId, _mainFormName);

		var propertyDetailsBody = $$"""
		{
			"stopOnFailure": true,
			"usePHPIntegrations": true,
			"stage_id": "{{_stageId}}",
			"stage_name": "{{_stageName}}",
			"formId": "{{_addressFormId}}",
			"formValues": {
				"Section 1": {
					"processName": { "value": "bin calendar" },
					"productName": { "value": "Self" },
					"useDefaultAddress": { "value": "true" },
					"informationCorrectLabel": { "value": "Is this information correct?" },
					"postcode_search": { "value": "{{postcode}}" },
					"chooseAddress": { "value": "{{uprn}}" },
					"txtListHouseNumber": { "value": "{{house}}" },
					"txtListStreet": { "value": "{{street}}" },
					"txtListTown": { "value": "{{town}}" },
					"txtListCounty": { "value": "{{county}}" },
					"txtListPostcode": { "value": "{{postcode}}" },
					"txtUprn": { "value": "{{uprn}}" },
					"txtWard": { "value": "{{wardCode}}" },
					"txtlatitude": { "value": "{{latitude}}" },
					"txtlongitude": { "value": "{{longitude}}" },
					"txtUsrn": { "value": "{{usrn}}" },
					"calcHouseNumber": { "value": "{{house}}" },
					"calcStreet": { "value": "{{street}}" },
					"calcTown": { "value": "{{town}}" },
					"calcWard": { "value": "{{wardCode}}" },
					"calcCounty": { "value": "{{county}}" },
					"calcPostcode": { "value": "{{postcode}}" },
					"txtTheHouse1": { "value": "{{house}}" },
					"txtTheStreet1": { "value": "{{street}}" },
					"txtTheTown1": { "value": "{{town}}" },
					"txtTheCounty1": { "value": "{{county}}" },
					"txtThePostcode1": { "value": "{{postcode}}" },
					"txtTheUprn1": { "value": "{{uprn}}" },
					"noPopuprn": { "value": "{{uprn}}" },
					"txtTheWard1": { "value": "{{wardCode}}" },
					"checkSpacesInPostcode": { "value": "true" },
					"lengthOfPostcode": { "value": "{{postcode.Length}}" },
					"lengthMinus2": { "value": "{{postcode.Length - 2}}" },
					"uniformUprn": { "value": "{{uprn}}" },
					"postcodeSector": { "value": "{{postcodeSector}}" }
				}
			},
			"isPublished": true,
			"formName": "{{_addressFormName}}",
			"processId": "{{_processId}}",
			"formUri": "{{_formUri}}",
			{{tokenSections}}
		}
		""";

		return propertyDetailsBody;
	}

	/// <summary>
	/// Builds the token and metadata sections required by the Achieve service.
	/// </summary>
	private static string BuildTokenSections(
		Dictionary<string, string> metadata,
		string formId,
		string formName
	)
	{
		var pageUrl = metadata["pageUrl"];
		var pageUri = new Uri(pageUrl);
		var encodedSite = Convert.ToBase64String(Encoding.UTF8.GetBytes(pageUrl));
		var created = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
		var reference = metadata["reference"];
		var csrfToken = metadata["csrfToken"];
		var phpSession = metadata["phpSession"];

		var tokenSections = $$"""
		"tokens": {
			"port": "",
			"host": "{{pageUri.Host}}",
			"site_url": "{{pageUrl}}",
			"site_path": "{{pageUri.AbsolutePath}}",
			"site_origin": "{{pageUri.GetLeftPart(UriPartial.Authority)}}",
			"user_agent": "{{Constants.UserAgent}}",
			"site_protocol": "{{pageUri.Scheme}}",
			"session_id": "{{phpSession}}",
			"product": "Self",
			"formLanguage": "en",
			"isAuthenticated": false,
			"api_url": "{{_baseUrl}}/apibroker/",
			"transactionReference": "",
			"transaction_status": "",
			"published": true,
			"sectionLength": 1,
			"formUri": "sandbox://{{formId}}",
			"publishUri": "{{_formUri}}",
			"formId": "{{formId}}",
			"topFormId": "{{formId}}",
			"parentFormId": "{{formId}}",
			"formName": "{{formName}}",
			"topFormName": "{{formName}}",
			"parentFormName": "{{formName}}",
			"formDescription": "",
			"topFormDescription": "",
			"parentFormDescription": "",
			"case_ref": "{{reference}}",
			"stage_id": "{{_stageId}}",
			"processId": "{{_processId}}",
			"stage_name": "{{_stageName}}",
			"processName": "{{formName}}",
			"stageLength": 1,
			"processDescription": "",
			"processUri": "sandbox-processes://{{_processId}}",
			"version": "1",
			"csrf_token": "{{csrfToken}}",
			"tokenString": "{{metadata["tokenString"]}}",
			"reference": "{{reference}}",
			"process_prefix": "REF"
		},
		"env_tokens": {},
		"site": "{{encodedSite}}",
		"processName": "{{formName}}",
		"created": "{{created}}",
		"reference": "{{reference}}",
		"formUri": "{{_formUri}}"
		""";

		return tokenSections;
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

	/// <summary>
	/// Builds the request body for collection week and bin calendar lookups.
	/// </summary>
	private static string BuildScheduleRequestBody(
		Dictionary<string, string> metadata,
		string collectionWeek
	)
	{
		var today = DateOnly.FromDateTime(DateTime.UtcNow);
		var maxDate = today.AddDays(28);
		var lastSunday = today.AddDays(-(int)today.DayOfWeek);
		var tokenSections = BuildTokenSections(metadata, _mainFormId, _mainFormName);

		var house = metadata["house"];
		var street = metadata["street"];
		var town = metadata["town"];
		var county = metadata["county"];
		var postcode = metadata["postcode"];
		var uprn = metadata["uprn"];
		var wardCode = metadata["wardCode"];
		var wardShort = metadata["wardShort"];
		var wardName = metadata["wardName"];
		var latitude = metadata["latitude"];
		var longitude = metadata["longitude"];
		var usrn = metadata["usrn"];
		var postcodeSector = metadata["postcodeSector"];
		var lengthOfPostcode = metadata["lengthOfPostcode"];
		var lengthMinusTwo = metadata["lengthMinusTwo"];

		var fullAddress = string.Join(
			" ",
			new[] { house, street, town, county, postcode }.Where(part => !string.IsNullOrWhiteSpace(part))
		);

		var fullAddressMultiLine = string.Join(
			"\\n",
			new[] { $"{house} {street}".Trim(), town, county, postcode }.Where(part => !string.IsNullOrWhiteSpace(part))
		);

		var requestBody = $$"""
		{
			"stopOnFailure": true,
			"usePHPIntegrations": true,
			"stage_id": "{{_stageId}}",
			"stage_name": "{{_stageName}}",
			"formId": "{{_mainFormId}}",
			"formValues": {
				"Section 1": {
					"process": { "value": "bin calendar" },
					"testOrLive": { "value": "{{_baseUrl}}" },
					"dateNow": { "value": "{{today:yyyy-MM-dd}}" },
					"todayDayOfWeek": { "value": "{{(int)today.DayOfWeek}}" },
					"lastSunday": { "value": "{{lastSunday:yyyy-MM-dd}}" },
					"calendarEnd": { "value": "{{maxDate:yyyy-MM-dd}}" },
					"selfOrService": { "value": "Self" },
					"lengthQueryUprn": { "value": "0" },
					"isBartecAvailable": { "value": "true" },
					"testOrLiveBartec": { "value": "Live" },
					"isBinCalendarAvailable": { "value": "true" },
					"enterYourAddress": {
						"value": {
							"Section 1": {
								"processName": { "value": "bin calendar" },
								"productName": { "value": "Self" },
								"useDefaultAddress": { "value": "true" },
								"ward": { "value": "{{wardShort}}" },
								"informationCorrectLabel": { "value": "Is this information correct?" },
								"postcode_search": { "value": "{{postcode}}" },
								"chooseAddress": { "value": "{{uprn}}" },
								"txtListHouseNumber": { "value": "{{house}}" },
								"txtListStreet": { "value": "{{street}}" },
								"txtListTown": { "value": "{{town}}" },
								"txtListCounty": { "value": "{{county}}" },
								"txtListPostcode": { "value": "{{postcode}}" },
								"txtUprn": { "value": "{{uprn}}" },
								"txtWard": { "value": "{{wardCode}}" },
								"txtlatitude": { "value": "{{latitude}}" },
								"txtlongitude": { "value": "{{longitude}}" },
								"txtUsrn": { "value": "{{usrn}}" },
								"calcHouseNumber": { "value": "{{house}}" },
								"calcStreet": { "value": "{{street}}" },
								"calcTown": { "value": "{{town}}" },
								"calcWard": { "value": "{{wardCode}}" },
								"calcCounty": { "value": "{{county}}" },
								"calcPostcode": { "value": "{{postcode}}" },
								"txtTheHouse1": { "value": "{{house}}" },
								"txtTheStreet1": { "value": "{{street}}" },
								"txtTheTown1": { "value": "{{town}}" },
								"txtTheCounty1": { "value": "{{county}}" },
								"txtThePostcode1": { "value": "{{postcode}}" },
								"txtTheUprn1": { "value": "{{uprn}}" },
								"noPopuprn": { "value": "{{uprn}}" },
								"txtTheWard1": { "value": "{{wardCode}}" },
								"txtFullAddress2": { "value": "{{fullAddress}}" },
								"fullAddressMultiLine": { "value": "{{fullAddressMultiLine}}" },
								"fullAddressComplete": { "value": "true" },
								"checkSpacesInPostcode": { "value": "true" },
								"lengthOfPostcode": { "value": "{{lengthOfPostcode}}" },
								"lengthMinus2": { "value": "{{lengthMinusTwo}}" },
								"postcodeSector": { "value": "{{postcodeSector}}" },
								"uniformUprn": { "value": "{{uprn}}" },
								"wardCode": { "value": "{{wardCode}}" },
								"GWward": { "value": "{{wardShort}}" },
								"wardName": { "value": "{{wardName}}" },
								"txtUsrn1": { "value": "{{usrn}}" }
							}
						}
					},
					"uprnPWB": { "value": "{{uprn}}" },
					"collectionWeek": { "value": "{{collectionWeek}}" },
					"minDate": { "value": "{{today:yyyy-MM-dd}}" },
					"maxDate": { "value": "{{maxDate:yyyy-MM-dd}}" },
					"connectionConfirmed": { "value": "false" }
				}
			},
			"isPublished": true,
			"formName": "{{_mainFormName}}",
			"processId": "{{_processId}}",
			"formUri": "{{_formUri}}",
			{{tokenSections}}
		}
		""";

		return requestBody;
	}
}
