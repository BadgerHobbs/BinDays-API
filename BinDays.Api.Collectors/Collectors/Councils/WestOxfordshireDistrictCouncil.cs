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
			Name = "General Waste",
			Colour = BinColour.Grey,
			Keys =
			[
				"Refuse",
			],
		},
		new()
		{
			Name = "Mixed Recycling Bin",
			Colour = BinColour.Blue,
			Keys =
			[
				"360 Litre Recycling",
			],
		},
		new()
		{
			Name = "Glass Recycling Box",
			Colour = BinColour.Black,
			Keys =
			[
				"Black Recycling Box",
			],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Food Waste Caddy",
			Colour = BinColour.Brown,
			Keys =
			[
				"Food",
			],
		},
	];

	[GeneratedRegex(@"""fwuid"":""(?<fwuid>[^""]+)""")]
	private static partial Regex FwuidRegex();

	[GeneratedRegex(@"APPLICATION@markup://siteforce:communityApp"":""(?<appId>[^""]+)""")]
	private static partial Regex AppIdRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://community.westoxon.gov.uk/s/waste-collection-enquiry",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		else if (clientSideResponse.RequestId == 1)
		{
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var fwuid = FwuidRegex().Match(clientSideResponse.Content).Groups["fwuid"].Value;
			var appId = AppIdRegex().Match(clientSideResponse.Content).Groups["appId"].Value;

			var auraContext = BuildAuraContext(fwuid, appId);

			var lookupMessage = new
			{
				actions = new[]
				{
					new
					{
						id = "lookup",
						descriptor = "aura://LookupController/ACTION$lookup",
						callingDescriptor = "UNKNOWN",
						@params = new
						{
							objectApiName = "Case",
							fieldApiName = "Property__c",
							pageParam = 1,
							pageSize = 50,
							q = postcode,
							searchType = "TypeAhead",
							targetApiName = "Property__c",
							body = new
							{
								sourceRecord = new
								{
									apiName = "Case",
									fields = new { Id = (string?)null },
								},
							},
						},
					},
				},
			};

			var messageBody = $"message={JsonSerializer.Serialize(lookupMessage)}&aura.context={auraContext}&aura.pageURI=/s/waste-collection-enquiry&aura.token=null";

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
			var lookupResults = actions[0].GetProperty("returnValue").GetProperty("lookupResults").GetProperty("Property__c").GetProperty("records");

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
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://community.westoxon.gov.uk/s/waste-collection-enquiry",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		else if (clientSideResponse.RequestId == 1)
		{
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var fwuid = FwuidRegex().Match(clientSideResponse.Content).Groups["fwuid"].Value;
			var appId = AppIdRegex().Match(clientSideResponse.Content).Groups["appId"].Value;
			var auraContext = BuildAuraContext(fwuid, appId);

			var startFlowMessage = new
			{
				actions = new[]
				{
					new
					{
						id = "start",
						descriptor = "aura://FlowRuntimeConnectController/ACTION$startFlow",
						callingDescriptor = "UNKNOWN",
						@params = new
						{
							flowDevName = "WebFormAlloyWasteCollectionEnquiry",
							arguments = "[{\"name\":\"vClientCode\",\"type\":\"String\",\"supportsRecordId\":false,\"value\":\"WOD\"}]",
							enableTrace = false,
							enableRollbackMode = false,
							debugAsUserId = string.Empty,
							useLatestSubflow = false,
							isBuilderDebug = false,
						},
					},
				},
			};

			var messageBody = $"message={JsonSerializer.Serialize(startFlowMessage)}&aura.context={auraContext}&aura.pageURI=/s/waste-collection-enquiry&aura.token=null";

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
			var serializedState = JsonDocument.Parse(clientSideResponse.Content)
				.RootElement.GetProperty("actions")[0]
				.GetProperty("returnValue")
				.GetProperty("response")
				.GetProperty("serializedEncodedState")
				.GetString()!;

			var clientSideRequest = CreateNavigateRequest(
				3,
				"NEXT",
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
		else if (clientSideResponse.RequestId == 3)
		{
			var serializedState = JsonDocument.Parse(clientSideResponse.Content)
				.RootElement.GetProperty("actions")[0]
				.GetProperty("returnValue")
				.GetProperty("response")
				.GetProperty("serializedEncodedState")
				.GetString()!;

			var clientSideRequest = CreateNavigateRequest(
				4,
				"CONTINUE_AFTER_COMMIT",
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
		else if (clientSideResponse.RequestId == 4)
		{
			var serializedState = JsonDocument.Parse(clientSideResponse.Content)
				.RootElement.GetProperty("actions")[0]
				.GetProperty("returnValue")
				.GetProperty("response")
				.GetProperty("serializedEncodedState")
				.GetString()!;

			var clientSideRequest = CreateNavigateRequest(
				5,
				"CONTINUE_AFTER_COMMIT",
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
		else if (clientSideResponse.RequestId == 5)
		{
			using var document = JsonDocument.Parse(clientSideResponse.Content);
			var fields = document.RootElement.GetProperty("actions")[0].GetProperty("returnValue").GetProperty("response").GetProperty("fields");

			string? dataJson = null;
			foreach (var field in fields.EnumerateArray())
			{
				if (field.GetProperty("name").GetString() == "FlowShowTableFromJson1")
				{
					dataJson = field
						.GetProperty("inputs")[3]
						.GetProperty("value")
						.GetString();
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
	/// Builds the aura context payload for Salesforce requests.
	/// </summary>
	private static string BuildAuraContext(string fwuid, string appId)
	{
		return $$"""
{"mode":"PROD","fwuid":"{{fwuid}}","app":"siteforce:communityApp","loaded":{"APPLICATION@markup://siteforce:communityApp":"{{appId}}"},"dn":[],"globals":{},"uad":true}
""";
	}

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
		var fields = action == "NEXT"
			? new object[]
			{
				new
				{
					field = "FlowStages1.currentStage",
					value = (string?)null,
					isVisible = false,
				},
				new
				{
					field = "FlowStages1.stages",
					value = (string?)null,
					isVisible = false,
				},
				new
				{
					field = "Property.recordId",
					value = address.Uid,
					isVisible = true,
				},
				new
				{
					field = "Property.recordIds",
					value = new[]
					{
						address.Uid,
					},
					isVisible = true,
				},
				new
				{
					field = "Property.recordName",
					value = address.Property,
					isVisible = true,
				},
			}
			: [];

		var auraContext = BuildAuraContext(fwuid, appId);

		var navigateMessage = new
		{
			actions = new[]
			{
				new
				{
					id = "navigate",
					descriptor = "aura://FlowRuntimeConnectController/ACTION$navigateFlow",
					callingDescriptor = "UNKNOWN",
					@params = new
					{
						request = new
						{ action,
							 serializedState,
							fields,
							uiElementVisited = true,
							enableTrace = false,
							lcErrors = new { },
							isBuilderDebug = false,
						},
					},
				},
			},
		};

		var messageBody = $"message={JsonSerializer.Serialize(navigateMessage)}&aura.context={auraContext}&aura.pageURI=/s/waste-collection-enquiry&aura.token=null";

		var clientSideRequest = new ClientSideRequest
		{
			RequestId = requestId,
			Url = requestId == 3
				? "https://community.westoxon.gov.uk/s/sfsites/aura?r=11&aura.FlowRuntimeConnect.navigateFlow=1"
				: requestId == 4
					? "https://community.westoxon.gov.uk/s/sfsites/aura?r=12&aura.FlowRuntimeConnect.navigateFlow=1"
					: "https://community.westoxon.gov.uk/s/sfsites/aura?r=13&aura.FlowRuntimeConnect.navigateFlow=1",
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
