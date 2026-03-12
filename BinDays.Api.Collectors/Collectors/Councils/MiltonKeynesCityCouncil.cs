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
/// Collector implementation for Milton Keynes City Council.
/// </summary>
internal sealed partial class MiltonKeynesCityCouncil : GovUkCollectorBase, ICollector
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

	private const string _baseUrl = "https://mycouncil.milton-keynes.gov.uk";
	private const string _serviceUrl = "https://mycouncil.milton-keynes.gov.uk/en/service/Waste_Collection_Round_Checker";
	private const string _stageId = "AF-Stage-aeed9de6-ac06-4ac0-bfaf-f6cfee431092";
	private const string _processId = "AF-Process-0a0b6838-6284-4163-998f-7ca6d4d62f41";
	private const string _formUri = "sandbox-publish://AF-Process-0a0b6838-6284-4163-998f-7ca6d4d62f41/AF-Stage-aeed9de6-ac06-4ac0-bfaf-f6cfee431092/definition.json";
	private const string _formName = "Milton Keynes City Council Waste Collection Round Checker";
	private const string _searchFormId = "AF-Form-b7f7c45d-40ed-46dd-aa1c-18fb875921ee";
	private const string _binDaysFormId = "AF-Form-1a083dbd-80dd-4a81-8e7e-7ab0b66b5fb5";
	private const string _addressLookupId = "56a1f135c2a43";
	private const string _tokenLookupId = "64e613b119075";
	private const string _binDaysLookupId = "64d9feda3a507";

	/// <summary>
	/// Regex to extract the session identifier from the HTML content.
	/// </summary>
	[GeneratedRegex(@"sid=(?<sessionId>[a-f0-9]+)")]
	private static partial Regex SessionIdRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for starting the session
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

			var metadata = new Dictionary<string, string>
			{
				{ "sid", sessionId },
				{ "cookie", cookies },
			};

			var requestBody = $$"""
			{
				"stopOnFailure": true,
				"usePHPIntegrations": true,
				"stage_id": "{{_stageId}}",
				"stage_name": "Round Checker",
				"formId": "{{_searchFormId}}",
				"formValues": {
					"Search": {
						"postcode_search": { "value": "{{postcode}}" }
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
				Url = $"{_baseUrl}/apibroker/runLookup?id={_addressLookupId}&repeat_against=&noRetry=false&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&_={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}&sid={sessionId}",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.ApplicationJson },
					{ "x-requested-with", Constants.XmlHttpRequest },
					{ "cookie", cookies },
				},
				Body = requestBody,
				Options = new ClientSideOptions
				{
					Metadata = metadata,
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
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var xmlData = jsonDoc.RootElement.GetProperty("data").GetString()!;

			var xml = XDocument.Parse(xmlData);
			var rows = xml.Descendants("Row");

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var row in rows)
			{
				var results = row.Elements("result").ToList();
				var uprn = results.First(result => result.Attribute("column")!.Value == "uprn").Value.Trim();
				var display = results.First(result => result.Attribute("column")!.Value == "display").Value.Trim();
				var usrn = results.First(result => result.Attribute("column")!.Value == "usrn").Value.Trim();
				var house = results.First(result => result.Attribute("column")!.Value == "house").Value.Trim();
				var street = results.First(result => result.Attribute("column")!.Value == "street").Value.Trim();
				var locality = results.First(result => result.Attribute("column")!.Value == "locality").Value.Trim();
				var town = results.First(result => result.Attribute("column")!.Value == "town").Value.Trim();
				var county = results.First(result => result.Attribute("column")!.Value == "county").Value.Trim();
				var blpu = results.First(result => result.Attribute("column")!.Value == "BLPU").Value.Trim();

				var uid = string.Join(
					';',
					new[]
					{
						uprn,
						usrn,
						house,
						street,
						locality,
						town,
						county,
						postcode,
						blpu,
					}
				);

				var address = new Address
				{
					Property = display,
					Postcode = postcode,
					// Uid format: uprn;usrn;house;street;locality;town;county;postcode;blpu
					Uid = uid,
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
			var clientSideRequest = CreateInitialRequest();

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for fetching the bearer token
		else if (clientSideResponse.RequestId == 1)
		{
			var (sessionId, cookies) = ExtractSessionData(clientSideResponse);

			var metadata = new Dictionary<string, string>
			{
				{ "sid", sessionId },
				{ "cookie", cookies },
			};

			var requestBody = $$"""
			{
				"stopOnFailure": true,
				"usePHPIntegrations": true,
				"stage_id": "{{_stageId}}",
				"stage_name": "Round Checker",
				"formId": "{{_binDaysFormId}}",
				"formValues": {
					"Section 1": {
						"showTable": { "value": "no" },
						"sfNumberOfEntries": { "value": "0" },
						"serviceResComm": { "value": "Domestic" },
						"RefuseEntry": { "value": "0" },
						"RecyclingEntry": { "value": "0" },
						"FGWEntry": { "value": "0" },
						"roundInformationStatus": { "value": "No address entered" }
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
				Url = $"{_baseUrl}/apibroker/runLookup?id={_tokenLookupId}&repeat_against=&noRetry=true&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&_={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}&sid={sessionId}",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.ApplicationJson },
					{ "x-requested-with", Constants.XmlHttpRequest },
					{ "cookie", cookies },
				},
				Body = requestBody,
				Options = new ClientSideOptions
				{
					Metadata = metadata,
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for bin days lookup
		else if (clientSideResponse.RequestId == 2)
		{
			var metadata = new Dictionary<string, string>(clientSideResponse.Options.Metadata);

			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var tokenData = jsonDoc.RootElement.GetProperty("data").GetString()!;
			var tokenXml = XDocument.Parse(tokenData);
			var coreBearerToken = tokenXml.Descendants("result").First().Value.Trim();

			var parts = address.Uid!.Split(';');
			var uprn = parts[0];
			var usrn = parts[1];
			var house = parts[2];
			var street = parts[3];
			var locality = parts[4];
			var town = parts[5];
			var county = parts[6];
			var postcode = parts[7];
			var blpu = parts[8];

			var fullAddress = string.Join(
				", ",
				new[]
				{
					house,
					street,
					locality,
					town,
					county,
					postcode,
				}.Where(part => !string.IsNullOrWhiteSpace(part))
			);

			var requestBody = $$"""
			{
				"stopOnFailure": true,
				"usePHPIntegrations": true,
				"stage_id": "{{_stageId}}",
				"stage_name": "Round Checker",
				"formId": "{{_binDaysFormId}}",
				"formValues": {
					"Section 1": {
						"coreBearerToken": { "value": "{{coreBearerToken}}" },
						"propertySearch": {
							"postcode_search": { "value": "{{address.Postcode!}}" },
							"ChooseAddress": { "value": "{{uprn}}" },
							"propertyUprn": { "value": "{{uprn}}" },
							"propertyUsrn": { "value": "{{usrn}}" },
							"propertyPaon": { "value": "{{house}}" },
							"propertyHouse": { "value": "{{house}}" },
							"propertyStreet": { "value": "{{street}}" },
							"propertyLocality": { "value": "{{locality}}" },
							"propertyTown": { "value": "{{town}}" },
							"propertyCounty": { "value": "{{county}}" },
							"propertyPostcode": { "value": "{{address.Postcode!}}" },
							"fullAddress": { "value": "{{fullAddress}}" },
							"BLPU": { "value": "{{blpu}}" }
						},
						"uprnCore": { "value": "{{uprn}}" },
						"fullAddress1": { "value": "{{fullAddress}}" },
						"assisted": { "value": "false" },
						"showTable": { "value": "no" },
						"sfNumberOfEntries": { "value": "0" },
						"serviceResComm": { "value": "Domestic" },
						"RefuseEntry": { "value": "0" },
						"RecyclingEntry": { "value": "0" },
						"FGWEntry": { "value": "0" },
						"processDowntime": {
							"Section 1": {
								"isProcessDown": { "value": "No" }
							}
						},
						"roundInformationStatus": { "value": "No round information" }
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
				RequestId = 3,
				Url = $"{_baseUrl}/apibroker/runLookup?id={_binDaysLookupId}&repeat_against=&noRetry=false&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&_={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}&sid={metadata["sid"]}",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.ApplicationJson },
					{ "x-requested-with", Constants.XmlHttpRequest },
					{ "cookie", metadata["cookie"] },
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
		else if (clientSideResponse.RequestId == 3)
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
				var service = results.First(result => result.Attribute("column")!.Value == "TaskTypeName").Value.Trim();
				var nextCollection = results.First(result => result.Attribute("column")!.Value == "NextInstance").Value.Trim();
				var lastCollection = results.First(result => result.Attribute("column")!.Value == "LastInstance").Value.Trim();

				if (!string.IsNullOrWhiteSpace(nextCollection))
				{
					var nextDate = DateUtilities.ParseDateExact(nextCollection, "yyyy-MM-dd");
					var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, service);

					var binDay = new BinDay
					{
						Date = nextDate,
						Address = address,
						Bins = matchedBins,
					};

					binDays.Add(binDay);
				}

				if (!string.IsNullOrWhiteSpace(lastCollection))
				{
					var lastDate = DateUtilities.ParseDateExact(lastCollection, "yyyy-MM-dd");
					var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, service);

					var binDay = new BinDay
					{
						Date = lastDate,
						Address = address,
						Bins = matchedBins,
					};

					binDays.Add(binDay);
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

	/// <summary>
	/// Creates the initial client-side request used to start the session.
	/// </summary>
	private static ClientSideRequest CreateInitialRequest()
	{
		var clientSideRequest = new ClientSideRequest
		{
			RequestId = 1,
			Url = _serviceUrl,
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
