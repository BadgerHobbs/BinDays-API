namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
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

	private const string StageId = "AF-Stage-eb382015-001c-415d-beda-84f796dbb167";
	private const string StageName = "Stage 1";
	private const string FormId = "AF-Form-dc8ffbd6-4832-443b-ba3f-5e8d36bf11d4";
	private const string ProcessId = "AF-Process-2289dd06-9a12-4202-ba09-857fe756f6bd";
	private const string FormUri = "sandbox-publish://AF-Process-2289dd06-9a12-4202-ba09-857fe756f6bd/AF-Stage-eb382015-001c-415d-beda-84f796dbb167/definition.json";
	private const string InternalFormUri = "sandbox://AF-Form-dc8ffbd6-4832-443b-ba3f-5e8d36bf11d4";
	private const string FormName = "Collection Day Lookup";
	private const string ProcessName = "Waste - Collection Day Lookup";

	private static readonly Uri FormUrl = new("https://my.middevon.gov.uk/en/AchieveForms/?form_uri=sandbox-publish://AF-Process-2289dd06-9a12-4202-ba09-857fe756f6bd/AF-Stage-eb382015-001c-415d-beda-84f796dbb167/definition.json&redirectlink=%2Fen&cancelRedirectLink=%2Fen&consentMessage=yes");
	private static readonly string FormUrlBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(FormUrl.ToString()));

	private const string AddressLookupId = "64c24c3e7f5bb";
	private const string OrganicLookupId = "641c7ae9b4c96";
	private const string ResidualLookupId = "6423144f50ec0";
	private const string RecyclingLookupId = "642315aacb919";

	/// <summary>
	/// Regex to extract the sid value from HTML content.
	/// </summary>
	[GeneratedRegex(@"sid=([a-f0-9]+)")]
	private static partial Regex SidRegex();

	/// <summary>
	/// Regex to extract the PHP session id from the cookie string.
	/// </summary>
	[GeneratedRegex(@"PHPSESSID=([^;]+)")]
	private static partial Regex SessionIdRegex();

	private sealed record InterimBinDay(string Date, string CollectionItems);

	private static readonly Dictionary<string, string> EnvTokens = new()
	{
		{ "CollectionDetailsNotAvailable", "We are unable to display your waste & recycling collection details. Please contact Customer Services for assistance." },
		{ "CouncilName", "Mid Devon District Council" },
		{ "FoodWaste", "Your next Blue Food Caddy Collection" },
		{ "GardenWaste", "Your next Chargeable Garden Waste Collection:" },
		{ "Recycling", "Your next Recycling Collection:" },
		{ "ResidualWaste", "Your next Residual Waste Collection (Black Bin/Seagull Sack):" },
		{ "Environment", "Production" },
		{ "Welfare_Team", "USERGROUP-87421c7a-6d94-4d8f-8b85-430d877cc71b" },
		{ "CSA_Officer_Team", "USERGROUP-f1646674-9489-403d-a774-ad2e2fa2206d" },
		{ "govtechAddressSource", "NORTHGATE" },
		{ "govtechPostcodeFinderLink", "<div><em>If you do not know the postcode, you can search for it on the </em><a href=\"https://www.royalmail.com/find-a-postcode\" target=\"_blank\" rel=\"noopener\"><em>Royal Mail website</em></a> <em>(link will open in a new window)</em></div>" },
		{ "govtechAreaName", "Mid Devon" },
		{ "govtechCouncilName", "Mid Devon District Council" },
		{ "govtechSPDName", "Single Person Discount" },
		{ "govtechNextOrContinue", "Next" },
		{ "cTOriginatorIdentificationNumber", "950235" },
		{ "nDROriginatorIdentificationNumber", "950235" },
		{ "serviceUserName", "Mid Devon District Council, Phoenix House, Phoenix Lane, Tiverton, EX16 6PP" },
		{ "govtechDDNotifyDays", "10" },
		{ "govtechSendDDConfirmEmail", "true" },
		{ "govtechCTCPYFormatOptions", "true" },
		{ "govtechRSPDEnabled", "true" },
		{ "govtechRSPDInProgress", "false" },
		{ "govtechCTMOVEReminderEmail", "true" },
		{ "govtechCTMOVEAdvanceCheck", "true" },
		{ "govtechCTMOVEAdvanceDays", "14" },
		{ "govtechCTMOVEBenefitQuestions", "No" },
		{ "govtechCTMoveLink", "https://middevondc-self.achieveservice.com/service/Council_Tax___Change_of_address" },
		{ "govtechCTMOVENewOwnerMandatory", "false" },
		{ "govtechCouncilTaxSupportName", "N/A" },
		{ "govtechCTCOTStudentQuestion", "false" },
		{ "govtechNDRCPYFormatOptions", "true" },
		{ "NDRCPYFormTypeNG", "true" },
		{ "NDRMIFormTypeNG", "true" },
		{ "NDRMOFormTypeNG", "true" },
		{ "NDRDDFormTypeNG", "true" },
		{ "NDREBILLFormTypeNG", "true" },
	};

	private readonly IReadOnlyCollection<Bin> _binTypes = [
		new()
		{
			Name = "Residual Waste",
			Colour = BinColour.Black,
			Keys = [ "Residual", "Black Bin", "Seagull Sack", "Black Sack" ],
			Type = BinType.Bin,
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Blue,
			Keys = [ "Food Caddy", "Food Waste" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden Waste" ],
			Type = BinType.Bin,
		},
		new()
		{
			Name = "Recycling (Black Box)",
			Colour = BinColour.Black,
			Keys = [ "Black Recycling Box", "Black Box" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Recycling (Green Box)",
			Colour = BinColour.Green,
			Keys = [ "Green Recycling Box", "Green Box", "Recycling Boxes" ],
			Type = BinType.Box,
		},
	];

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		var formattedPostcode = ProcessingUtilities.FormatPostcode(postcode);

		if (clientSideResponse == null)
		{
			return new GetAddressesResponse
			{
				NextClientSideRequest = CreateInitialRequest(1),
			};
		}

		if (clientSideResponse.RequestId == 1)
		{
			var metadata = BuildBaseMetadata(clientSideResponse);

			return new GetAddressesResponse
			{
				NextClientSideRequest = CreateNextRefRequest(2, metadata),
			};
		}

		if (clientSideResponse.RequestId == 2)
		{
			var metadata = UpdateMetadataWithNextRef(clientSideResponse);

			return new GetAddressesResponse
			{
				NextClientSideRequest = CreateRunLookupRequest(
					requestId: 3,
					lookupId: AddressLookupId,
					postcode: formattedPostcode,
					metadata: metadata)
			};
		}

		if (clientSideResponse.RequestId == 3)
		{
			var addresses = ParseAddresses(clientSideResponse, formattedPostcode);

			return new GetAddressesResponse
			{
				Addresses = addresses,
			};
		}

		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <inheritdoc/>
	public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
	{
		var addressPostcode = address.Postcode ?? string.Empty;

		if (clientSideResponse == null)
		{
			return new GetBinDaysResponse
			{
				NextClientSideRequest = CreateInitialRequest(1),
			};
		}

		if (clientSideResponse.RequestId == 1)
		{
			var metadata = BuildBaseMetadata(clientSideResponse);

			return new GetBinDaysResponse
			{
				NextClientSideRequest = CreateNextRefRequest(2, metadata),
			};
		}

		if (clientSideResponse.RequestId == 2)
		{
			var metadata = UpdateMetadataWithNextRef(clientSideResponse);

			return new GetBinDaysResponse
			{
				NextClientSideRequest = CreateRunLookupRequest(
					requestId: 3,
					lookupId: OrganicLookupId,
					postcode: addressPostcode,
					metadata: metadata,
					address: address)
			};
		}

		if (clientSideResponse.RequestId == 3)
		{
			var metadata = UpdateCookies(clientSideResponse);
			var interimBinDays = GetInterimBinDays(metadata);
			var organicBinDay = ParseCollections(clientSideResponse);

			interimBinDays.AddRange(organicBinDay);
			StoreInterimBinDays(metadata, interimBinDays);

			var organicDate = organicBinDay.First().Date;

			return new GetBinDaysResponse
			{
				NextClientSideRequest = CreateRunLookupRequest(
					requestId: 4,
					lookupId: ResidualLookupId,
					postcode: addressPostcode,
					metadata: metadata,
					address: address,
					organicCollection: organicDate)
			};
		}

		if (clientSideResponse.RequestId == 4)
		{
			var metadata = UpdateCookies(clientSideResponse);
			var interimBinDays = GetInterimBinDays(metadata);
			var residualBinDay = ParseCollections(clientSideResponse);

			interimBinDays.AddRange(residualBinDay);
			StoreInterimBinDays(metadata, interimBinDays);

			var organicDate = interimBinDays.First().Date;
			var residualDate = residualBinDay.First().Date;

			return new GetBinDaysResponse
			{
				NextClientSideRequest = CreateRunLookupRequest(
					requestId: 5,
					lookupId: RecyclingLookupId,
					postcode: addressPostcode,
					metadata: metadata,
					address: address,
					organicCollection: organicDate,
					residualCollection: residualDate,
					foodCollection: organicDate)
			};
		}

		if (clientSideResponse.RequestId == 5)
		{
			var metadata = UpdateCookies(clientSideResponse);
			var interimBinDays = GetInterimBinDays(metadata);
			var recyclingBinDay = ParseCollections(clientSideResponse);

			interimBinDays.AddRange(recyclingBinDay);

			var binDays = BuildBinDays(interimBinDays, address);

			return new GetBinDaysResponse
			{
				BinDays = binDays,
			};
		}

		throw new InvalidOperationException("Invalid client-side request.");
	}

	private static ClientSideRequest CreateInitialRequest(int requestId)
	{
		return new ClientSideRequest
		{
			RequestId = requestId,
			Url = FormUrl.ToString(),
			Method = "GET",
			Headers = new Dictionary<string, string>
			{
				{ "User-Agent", Constants.UserAgent },
				{ "cookie", "CookiesAccepted=true" }
			},
		};
	}

	private static ClientSideRequest CreateNextRefRequest(int requestId, Dictionary<string, string> metadata)
	{
		var sid = metadata["sid"];
		var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

		return new ClientSideRequest
		{
			RequestId = requestId,
			Url = $"https://my.middevon.gov.uk/api/nextref?_={timestamp}&sid={sid}",
			Method = "GET",
			Headers = new Dictionary<string, string>
			{
				{ "X-Requested-With", "XMLHttpRequest" },
				{ "cookie", metadata["cookie"] },
				{ "User-Agent", Constants.UserAgent },
			},
			Options = new ClientSideOptions
			{
				Metadata = metadata
			},
		};
	}

	private static ClientSideRequest CreateRunLookupRequest(
		int requestId,
		string lookupId,
		string postcode,
		Dictionary<string, string> metadata,
		Address? address = null,
		string organicCollection = "",
		string residualCollection = "",
		string foodCollection = "",
		string recyclingCollection = "")
	{
		var sid = metadata["sid"];
		var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		var reference = metadata["reference"];
		var csrfToken = metadata["csrf_token"];
		var sessionId = metadata["sessionId"];
		var formattedPostcode = ProcessingUtilities.FormatPostcode(postcode);

		var requestBody = BuildLookupPayload(
			formattedPostcode,
			address,
			sessionId,
			csrfToken,
			reference,
			organicCollection,
			residualCollection,
			foodCollection,
			recyclingCollection);

		var url = $"https://my.middevon.gov.uk/apibroker/runLookup?id={lookupId}&repeat_against=&noRetry=false&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&_={timestamp}&sid={sid}";

		return new ClientSideRequest
		{
			RequestId = requestId,
			Url = url,
			Method = "POST",
			Headers = new Dictionary<string, string>
			{
				{ "Content-Type", "application/json" },
				{ "X-Requested-With", "XMLHttpRequest" },
				{ "cookie", metadata["cookie"] },
				{ "User-Agent", Constants.UserAgent },
				{ "Referer", FormUrl.ToString() },
			},
			Body = requestBody,
			Options = new ClientSideOptions
			{
				Metadata = metadata
			},
		};
	}

	private static Dictionary<string, string> BuildBaseMetadata(ClientSideResponse clientSideResponse)
	{
		var setCookieHeader = clientSideResponse.Headers.TryGetValue("set-cookie", out var cookieHeader)
			? cookieHeader
			: string.Empty;

		var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

		var sid = SidRegex().Match(clientSideResponse.Content).Groups[1].Value;
		var sessionId = SessionIdRegex().Match(requestCookies).Groups[1].Value;

		return new Dictionary<string, string>
		{
			{ "cookie", requestCookies },
			{ "sid", sid },
			{ "sessionId", sessionId },
		};
	}

	private static Dictionary<string, string> UpdateMetadataWithNextRef(ClientSideResponse clientSideResponse)
	{
		var metadata = new Dictionary<string, string>(clientSideResponse.Options.Metadata);
		metadata = UpdateCookies(clientSideResponse, metadata);

		using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
		var data = jsonDoc.RootElement.GetProperty("data");

		var reference = data.GetProperty("reference").GetString()!;
		var csrfToken = data.GetProperty("csrfToken").GetString()!;

		metadata["reference"] = reference;
		metadata["csrf_token"] = csrfToken;

		return metadata;
	}

	private static Dictionary<string, string> UpdateCookies(ClientSideResponse clientSideResponse, Dictionary<string, string>? metadata = null)
	{
		var updatedMetadata = metadata == null
			? new Dictionary<string, string>(clientSideResponse.Options.Metadata)
			: new Dictionary<string, string>(metadata);

		if (clientSideResponse.Headers.TryGetValue("set-cookie", out var setCookieHeader))
		{
			var newCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			if (!string.IsNullOrWhiteSpace(newCookies))
			{
				var mergedCookies = MergeCookies(updatedMetadata.TryGetValue("cookie", out var existingCookies) ? existingCookies : string.Empty, newCookies);

				updatedMetadata["cookie"] = mergedCookies;

				var newSessionId = SessionIdRegex().Match(mergedCookies).Groups[1].Value;

				if (!string.IsNullOrWhiteSpace(newSessionId))
				{
					updatedMetadata["sessionId"] = newSessionId;
				}
			}
		}

		return updatedMetadata;
	}

	private static string BuildLookupPayload(
		string postcode,
		Address? address,
		string sessionId,
		string csrfToken,
		string reference,
		string organicCollection,
		string residualCollection,
		string foodCollection,
		string recyclingCollection)
	{
		var formValues = CreateFormValues(
			postcode,
			address,
			organicCollection,
			residualCollection,
			foodCollection,
			recyclingCollection);

		var tokens = BuildTokens(sessionId, csrfToken, reference);

		var payload = new Dictionary<string, object?>
		{
			{ "stopOnFailure", true },
			{ "usePHPIntegrations", true },
			{ "stage_id", StageId },
			{ "stage_name", StageName },
			{ "formId", FormId },
			{ "formValues", formValues },
			{ "isPublished", true },
			{ "formName", FormName },
			{ "processId", ProcessId },
			{ "tokens", tokens },
			{ "env_tokens", EnvTokens },
			{ "site", FormUrlBase64 },
			{ "processName", ProcessName },
			{ "created", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) },
			{ "reference", reference },
			{ "formUri", FormUri },
		};

		return JsonSerializer.Serialize(payload);
	}

	private static Dictionary<string, object?> CreateFormValues(
		string postcode,
		Address? address,
		string organicCollection,
		string residualCollection,
		string foodCollection,
		string recyclingCollection)
	{
		var addressDisplay = address?.Property ?? string.Empty;
		var addressValue = address?.Uid ?? string.Empty;

		var yourAddress = new Dictionary<string, object?>
		{
			{ "postcode_search", CreateField("postcode_search", "text", "AF-Field-7ad337fc-1c73-4658-8c4a-4de6079621b2", postcode, "AF-Section-4e9d63cb-4544-4b8e-8b87-eec4a70b2584", "Enter your Postcode", "root/postcode_search", isMandatory: true) },
			{ "listAddress", CreateField("listAddress", "select", "AF-Field-c2bab0b9-acaa-46a0-a758-8f87e542c71e", addressValue, "AF-Section-4e9d63cb-4544-4b8e-8b87-eec4a70b2584", "Select Address", "root/listAddress", isMandatory: true, valueLabel: string.IsNullOrEmpty(addressValue) ? [] : new[] { addressDisplay }, hash: "24590B7CF2CBCF6745A3AED7CFF51EA4E2A000011F2A6ED5D454BA9933904C1B", logId: "9fae08ed-69e9-4e17-9851-9b8a62d0a2c3") },
			{ "displayAddress", CreateField("displayAddress", "text", "AF-Field-18d8a3a7-2a4c-47fb-bcee-2c57b3388c30", addressDisplay, "AF-Section-4e9d63cb-4544-4b8e-8b87-eec4a70b2584", "DisplayAddress", "root/displayAddress", isHiddenInternal: true) },
			{ "uprn", CreateField("uprn", "text", "AF-Field-74979d02-7087-4649-8fe7-ff351777a0ab", addressValue, "AF-Section-4e9d63cb-4544-4b8e-8b87-eec4a70b2584", "uprn", "root/uprn", isHiddenInternal: true) },
			{ "postcode", CreateField("postcode", "text", "AF-Field-99f60018-01d7-4623-9623-db7aef0e0a62", string.Empty, "AF-Section-4e9d63cb-4544-4b8e-8b87-eec4a70b2584", "postcode", "root/postcode", isHiddenInternal: true) },
			{ "loggedInUser", CreateField("loggedInUser", "text", "AF-Field-71741d89-403d-47dd-9e62-ffbd52d0b756", string.Empty, "AF-Section-4e9d63cb-4544-4b8e-8b87-eec4a70b2584", "Logged In User", "root/loggedInUser", isHiddenInternal: true) },
			{ "OrganicCollections", CreateField("OrganicCollections", "select", "AF-Field-9f49b399-86a3-45e2-a670-e3719a9b8d75", organicCollection, "AF-Section-4e9d63cb-4544-4b8e-8b87-eec4a70b2584", "OrganicCollections", "root/OrganicCollections", valueLabel: string.IsNullOrEmpty(organicCollection) ? [] : new[] { organicCollection }, hash: "01468BBE9D340867528E4B0CA87D510F5C19D33D58629915B07D0AD171A06072", logId: "a592351e-bcca-4b1d-b5ef-a410d3d731a3", isHiddenInternal: true) },
			{ "ResidualCollections", CreateField("ResidualCollections", "select", "AF-Field-12b20af8-6f0d-4b04-9c8d-84b9f5493906", residualCollection, "AF-Section-4e9d63cb-4544-4b8e-8b87-eec4a70b2584", "ResidualCollections", "root/ResidualCollections", valueLabel: string.IsNullOrEmpty(residualCollection) ? [] : new[] { residualCollection }, hash: "BB7AB568F366F8CD43D7F111401797B7AEA8DA49A06A0C4D4D3F8F8F4650BB3B", logId: "f67dc5d3-5acf-4b7b-b3b2-0fe3e2205532", isHiddenInternal: true) },
			{ "foodCollections", CreateField("foodCollections", "select", "AF-Field-257ea732-3255-4a20-8662-dca10c96e6da", foodCollection, "AF-Section-4e9d63cb-4544-4b8e-8b87-eec4a70b2584", "FoodCollections", "root/foodCollections", valueLabel: string.IsNullOrEmpty(foodCollection) ? [] : new[] { foodCollection }, hash: "D7EBA9A101846494079755CCD9BD1186D7CB31411C4E0357E24A25EBEE1EEBB6", logId: "851e4001-01e0-46f6-8558-c916084979c5", isHiddenInternal: true) },
			{ "RecyclingCollections", CreateField("RecyclingCollections", "select", "AF-Field-b2104a22-bba4-4f78-bf62-ccddc2a0bb48", recyclingCollection, "AF-Section-4e9d63cb-4544-4b8e-8b87-eec4a70b2584", "RecyclingCollections", "root/RecyclingCollections", valueLabel: string.IsNullOrEmpty(recyclingCollection) ? [] : new[] { recyclingCollection }, hash: "A983A275BFD8146A2917C87DAE3ED28E56F067E8A58C038AFE6A424BBE2A12EA", logId: "8f273562-1605-4c2a-a3e9-2e3881ba2517", isHiddenInternal: true) },
			{ "CombiResidualCollections", CreateField("CombiResidualCollections", "select", "AF-Field-a27661d1-e53e-4056-b9e4-aac7890998d7", string.Empty, "AF-Section-4e9d63cb-4544-4b8e-8b87-eec4a70b2584", "CombiResidualCollections", "root/CombiResidualCollections", hash: "0D157C25924C85D07A1E7B252BC3BA7CC078615C1EB4BD751EF0A5C8FF2CEFF3", logId: "46b299d0-fb72-49b1-a499-e5f81c8d4d6b", isHiddenInternal: true) },
			{ "CombiRecyclingCollections", CreateField("CombiRecyclingCollections", "select", "AF-Field-e6bc4c81-d239-4bb9-8e1a-46d2a4754c19", string.Empty, "AF-Section-4e9d63cb-4544-4b8e-8b87-eec4a70b2584", "CombiRecyclingCollections", "root/CombiRecyclingCollections", hash: "D735FFE5F9300FE8B24958AF09B924F7CE13EF5614A91A4594EFBC1EE87E65D6", logId: "db4a044a-aaea-4e8c-8710-5670956bdb70", isHiddenInternal: true) },
			{ "CombiOrganicCollections", CreateField("CombiOrganicCollections", "select", "AF-Field-c3f5f0d4-f29e-432c-8129-19a25bb96188", string.Empty, "AF-Section-4e9d63cb-4544-4b8e-8b87-eec4a70b2584", "CombiOrganicCollections", "root/CombiOrganicCollections", hash: "71E6C87D496253FF6AF8AC6FC4BE9C6D952233159FDA0698B2BF6D3D005FF19A", logId: "31330a34-a154-4839-80ff-700c0c8ff5a3", isHiddenInternal: true) },
			{ "combiFoodCollections", CreateField("combiFoodCollections", "select", "AF-Field-11922dce-e915-4352-b415-380b7e88e831", string.Empty, "AF-Section-4e9d63cb-4544-4b8e-8b87-eec4a70b2584", "CombiFoodCollections", "root/combiFoodCollections", hash: "4075404FE94DAF6DCDA219D58B3758FBA9592DF71D0FFD31C1EE618B598768F3", logId: "c6c5c899-5c11-468f-91ec-f31a28528b45", isHiddenInternal: true) },
		};

		var collectionDetails = new Dictionary<string, object?>
		{
			{ "rdoRegularEmail", CreateField("rdoRegularEmail", "radio", "AF-Field-22a3992a-82ff-4ce2-acbe-9100b93172f0", string.Empty, "AF-Section-0d152b6c-8c4c-46f9-92c5-97a83362ef13", "Would you like a regular email to remind you what to put out each week?", "root/rdoRegularEmail", isMandatory: true, valueLabel: Array.Empty<string>()) },
			{ "Email_Address", CreateField("Email_Address", "text", "AF-Field-031d8617-e4e9-4c6b-b4b6-310ed580ca4f", string.Empty, "AF-Section-0d152b6c-8c4c-46f9-92c5-97a83362ef13", "EmailAddress", "root/Email_Address", isMandatory: true) },
			{ "subscriptionDetails", CreateField("subscriptionDetails", "select", "AF-Field-27459cd6-5f64-4a56-9c94-41555bd22d45", string.Empty, "AF-Section-0d152b6c-8c4c-46f9-92c5-97a83362ef13", "subscriptionDetails", "root/subscriptionDetails", valueLabel: Array.Empty<string>(), isHiddenInternal: true) },
			{ "RequestPlatform", CreateField("RequestPlatform", "text", "AF-Field-531355c8-6be4-4a10-8d01-ca954e0164fd", string.Empty, "AF-Section-0d152b6c-8c4c-46f9-92c5-97a83362ef13", "Product", "root/RequestPlatform", isHiddenInternal: true) },
			{ "collectionDay", CreateField("collectionDay", "text", "AF-Field-29a8751c-3fca-4810-a291-3cf370a7aafb", string.Empty, "AF-Section-0d152b6c-8c4c-46f9-92c5-97a83362ef13", "CollectionDay", "root/collectionDay", isHiddenInternal: true) },
			{ "residualWasteCycle", CreateField("residualWasteCycle", "select", "AF-Field-d9870166-68b1-4b83-856d-67d4e7074beb", string.Empty, "AF-Section-0d152b6c-8c4c-46f9-92c5-97a83362ef13", "Residual Waste Cycle", "root/residualWasteCycle", valueLabel: Array.Empty<string>(), isHiddenInternal: true) },
			{ "organicWasteCycle", CreateField("organicWasteCycle", "select", "AF-Field-332a552c-894c-4cea-a4e8-baf333cd958b", string.Empty, "AF-Section-0d152b6c-8c4c-46f9-92c5-97a83362ef13", "Organic Waste Cycle", "root/organicWasteCycle", valueLabel: Array.Empty<string>(), isHiddenInternal: true) },
			{ "txtWasteCalendar", CreateField("txtWasteCalendar", "text", "AF-Field-a4b993b4-e0c2-4dcf-a23b-2944906289de", string.Empty, "AF-Section-0d152b6c-8c4c-46f9-92c5-97a83362ef13", "Waste Calendar", "root/txtWasteCalendar", isHiddenInternal: true) },
			{ "printedCalendar", CreateField("printedCalendar", "radio", "AF-Field-239e4d22-b11d-452e-a156-839538b262db", string.Empty, "AF-Section-0d152b6c-8c4c-46f9-92c5-97a83362ef13", "Would you like a calendar emailed or posted to you?", "root/printedCalendar", valueLabel: Array.Empty<string>()) },
			{ "calendarEmailAddress", CreateField("calendarEmailAddress", "text", "AF-Field-d397f6d8-bdf6-491f-a79f-be864e6c684e", string.Empty, "AF-Section-0d152b6c-8c4c-46f9-92c5-97a83362ef13", "Email Address for Calendar", "root/calendarEmailAddress", isMandatory: true) },
			{ "prevCalendarRequests", CreateField("prevCalendarRequests", "subform", "AF-Field-cba86eaa-7632-4105-bc7b-c3d595463273", string.Empty, "AF-Section-0d152b6c-8c4c-46f9-92c5-97a83362ef13", "Previous Calendar Requests", "root/prevCalendarRequests", isRepeatable: true) },
			{ "countOfOpenReques", CreateField("countOfOpenReques", "select", "AF-Field-688b1ad0-76a6-4f01-9831-fafb626db1ee", string.Empty, "AF-Section-0d152b6c-8c4c-46f9-92c5-97a83362ef13", "Count of Open Requests", "root/countOfOpenReques", valueLabel: Array.Empty<string>(), isHiddenInternal: true) },
			{ "staticZero", CreateField("staticZero", "number", "AF-Field-79421d4d-e38c-4442-a135-c5fc6986cb9a", string.Empty, "AF-Section-0d152b6c-8c4c-46f9-92c5-97a83362ef13", "staticZero", "root/staticZero", isMandatory: true, isHiddenInternal: true) },
			{ "caseRef", CreateField("caseRef", "text", "AF-Field-4a3656d7-c7c6-4d63-855b-53c02299cb82", string.Empty, "AF-Section-0d152b6c-8c4c-46f9-92c5-97a83362ef13", "CaseRef", "root/caseRef", isHiddenInternal: true) },
		};

		return new Dictionary<string, object?>
		{
			{ "Your Address", yourAddress },
			{ "Collection Details", collectionDetails },
		};
	}

	private static Dictionary<string, object?> CreateField(
		string name,
		string type,
		string id,
		string value,
		string sectionId,
		string label,
		string path,
		bool isMandatory = false,
		bool isHidden = false,
		bool isHiddenInternal = false,
		bool isRepeatable = false,
		object? valueLabel = null,
		string hash = "",
		string? logId = null)
	{
		var field = new Dictionary<string, object?>
		{
			{ "name", name },
			{ "type", type },
			{ "id", id },
			{ "value_changed", true },
			{ "section_id", sectionId },
			{ "label", label },
			{ "value_label", valueLabel ?? (type is "select" or "radio" ? Array.Empty<string>() : string.Empty) },
			{ "hasOther", false },
			{ "value", value },
			{ "path", path },
			{ "valid", string.Empty },
			{ "totals", string.Empty },
			{ "suffix", string.Empty },
			{ "prefix", string.Empty },
			{ "summary", string.Empty },
			{ "hidden", isHidden },
			{ "_hidden", isHiddenInternal },
			{ "isSummary", false },
			{ "staticMap", false },
			{ "isMandatory", isMandatory },
			{ "isRepeatable", isRepeatable },
			{ "currencyPrefix", string.Empty },
			{ "decimalPlaces", string.Empty },
			{ "hash", hash },
		};

		if (!string.IsNullOrWhiteSpace(logId))
		{
			field["log_id"] = logId;
		}

		return field;
	}

	private static Dictionary<string, object?> BuildTokens(string sessionId, string csrfToken, string reference)
	{
		var caseRef = reference.StartsWith("FS", StringComparison.Ordinal)
			? reference.Replace("FS", "FS-Case-", StringComparison.Ordinal)
			: reference;

		return new Dictionary<string, object?>
		{
			{ "port", string.Empty },
			{ "host", FormUrl.Host },
			{ "site_url", FormUrl.ToString() },
			{ "site_path", FormUrl.AbsolutePath },
			{ "site_origin", FormUrl.GetLeftPart(UriPartial.Authority) },
			{ "user_agent", Constants.UserAgent },
			{ "site_protocol", FormUrl.Scheme },
			{ "session_id", sessionId },
			{ "product", "Self" },
			{ "formLanguage", "en" },
			{ "isAuthenticated", false },
			{ "api_url", "https://my.middevon.gov.uk/apibroker/" },
			{ "transactionReference", string.Empty },
			{ "transaction_status", string.Empty },
			{ "published", true },
			{ "sectionLength", 2 },
			{ "formUri", InternalFormUri },
			{ "publishUri", FormUri },
			{ "formId", FormId },
			{ "topFormId", FormId },
			{ "parentFormId", FormId },
			{ "formName", FormName },
			{ "topFormName", FormName },
			{ "parentFormName", FormName },
			{ "formDescription", string.Empty },
			{ "topFormDescription", string.Empty },
			{ "parentFormDescription", string.Empty },
			{ "case_ref", caseRef },
			{ "stage_id", StageId },
			{ "processId", ProcessId },
			{ "stage_name", StageName },
			{ "processName", ProcessName },
			{ "stageLength", 2 },
			{ "processDescription", string.Empty },
			{ "processUri", $"sandbox-processes://{ProcessId}" },
			{ "version", "17" },
			{ "csrf_token", csrfToken },
			{ "reference", reference },
			{ "process_prefix", "FS-Case-" },
			{ "ipAddress", string.Empty },
			{ "countryCode", string.Empty },
			{ "countryName", string.Empty },
			{ "regionName", string.Empty },
			{ "cityName", string.Empty },
			{ "zipCode", string.Empty },
			{ "latitude", string.Empty },
			{ "longitude", string.Empty },
			{ "timeZone", string.Empty },
		};
	}

	private static IReadOnlyCollection<Address> ParseAddresses(ClientSideResponse clientSideResponse, string postcode)
	{
		using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
		var rows = jsonDoc.RootElement.GetProperty("integration").GetProperty("transformed").GetProperty("rows_data");

		var addresses = new List<Address>();

		foreach (var property in rows.EnumerateObject())
		{
			var data = property.Value;

			var address = new Address
			{
				Property = data.GetProperty("display").GetString()!,
				Postcode = postcode,
				Uid = data.TryGetProperty("uprn", out var uprnElement)
					? uprnElement.GetString()
					: data.GetProperty("name").GetString(),
				Street = data.TryGetProperty("street", out var streetElement) ? streetElement.GetString() : null,
				Town = data.TryGetProperty("town", out var townElement) ? townElement.GetString() : null,
			};

			addresses.Add(address);
		}

		return addresses;
	}

	private static List<InterimBinDay> ParseCollections(ClientSideResponse clientSideResponse)
	{
		using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
		var rows = jsonDoc.RootElement.GetProperty("integration").GetProperty("transformed").GetProperty("rows_data");

		var binDays = new List<InterimBinDay>();

		foreach (var property in rows.EnumerateObject())
		{
			var data = property.Value;
			var dateString = data.GetProperty("display").GetString()!;
			var collectionItems = data.GetProperty("CollectionItems").GetString()!;

			binDays.Add(new InterimBinDay(dateString, collectionItems));
		}

		return binDays;
	}

	private IReadOnlyCollection<BinDay> BuildBinDays(List<InterimBinDay> interimBinDays, Address address)
	{
		var binDays = new List<BinDay>();

		foreach (var interim in interimBinDays)
		{
			var date = DateOnly.ParseExact(interim.Date, "dd-MMM-yy", CultureInfo.InvariantCulture);
			var bins = ProcessingUtilities.GetMatchingBins(_binTypes, interim.CollectionItems);

			binDays.Add(new BinDay
			{
				Date = date,
				Address = address,
				Bins = bins,
			});
		}

		return ProcessingUtilities.ProcessBinDays(binDays);
	}

	private static List<InterimBinDay> GetInterimBinDays(Dictionary<string, string> metadata)
	{
		if (metadata.TryGetValue("binDays", out var storedBinDays) && !string.IsNullOrWhiteSpace(storedBinDays))
		{
			return JsonSerializer.Deserialize<List<InterimBinDay>>(storedBinDays) ?? [];
		}

		return [];
	}

	private static void StoreInterimBinDays(Dictionary<string, string> metadata, List<InterimBinDay> binDays)
	{
		metadata["binDays"] = JsonSerializer.Serialize(binDays);
	}

	private static string MergeCookies(string existingCookies, string newCookies)
	{
		var cookieDictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		void AddCookiesToDictionary(string cookies)
		{
			if (string.IsNullOrWhiteSpace(cookies))
			{
				return;
			}

			var parts = cookies.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

			foreach (var part in parts)
			{
				var separatorIndex = part.IndexOf('=', StringComparison.Ordinal);

				if (separatorIndex <= 0)
				{
					continue;
				}

				var key = part[..separatorIndex].Trim();
				var value = part[(separatorIndex + 1)..].Trim();

				cookieDictionary[key] = value;
			}
		}

		AddCookiesToDictionary(existingCookies);
		AddCookiesToDictionary(newCookies);

		return string.Join("; ", cookieDictionary.Select(kvp => $"{kvp.Key}={kvp.Value}"));
	}
}
