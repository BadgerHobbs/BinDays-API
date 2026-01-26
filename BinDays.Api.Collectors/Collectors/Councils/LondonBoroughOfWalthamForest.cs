namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for London Borough of Waltham Forest.
/// </summary>
internal sealed partial class LondonBoroughOfWalthamForest : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "London Borough of Waltham Forest";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.walthamforest.gov.uk/rubbish-and-recycling/household-bin-collections/check-your-collection-days");

	/// <inheritdoc/>
	public override string GovUkId => "waltham-forest";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Domestic Waste Collection Service" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Green,
			Keys = [ "Recycling Collection Service" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden Waste Collection Service" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Brown,
			Keys = [ "Food Waste Collection Service" ],
			Type = BinType.Caddy,
		},
	];

	private const string _achieveFormsUrl = "https://portal.walthamforest.gov.uk/AchieveForms/?mode=fill&consentMessage=yes&form_uri=sandbox-publish://AF-Process-d62ccdd2-3de9-48eb-a229-8e20cbdd6393/AF-Stage-8bf39bf9-5391-4c24-857f-0dc2025c67f4/definition.json&process=1&process_uri=sandbox-processes://AF-Process-d62ccdd2-3de9-48eb-a229-8e20cbdd6393&process_id=AF-Process-d62ccdd2-3de9-48eb-a229-8e20cbdd6393";
	private const string _apibrokerBaseUrl = "https://portal.walthamforest.gov.uk/apibroker/";
	private const string _stageId = "AF-Stage-8bf39bf9-5391-4c24-857f-0dc2025c67f4";
	private const string _addressFormId = "AF-Form-08647570-43e4-4e68-9d6a-65d914e27ef7";
	private const string _mainFormId = "AF-Form-07a98da7-bc6b-4df6-aa46-33a06312acce";
	private const string _addressLookupId = "5694fd42a5541";
	private const string _setAddressLookupId = "5e42e28b44d9e";
	private const string _collectionsLookupId = "5e208cda0d0a0";

	/// <summary>
	/// Regex to extract the session ID (sid) from HTML content.
	/// </summary>
	[GeneratedRegex(@"sid=([a-f0-9]+)")]
	private static partial Regex SessionIdRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var formattedPostcode = ProcessingUtilities.FormatPostcode(postcode);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _achieveFormsUrl,
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
				},
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "postcode", formattedPostcode },
					},
				},
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
			var sid = SessionIdRegex().Match(clientSideResponse.Content).Groups[1].Value;
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(
				clientSideResponse.Headers["set-cookie"]
			);
			var formattedPostcode = clientSideResponse.Options.Metadata["postcode"];

			var requestBody = $$"""
			{
				"stopOnFailure": true,
				"stage_id": "{{_stageId}}",
				"stage_name": "Enquiry",
				"formId": "{{_addressFormId}}",
				"formValues": {
					"Section 1": {
						"blankLabel": {
							"name": "blankLabel",
							"type": "text",
							"id": "AF-Field-9ab181c8-c72d-43e8-91ea-17f9b632617f",
							"value_changed": true,
							"section_id": "AF-Section-cc45faa3-b04a-4815-81c4-261ff6cd94f2",
							"label": "blankLabel",
							"value": "",
							"path": "root/addressLookup/blankLabel",
							"valid": ""
						},
						"postcode_search": {
							"name": "postcode_search",
							"type": "text",
							"id": "AF-Field-648c89c8-ba21-49e0-b877-c23736d00e27",
							"value_changed": true,
							"section_id": "AF-Section-cc45faa3-b04a-4815-81c4-261ff6cd94f2",
							"label": "Enter your postcode",
							"value": "{{formattedPostcode}}",
							"path": "root/addressLookup/postcode_search",
							"valid": true
						},
						"postcodeFound": {
							"name": "postcodeFound",
							"type": "text",
							"id": "AF-Field-a46bf8a7-377a-4179-9234-869f03170b10",
							"value_changed": true,
							"section_id": "AF-Section-cc45faa3-b04a-4815-81c4-261ff6cd94f2",
							"label": "postcodeFound",
							"value": "",
							"path": "root/addressLookup/postcodeFound",
							"valid": ""
						},
						"uprnConfirm": {
							"name": "uprnConfirm",
							"type": "text",
							"id": "AF-Field-95ab2ef2-a616-4dbc-a0a7-86f5f88b2c17",
							"value_changed": true,
							"section_id": "AF-Section-cc45faa3-b04a-4815-81c4-261ff6cd94f2",
							"label": "uprnConfirm",
							"value": "",
							"path": "root/addressLookup/uprnConfirm",
							"valid": ""
						},
						"wardName": {
							"name": "wardName",
							"type": "text",
							"id": "AF-Field-0d01d6b6-eb90-4f20-8bd0-3cd6c3afb7f4",
							"value_changed": true,
							"section_id": "AF-Section-cc45faa3-b04a-4815-81c4-261ff6cd94f2",
							"label": "wardName",
							"value": "",
							"path": "root/addressLookup/wardName",
							"valid": ""
						}
					}
				}
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_apibrokerBaseUrl}?api=RunLookup&id={_addressLookupId}&repeat_against=&noRetry=false&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&sid={sid}",
				Method = "POST",
				Headers = new()
				{
					{ "content-type", "application/json" },
					{ "cookie", requestCookies },
					{ "x-requested-with", "XMLHttpRequest" },
				},
				Body = requestBody,
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "cookie", requestCookies },
						{ "sid", sid },
						{ "postcode", formattedPostcode },
					},
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
			var rowsData = jsonDoc.RootElement
				.GetProperty("integration")
				.GetProperty("transformed")
				.GetProperty("rows_data");

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var property in rowsData.EnumerateObject())
			{
				var addressData = property.Value;
				var display = addressData.GetProperty("display").GetString()!;
				var uprn = addressData.GetProperty("overview_uprn").GetString()!;
				var addressPostcode = addressData.GetProperty("overview_postcode").GetString()!;

				var address = new Address
				{
					Property = display.Trim(),
					Postcode = addressPostcode.Trim(),
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
		// Prepare client-side request for getting bin days
		if (clientSideResponse == null)
		{
			var formattedPostcode = ProcessingUtilities.FormatPostcode(address.Postcode!);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _achieveFormsUrl,
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
				},
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "postcode", formattedPostcode },
						{ "uprn", address.Uid! },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for address lookup before bin days
		else if (clientSideResponse.RequestId == 1)
		{
			var sid = SessionIdRegex().Match(clientSideResponse.Content).Groups[1].Value;
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(
				clientSideResponse.Headers["set-cookie"]
			);
			var formattedPostcode = clientSideResponse.Options.Metadata["postcode"];

			var requestBody = $$"""
			{
				"stopOnFailure": true,
				"stage_id": "{{_stageId}}",
				"stage_name": "Enquiry",
				"formId": "{{_addressFormId}}",
				"formValues": {
					"Section 1": {
						"blankLabel": {
							"name": "blankLabel",
							"type": "text",
							"id": "AF-Field-9ab181c8-c72d-43e8-91ea-17f9b632617f",
							"value_changed": true,
							"section_id": "AF-Section-cc45faa3-b04a-4815-81c4-261ff6cd94f2",
							"label": "blankLabel",
							"value": "",
							"path": "root/addressLookup/blankLabel",
							"valid": ""
						},
						"postcode_search": {
							"name": "postcode_search",
							"type": "text",
							"id": "AF-Field-648c89c8-ba21-49e0-b877-c23736d00e27",
							"value_changed": true,
							"section_id": "AF-Section-cc45faa3-b04a-4815-81c4-261ff6cd94f2",
							"label": "Enter your postcode",
							"value": "{{formattedPostcode}}",
							"path": "root/addressLookup/postcode_search",
							"valid": true
						},
						"postcodeFound": {
							"name": "postcodeFound",
							"type": "text",
							"id": "AF-Field-a46bf8a7-377a-4179-9234-869f03170b10",
							"value_changed": true,
							"section_id": "AF-Section-cc45faa3-b04a-4815-81c4-261ff6cd94f2",
							"label": "postcodeFound",
							"value": "",
							"path": "root/addressLookup/postcodeFound",
							"valid": ""
						},
						"uprnConfirm": {
							"name": "uprnConfirm",
							"type": "text",
							"id": "AF-Field-95ab2ef2-a616-4dbc-a0a7-86f5f88b2c17",
							"value_changed": true,
							"section_id": "AF-Section-cc45faa3-b04a-4815-81c4-261ff6cd94f2",
							"label": "uprnConfirm",
							"value": "",
							"path": "root/addressLookup/uprnConfirm",
							"valid": ""
						},
						"wardName": {
							"name": "wardName",
							"type": "text",
							"id": "AF-Field-0d01d6b6-eb90-4f20-8bd0-3cd6c3afb7f4",
							"value_changed": true,
							"section_id": "AF-Section-cc45faa3-b04a-4815-81c4-261ff6cd94f2",
							"label": "wardName",
							"value": "",
							"path": "root/addressLookup/wardName",
							"valid": ""
						}
					}
				}
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_apibrokerBaseUrl}?api=RunLookup&id={_addressLookupId}&repeat_against=&noRetry=false&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&sid={sid}",
				Method = "POST",
				Headers = new()
				{
					{ "content-type", "application/json" },
					{ "cookie", requestCookies },
					{ "x-requested-with", "XMLHttpRequest" },
				},
				Body = requestBody,
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "cookie", requestCookies },
						{ "sid", sid },
						{ "postcode", formattedPostcode },
						{ "uprn", address.Uid! },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for bin collections lookup
		else if (clientSideResponse.RequestId == 2)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var rowsData = jsonDoc.RootElement
				.GetProperty("integration")
				.GetProperty("transformed")
				.GetProperty("rows_data");

			JsonElement? matchedAddress = null;
			var uprn = clientSideResponse.Options.Metadata["uprn"];
			foreach (var property in rowsData.EnumerateObject())
			{
				if (property.Value.GetProperty("overview_uprn").GetString()! == uprn)
				{
					matchedAddress = property.Value;
					break;
				}
			}

			var sid = clientSideResponse.Options.Metadata["sid"];
			var requestCookies = clientSideResponse.Options.Metadata["cookie"];
			var formattedPostcode = clientSideResponse.Options.Metadata["postcode"];

			var addressDisplay = matchedAddress!.Value.GetProperty("display").GetString()!;
			var ward = matchedAddress.Value.GetProperty("overview_ward").GetString()!;

			var requestBody = $$"""
			{
				"stopOnFailure": true,
				"usePHPIntegrations": true,
				"stage_id": "{{_stageId}}",
				"stage_name": "Enquiry",
				"formId": "{{_mainFormId}}",
				"formValues": {
					"Section 1": {
						"blankLabel": {
							"name": "blankLabel",
							"type": "text",
							"id": "AF-Field-eee8919b-4a60-475a-9b69-f06ca8730949",
							"value_changed": true,
							"section_id": "AF-Section-8cd8bd22-73c5-42b4-ba2c-f66fb15dfedf",
							"label": "blankLabel",
							"value": "",
							"path": "root/blankLabel",
							"valid": ""
						},
						"HelloWorldResponse": {
							"name": "HelloWorldResponse",
							"type": "text",
							"id": "AF-Field-0cf17f1f-d014-4249-9fb8-8a6e812ab7b0",
							"value_changed": true,
							"section_id": "AF-Section-8cd8bd22-73c5-42b4-ba2c-f66fb15dfedf",
							"label": "HelloWorldResponse",
							"value": "Hello World",
							"path": "root/HelloWorldResponse",
							"valid": ""
						},
						"lookupRun": {
							"name": "lookupRun",
							"type": "text",
							"id": "AF-Field-30a6afa0-ce95-476c-a2b1-c93f18a932df",
							"value_changed": true,
							"section_id": "AF-Section-8cd8bd22-73c5-42b4-ba2c-f66fb15dfedf",
							"label": "lookupRun",
							"value": "1",
							"path": "root/lookupRun",
							"valid": ""
						},
						"parentProcess": {
							"name": "parentProcess",
							"type": "text",
							"id": "AF-Field-e9f2e55b-3ff8-483a-afb6-513428f50f7a",
							"value_changed": true,
							"section_id": "AF-Section-8cd8bd22-73c5-42b4-ba2c-f66fb15dfedf",
							"label": "parentProcess",
							"value": "Find My Bin Collection Dates",
							"path": "root/parentProcess",
							"valid": ""
						},
						"calcWardCode": {
							"name": "calcWardCode",
							"type": "text",
							"id": "AF-Field-58c65da9-1a3f-409c-a0b5-dbb727365996",
							"value_changed": true,
							"section_id": "AF-Section-8cd8bd22-73c5-42b4-ba2c-f66fb15dfedf",
							"label": "calcWardCode",
							"value": "{{ward}}",
							"path": "root/calcWardCode",
							"valid": ""
						},
						"addressLookup": {
							"name": "addressLookup",
							"type": "subform",
							"id": "AF-Field-176c0544-bcae-43fe-a7bd-a5d30d87458d",
							"value_changed": true,
							"section_id": "AF-Section-8cd8bd22-73c5-42b4-ba2c-f66fb15dfedf",
							"value": {
								"Section 1": {
									"blankLabel": {
										"name": "blankLabel",
										"type": "text",
										"id": "AF-Field-9ab181c8-c72d-43e8-91ea-17f9b632617f",
										"value_changed": true,
										"section_id": "AF-Section-cc45faa3-b04a-4815-81c4-261ff6cd94f2",
										"label": "blankLabel",
										"value": "",
										"path": "root/addressLookup/blankLabel",
										"valid": ""
									},
									"postcode_search": {
										"name": "postcode_search",
										"type": "text",
										"id": "AF-Field-648c89c8-ba21-49e0-b877-c23736d00e27",
										"value_changed": true,
										"section_id": "AF-Section-cc45faa3-b04a-4815-81c4-261ff6cd94f2",
										"label": "Enter your postcode",
										"value": "{{formattedPostcode}}",
										"path": "root/addressLookup/postcode_search",
										"valid": true
									},
									"postcodeFound": {
										"name": "postcodeFound",
										"type": "text",
										"id": "AF-Field-a46bf8a7-377a-4179-9234-869f03170b10",
										"value_changed": true,
										"section_id": "AF-Section-cc45faa3-b04a-4815-81c4-261ff6cd94f2",
										"label": "postcodeFound",
										"value": "1",
										"path": "root/addressLookup/postcodeFound",
										"valid": ""
									},
									"YourAddress": {
										"name": "YourAddress",
										"type": "select",
										"id": "AF-Field-970934ef-6747-4722-97ca-8d5577423243",
										"value_changed": true,
										"section_id": "AF-Section-cc45faa3-b04a-4815-81c4-261ff6cd94f2",
										"label": "Select your address",
										"value_label": [
											"{{addressDisplay}}"
										],
										"value": "{{uprn}}",
										"path": "root/addressLookup/YourAddress",
										"valid": true
									},
									"uprnConfirm": {
										"name": "uprnConfirm",
										"type": "text",
										"id": "AF-Field-95ab2ef2-a616-4dbc-a0a7-86f5f88b2c17",
										"value_changed": true,
										"section_id": "AF-Section-cc45faa3-b04a-4815-81c4-261ff6cd94f2",
										"label": "uprnConfirm",
										"value": "{{uprn}}",
										"path": "root/addressLookup/uprnConfirm",
										"valid": true
									},
									"wardName": {
										"name": "wardName",
										"type": "text",
										"id": "AF-Field-0d01d6b6-eb90-4f20-8bd0-3cd6c3afb7f4",
										"value_changed": true,
										"section_id": "AF-Section-cc45faa3-b04a-4815-81c4-261ff6cd94f2",
										"label": "wardName",
										"value": "{{ward}}",
										"path": "root/addressLookup/wardName",
										"valid": ""
									}
								}
							},
							"path": "root/addressLookup",
							"valid": true
						},
						"addressLookupValue": {
							"name": "addressLookupValue",
							"type": "text",
							"id": "AF-Field-ed6bafbd-c445-4048-85ac-a298a42687b5",
							"value_changed": true,
							"section_id": "AF-Section-8cd8bd22-73c5-42b4-ba2c-f66fb15dfedf",
							"label": "addressLookupValue",
							"value": "{{addressDisplay}}",
							"path": "root/addressLookupValue",
							"valid": ""
						},
						"siteCollectionsSuccessFlag": {
							"name": "siteCollectionsSuccessFlag",
							"type": "text",
							"id": "AF-Field-350a8f65-eabd-4a8c-bbe3-e9edb1e1de46",
							"value_changed": true,
							"section_id": "AF-Section-8cd8bd22-73c5-42b4-ba2c-f66fb15dfedf",
							"label": "siteCollectionsSuccessFlag",
							"value": "true",
							"path": "root/siteCollectionsSuccessFlag",
							"valid": ""
						},
						"inputUPRN": {
							"name": "inputUPRN",
							"type": "text",
							"id": "AF-Field-19c12ba7-2d59-46f3-b966-52250183ad62",
							"value_changed": true,
							"section_id": "AF-Section-8cd8bd22-73c5-42b4-ba2c-f66fb15dfedf",
							"label": "inputUPRN",
							"value": "{{uprn}}",
							"path": "root/inputUPRN",
							"valid": ""
						}
					}
				}
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = $"{_apibrokerBaseUrl}runLookup?id={_setAddressLookupId}&repeat_against=&noRetry=true&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&sid={sid}",
				Method = "POST",
				Headers = new()
				{
					{ "content-type", "application/json" },
					{ "cookie", requestCookies },
					{ "x-requested-with", "XMLHttpRequest" },
				},
				Body = requestBody,
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "cookie", requestCookies },
						{ "sid", sid },
						{ "postcode", formattedPostcode },
						{ "uprn", uprn },
						{ "address", addressDisplay },
						{ "ward", ward },
					},
				},
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
			var sid = clientSideResponse.Options.Metadata["sid"];
			var requestCookies = clientSideResponse.Options.Metadata["cookie"];
			var uprn = clientSideResponse.Options.Metadata["uprn"];
			var addressDisplay = clientSideResponse.Options.Metadata["address"];
			var formattedPostcode = clientSideResponse.Options.Metadata["postcode"];
			var ward = clientSideResponse.Options.Metadata["ward"];

			var requestBody = $$"""
			{
				"stopOnFailure": true,
				"usePHPIntegrations": true,
				"stage_id": "{{_stageId}}",
				"stage_name": "Enquiry",
				"formId": "{{_mainFormId}}",
				"formValues": {
					"Section 1": {
						"blankLabel": {
							"name": "blankLabel",
							"type": "text",
							"id": "AF-Field-eee8919b-4a60-475a-9b69-f06ca8730949",
							"value_changed": true,
							"section_id": "AF-Section-8cd8bd22-73c5-42b4-ba2c-f66fb15dfedf",
							"label": "blankLabel",
							"value": "",
							"path": "root/blankLabel",
							"valid": ""
						},
						"HelloWorldResponse": {
							"name": "HelloWorldResponse",
							"type": "text",
							"id": "AF-Field-0cf17f1f-d014-4249-9fb8-8a6e812ab7b0",
							"value_changed": true,
							"section_id": "AF-Section-8cd8bd22-73c5-42b4-ba2c-f66fb15dfedf",
							"label": "HelloWorldResponse",
							"value": "Hello World",
							"path": "root/HelloWorldResponse",
							"valid": ""
						},
						"lookupRun": {
							"name": "lookupRun",
							"type": "text",
							"id": "AF-Field-30a6afa0-ce95-476c-a2b1-c93f18a932df",
							"value_changed": true,
							"section_id": "AF-Section-8cd8bd22-73c5-42b4-ba2c-f66fb15dfedf",
							"label": "lookupRun",
							"value": "1",
							"path": "root/lookupRun",
							"valid": ""
						},
						"parentProcess": {
							"name": "parentProcess",
							"type": "text",
							"id": "AF-Field-e9f2e55b-3ff8-483a-afb6-513428f50f7a",
							"value_changed": true,
							"section_id": "AF-Section-8cd8bd22-73c5-42b4-ba2c-f66fb15dfedf",
							"label": "parentProcess",
							"value": "Find My Bin Collection Dates",
							"path": "root/parentProcess",
							"valid": ""
						},
						"calcWardCode": {
							"name": "calcWardCode",
							"type": "text",
							"id": "AF-Field-58c65da9-1a3f-409c-a0b5-dbb727365996",
							"value_changed": true,
							"section_id": "AF-Section-8cd8bd22-73c5-42b4-ba2c-f66fb15dfedf",
							"label": "calcWardCode",
							"value": "{{ward}}",
							"path": "root/calcWardCode",
							"valid": ""
						},
						"addressLookup": {
							"name": "addressLookup",
							"type": "subform",
							"id": "AF-Field-176c0544-bcae-43fe-a7bd-a5d30d87458d",
							"value_changed": true,
							"section_id": "AF-Section-8cd8bd22-73c5-42b4-ba2c-f66fb15dfedf",
							"value": {
								"Section 1": {
									"blankLabel": {
										"name": "blankLabel",
										"type": "text",
										"id": "AF-Field-9ab181c8-c72d-43e8-91ea-17f9b632617f",
										"value_changed": true,
										"section_id": "AF-Section-cc45faa3-b04a-4815-81c4-261ff6cd94f2",
										"label": "blankLabel",
										"value": "",
										"path": "root/addressLookup/blankLabel",
										"valid": ""
									},
									"postcode_search": {
										"name": "postcode_search",
										"type": "text",
										"id": "AF-Field-648c89c8-ba21-49e0-b877-c23736d00e27",
										"value_changed": true,
										"section_id": "AF-Section-cc45faa3-b04a-4815-81c4-261ff6cd94f2",
										"label": "Enter your postcode",
										"value": "{{formattedPostcode}}",
										"path": "root/addressLookup/postcode_search",
										"valid": true
									},
									"postcodeFound": {
										"name": "postcodeFound",
										"type": "text",
										"id": "AF-Field-a46bf8a7-377a-4179-9234-869f03170b10",
										"value_changed": true,
										"section_id": "AF-Section-cc45faa3-b04a-4815-81c4-261ff6cd94f2",
										"label": "postcodeFound",
										"value": "1",
										"path": "root/addressLookup/postcodeFound",
										"valid": ""
									},
									"YourAddress": {
										"name": "YourAddress",
										"type": "select",
										"id": "AF-Field-970934ef-6747-4722-97ca-8d5577423243",
										"value_changed": true,
										"section_id": "AF-Section-cc45faa3-b04a-4815-81c4-261ff6cd94f2",
										"label": "Select your address",
										"value_label": [
											"{{addressDisplay}}"
										],
										"value": "{{uprn}}",
										"path": "root/addressLookup/YourAddress",
										"valid": true
									},
									"uprnConfirm": {
										"name": "uprnConfirm",
										"type": "text",
										"id": "AF-Field-95ab2ef2-a616-4dbc-a0a7-86f5f88b2c17",
										"value_changed": true,
										"section_id": "AF-Section-cc45faa3-b04a-4815-81c4-261ff6cd94f2",
										"label": "uprnConfirm",
										"value": "{{uprn}}",
										"path": "root/addressLookup/uprnConfirm",
										"valid": true
									},
									"wardName": {
										"name": "wardName",
										"type": "text",
										"id": "AF-Field-0d01d6b6-eb90-4f20-8bd0-3cd6c3afb7f4",
										"value_changed": true,
										"section_id": "AF-Section-cc45faa3-b04a-4815-81c4-261ff6cd94f2",
										"label": "wardName",
										"value": "{{ward}}",
										"path": "root/addressLookup/wardName",
										"valid": ""
									}
								}
							},
							"path": "root/addressLookup",
							"valid": true
						},
						"addressLookupValue": {
							"name": "addressLookupValue",
							"type": "text",
							"id": "AF-Field-ed6bafbd-c445-4048-85ac-a298a42687b5",
							"value_changed": true,
							"section_id": "AF-Section-8cd8bd22-73c5-42b4-ba2c-f66fb15dfedf",
							"label": "addressLookupValue",
							"value": "{{addressDisplay}}",
							"path": "root/addressLookupValue",
							"valid": ""
						},
						"siteCollectionsSuccessFlag": {
							"name": "siteCollectionsSuccessFlag",
							"type": "text",
							"id": "AF-Field-350a8f65-eabd-4a8c-bbe3-e9edb1e1de46",
							"value_changed": true,
							"section_id": "AF-Section-8cd8bd22-73c5-42b4-ba2c-f66fb15dfedf",
							"label": "siteCollectionsSuccessFlag",
							"value": "true",
							"path": "root/siteCollectionsSuccessFlag",
							"valid": ""
						},
						"inputUPRN": {
							"name": "inputUPRN",
							"type": "text",
							"id": "AF-Field-19c12ba7-2d59-46f3-b966-52250183ad62",
							"value_changed": true,
							"section_id": "AF-Section-8cd8bd22-73c5-42b4-ba2c-f66fb15dfedf",
							"label": "inputUPRN",
							"value": "{{uprn}}",
							"path": "root/inputUPRN",
							"valid": ""
						}
					}
				}
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 4,
				Url = $"{_apibrokerBaseUrl}runLookup?id={_collectionsLookupId}&repeat_against=&noRetry=false&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&sid={sid}",
				Method = "POST",
				Headers = new()
				{
					{ "content-type", "application/json" },
					{ "cookie", requestCookies },
					{ "x-requested-with", "XMLHttpRequest" },
				},
				Body = requestBody,
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "address", addressDisplay },
						{ "postcode", formattedPostcode },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin day results
		else if (clientSideResponse.RequestId == 4)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var rowsData = jsonDoc.RootElement
				.GetProperty("integration")
				.GetProperty("transformed")
				.GetProperty("rows_data");

			// Iterate through each bin day record, and create a new bin day object
			var binDays = new List<BinDay>();
			var binDayEntries = new List<JsonElement>();
			if (rowsData.ValueKind == JsonValueKind.Object)
			{
				foreach (var property in rowsData.EnumerateObject())
				{
					binDayEntries.Add(property.Value);
				}
			}
			else if (rowsData.ValueKind == JsonValueKind.Array)
			{
				binDayEntries.AddRange(rowsData.EnumerateArray());
			}

			foreach (var binData in binDayEntries)
			{
				var serviceName = binData.GetProperty("ServiceName").GetString()!;
				var nextCollectionDate = binData.GetProperty("NextCollectionDate").GetString()!.Trim();

				if (string.IsNullOrWhiteSpace(nextCollectionDate) || nextCollectionDate.Contains("NaN", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				var date = nextCollectionDate.ParseDateInferringYear("dddd d MMMM");
				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, serviceName);

				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = matchedBins,
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
}
