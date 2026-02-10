namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for West Oxfordshire District Council.
/// </summary>
internal sealed partial class WestOxfordshireDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "West Oxfordshire District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.westoxon.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "west-oxfordshire";

	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "Rubbish",
			Colour = BinColour.Grey,
			Keys = [ "Refuse", ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Blue,
			Keys = [ "360 Litre Recycling", ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Black,
			Keys = [ "Black Recycling Box", ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Food waste",
			Colour = BinColour.Grey,
			Keys = [ "Food", ],
		},
	];

	/// <summary>
	/// Regex for extracting the Salesforce framework UID from the page content.
	/// </summary>
	[GeneratedRegex(@"""fwuid"":""(?<fwuid>[^""]+)""")]
	private static partial Regex FwuidRegex();

	/// <summary>
	/// Regex for extracting the Salesforce application ID from the page content.
	/// </summary>
	[GeneratedRegex(@"APPLICATION@markup://siteforce:communityApp"":""(?<appId>[^""]+)""")]
	private static partial Regex AppIdRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		if (clientSideResponse == null)
		{
			var clientSideRequest = CreateInitialRequest();

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		else if (clientSideResponse.RequestId == 1)
		{
			var (requestCookies, fwuid, appId) = ExtractSessionTokens(clientSideResponse);

			var lookupMessage = $$"""
				{
					"actions": [
						{
							"id": "lookup",
							"descriptor": "aura://LookupController/ACTION$lookup",
							"params": {
								"objectApiName": "Case",
								"fieldApiName": "Property__c",
								"pageSize": 50,
								"q": "{{postcode}}",
								"searchType": "TypeAhead",
								"targetApiName": "Property__c",
								"body": {
									"sourceRecord": {
										"apiName": "Case",
										"fields": {
											"Id": null
										}
									}
								}
							}
						}
					]
				}
				""";

			var messageBody = BuildAuraFormData(lookupMessage, fwuid, appId);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = "https://community.westoxon.gov.uk/s/sfsites/aura?r=9&aura.Lookup.lookup=1",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", "application/x-www-form-urlencoded;charset=UTF-8" },
					{ "cookie", requestCookies },
				},
				Body = messageBody,
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		else if (clientSideResponse.RequestId == 2)
		{
			using var document = JsonDocument.Parse(clientSideResponse.Content);
			var actions = document.RootElement.GetProperty("actions");
			var lookupResults = actions[0]
				.GetProperty("returnValue")
				.GetProperty("lookupResults")
				.GetProperty("Property__c")
				.GetProperty("records");

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var record in lookupResults.EnumerateArray())
			{
				var uid = record.GetProperty("id").GetString()!;
				var property = record.GetProperty("fields").GetProperty(nameof(Name)).GetProperty("value").GetString()!;

				var address = new Address
				{
					Property = property,
					Postcode = postcode,
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
		if (clientSideResponse == null)
		{
			var clientSideRequest = CreateInitialRequest();

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		else if (clientSideResponse.RequestId == 1)
		{
			var (requestCookies, fwuid, appId) = ExtractSessionTokens(clientSideResponse);

			var startFlowMessage = """
				{
					"actions": [
						{
							"id": "start",
							"descriptor": "aura://FlowRuntimeConnectController/ACTION$startFlow",
							"params": {
								"flowDevName": "WebFormAlloyWasteCollectionEnquiry",
								"arguments": "[{\"name\":\"vClientCode\",\"type\":\"String\",\"value\":\"WOD\"}]"
							}
						}
					]
				}
				""";

			var messageBody = BuildAuraFormData(startFlowMessage, fwuid, appId);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = "https://community.westoxon.gov.uk/s/sfsites/aura?r=4&aura.FlowRuntimeConnect.startFlow=1",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", "application/x-www-form-urlencoded;charset=UTF-8" },
					{ "cookie", requestCookies },
				},
				Body = messageBody,
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "fwuid", fwuid },
						{ "appId", appId },
						{ "cookie", requestCookies },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		else if (clientSideResponse.RequestId == 2)
		{
			return CreateNavigateResponse(3, "NEXT", clientSideResponse, address);
		}
		else if (clientSideResponse.RequestId == 3)
		{
			return CreateNavigateResponse(4, "CONTINUE_AFTER_COMMIT", clientSideResponse, address);
		}
		else if (clientSideResponse.RequestId == 4)
		{
			return CreateNavigateResponse(5, "CONTINUE_AFTER_COMMIT", clientSideResponse, address);
		}
		else if (clientSideResponse.RequestId == 5)
		{
			using var document = JsonDocument.Parse(clientSideResponse.Content);
			var fields = document.RootElement.GetProperty("actions")[0].GetProperty("returnValue").GetProperty("response").GetProperty("fields");

			string? dataJson = null;
			// Iterate through each field to find the bin data JSON
			foreach (var field in fields.EnumerateArray())
			{
				if (field.GetProperty("name").GetString() == "FlowShowTableFromJson1")
				{
					dataJson = field.GetProperty("inputs")[3].GetProperty("value").GetString();
					break;
				}
			}

			var binDays = new List<BinDay>();
			if (!string.IsNullOrWhiteSpace(dataJson))
			{
				using var dataDocument = JsonDocument.Parse(dataJson!);

				// Iterate through each bin day, and create a new bin day object
				foreach (var row in dataDocument.RootElement.EnumerateArray())
				{
					var service = row.GetProperty("col1").GetString()!;
					var dateString = row.GetProperty("col2").GetString()!;

					var date = dateString.ParseDateInferringYear("ddd, d MMMM");

					var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, service);

					var binDay = new BinDay
					{
						Date = date,
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
	/// Creates the initial client-side request for session initialization.
	/// </summary>
	private static ClientSideRequest CreateInitialRequest() =>
		new()
		{
			RequestId = 1,
			Url = "https://community.westoxon.gov.uk/s/waste-collection-enquiry",
			Method = "GET",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
			},
		};

	/// <summary>
	/// Extracts the session tokens from the initial page response.
	/// </summary>
	private static (string cookie, string fwuid, string appId) ExtractSessionTokens(ClientSideResponse clientSideResponse)
	{
		var setCookieHeader = clientSideResponse.Headers["set-cookie"];
		var cookie = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);
		var fwuid = FwuidRegex().Match(clientSideResponse.Content).Groups["fwuid"].Value;
		var appId = AppIdRegex().Match(clientSideResponse.Content).Groups["appId"].Value;
		return (cookie, fwuid, appId);
	}

	/// <summary>
	/// Extracts the serialized state from the response and creates a navigateFlow response.
	/// </summary>
	private static GetBinDaysResponse CreateNavigateResponse(
		int requestId,
		string action,
		ClientSideResponse clientSideResponse,
		Address address
	)
	{
		var serializedState = GetSerializedState(clientSideResponse.Content);

		var clientSideRequest = CreateNavigateRequest(
			requestId,
			action,
			serializedState,
			clientSideResponse.Options.Metadata["fwuid"],
			clientSideResponse.Options.Metadata["appId"],
			clientSideResponse.Options.Metadata["cookie"],
			address
		);

		var getBinDaysResponse = new GetBinDaysResponse
		{
			NextClientSideRequest = clientSideRequest,
		};

		return getBinDaysResponse;
	}

	/// <summary>
	/// Extracts the serialized encoded state from a Salesforce Aura response.
	/// </summary>
	private static string GetSerializedState(string content)
	{
		using var document = JsonDocument.Parse(content);
		return document.RootElement.GetProperty("actions")[0]
			.GetProperty("returnValue")
			.GetProperty("response")
			.GetProperty("serializedEncodedState")
			.GetString()!;
	}

	/// <summary>
	/// Builds the form-encoded Aura request body with the provided message and context.
	/// </summary>
	private static string BuildAuraFormData(string message, string fwuid, string appId) =>
		ProcessingUtilities.ConvertDictionaryToFormData(new()
		{
			{ "message", message },
			{ "aura.context", $$"""{"mode":"PROD","fwuid":"{{fwuid}}","app":"siteforce:communityApp","loaded":{"APPLICATION@markup://siteforce:communityApp":"{{appId}}"},"dn":[],"globals":{},"uad":true}""" },
			{ "aura.pageURI", "/s/waste-collection-enquiry" },
			{ "aura.token", "null" },
		});

	/// <summary>
	/// Creates a navigateFlow request with the provided action and state.
	/// </summary>
	private static ClientSideRequest CreateNavigateRequest(
		int requestId,
		string action,
		string serializedState,
		string fwuid,
		string appId,
		string cookie,
		Address address
	)
	{
		var fieldsJson = action == "NEXT"
			? $$"""
				[
					{"field": "Property.recordId", "value": "{{address.Uid}}", "isVisible": true},
					{"field": "Property.recordIds", "value": ["{{address.Uid}}"], "isVisible": true},
					{"field": "Property.recordName", "value": "{{address.Property}}", "isVisible": true}
				]
				"""
			: "[]";

		var navigateMessage = $$"""
				{
					"actions": [
						{
							"id": "navigate",
							"descriptor": "aura://FlowRuntimeConnectController/ACTION$navigateFlow",
							"params": {
								"request": {
									"action": "{{action}}",
									"serializedState": "{{serializedState}}",
									"fields": {{fieldsJson}}
								}
							}
						}
					]
				}
				""";

		var messageBody = BuildAuraFormData(navigateMessage, fwuid, appId);

		var clientSideRequest = new ClientSideRequest
		{
			RequestId = requestId,
			// The 'r' parameter is offset by 8 to match the Salesforce Aura framework's expected request sequence numbering
			Url = $"https://community.westoxon.gov.uk/s/sfsites/aura?r={requestId + 8}&aura.FlowRuntimeConnect.navigateFlow=1",
			Method = "POST",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
				{ "content-type", "application/x-www-form-urlencoded;charset=UTF-8" },
				{ "cookie", cookie },
			},
			Body = messageBody,
			Options = new ClientSideOptions
			{
				Metadata =
				{
					{ "fwuid", fwuid },
					{ "appId", appId },
					{ "cookie", cookie },
				},
			},
		};

		return clientSideRequest;
	}
}
