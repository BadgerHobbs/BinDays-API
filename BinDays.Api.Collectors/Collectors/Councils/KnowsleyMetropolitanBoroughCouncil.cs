namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Collector implementation for Knowsley Metropolitan Borough Council.
/// </summary>
internal sealed class KnowsleyMetropolitanBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Knowsley Metropolitan Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://knowsleytransaction.mendixcloud.com/link/youarebeingredirected?target=bincollectioninformation");

	/// <inheritdoc/>
	public override string GovUkId => "knowsley";

	/// <summary>
	/// The list of bin types for this collector. Each bin maps to a single fixed field name in the
	/// council's Mendix API response (e.g. "NextMaroon"), rather than a free-text service description.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = new("Maroon", "#800000"),
			Keys = [ "NextMaroon" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Grey,
			Keys = [ "NextGrey" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Blue,
			Keys = [ "NextBlue" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Grey,
			Keys = [ "NextFood" ],
			Type = BinType.Caddy,
		},
	];

	/// <summary>
	/// The URL used to establish a Mendix session for the bin collection enquiry form.
	/// </summary>
	private const string _linkUrl = "https://knowsleytransaction.mendixcloud.com/link/youarebeingredirected?target=bincollectioninformation";

	/// <summary>
	/// The URL for the Mendix runtime's JSON-RPC endpoint.
	/// </summary>
	private const string _xasUrl = "https://knowsleytransaction.mendixcloud.com/xas/";

	/// <summary>
	/// The Mendix object type of the deep-link redirect object.
	/// </summary>
	private const string _redirectObjectType = "Service_YouAreBeingRedirected.YouAreBeingRedirected_Redirect";

	/// <summary>
	/// The Mendix microflow that turns the deep-link redirect object into a bin collection enquiry object.
	/// </summary>
	private const string _redirectActionName = "Service_YouAreBeingRedirected.SUB_YouAreBeingRedirected";

	/// <summary>
	/// The Mendix object type of the bin collection enquiry object.
	/// </summary>
	private const string _enquiryObjectType = "OnlineServices.OS_vmBinCollectionEnquiry";

	/// <summary>
	/// The Mendix object type of an address search result.
	/// </summary>
	private const string _addressObjectType = "OnlineServices.OS_vmGeneric_Address";

	/// <summary>
	/// The Mendix runtime operation id for searching addresses by postcode.
	/// </summary>
	private const string _postcodeSearchOperationId = "fyb4rmYj50yyh7ccvFq9DQ";

	/// <summary>
	/// The Mendix runtime operation id for selecting an address and retrieving its bin collection dates.
	/// </summary>
	private const string _selectAddressOperationId = "ueSpXkj+JEegFIWpBS6oQA";

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for establishing a Mendix session
		if (clientSideResponse == null)
		{
			return new GetAddressesResponse { NextClientSideRequest = BuildInitialRequest() };
		}
		// Prepare client-side request for the session data
		else if (clientSideResponse.RequestId == 1)
		{
			return new GetAddressesResponse { NextClientSideRequest = BuildSessionDataRequest(clientSideResponse) };
		}
		// Prepare client-side request for the deep-link redirect microflow
		else if (clientSideResponse.RequestId == 2)
		{
			return new GetAddressesResponse { NextClientSideRequest = BuildRedirectActionRequest(clientSideResponse) };
		}
		// Prepare client-side request for the postcode search
		else if (clientSideResponse.RequestId == 3)
		{
			return new GetAddressesResponse { NextClientSideRequest = BuildPostcodeSearchRequest(clientSideResponse, postcode) };
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 4)
		{
			var addressEntries = ParseAddressEntries(clientSideResponse);

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var addressEntry in addressEntries)
			{
				var address = new Address
				{
					Property = addressEntry.FullAddress,
					Postcode = postcode,
					Uid = addressEntry.Uprn,
				};

				addresses.Add(address);
			}

			return new GetAddressesResponse { Addresses = [.. addresses] };
		}

		// Throw exception for invalid request
		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <inheritdoc/>
	public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
	{
		// Re-fetching the postcode search is required: the Mendix session and every object guid it
		// hands out (including the address's) are minted fresh per session and are not stable across
		// requests, so the search must be redone here to obtain a currently-valid guid for this address.
		if (clientSideResponse == null)
		{
			return new GetBinDaysResponse { NextClientSideRequest = BuildInitialRequest() };
		}
		// Prepare client-side request for the session data
		else if (clientSideResponse.RequestId == 1)
		{
			return new GetBinDaysResponse { NextClientSideRequest = BuildSessionDataRequest(clientSideResponse) };
		}
		// Prepare client-side request for the deep-link redirect microflow
		else if (clientSideResponse.RequestId == 2)
		{
			return new GetBinDaysResponse { NextClientSideRequest = BuildRedirectActionRequest(clientSideResponse) };
		}
		// Prepare client-side request for the postcode search
		else if (clientSideResponse.RequestId == 3)
		{
			return new GetBinDaysResponse { NextClientSideRequest = BuildPostcodeSearchRequest(clientSideResponse, address.Postcode!) };
		}
		// Prepare client-side request for selecting the matching address
		else if (clientSideResponse.RequestId == 4)
		{
			var addressEntries = ParseAddressEntries(clientSideResponse);
			var addressEntry = addressEntries.First(entry => entry.Uprn == address.Uid);

			return new GetBinDaysResponse { NextClientSideRequest = BuildSelectAddressRequest(clientSideResponse, addressEntry) };
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 5)
		{
			return ParseBinDaysResponse(clientSideResponse, address);
		}

		// Throw exception for invalid request
		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <summary>
	/// Builds the client-side request that establishes a Mendix session cookie.
	/// </summary>
	private static ClientSideRequest BuildInitialRequest()
	{
		return new ClientSideRequest
		{
			RequestId = 1,
			Url = _linkUrl,
			Method = "GET",
			Options = new ClientSideOptions
			{
				// We need to trap the 303 to get the Set-Cookie header
				FollowRedirects = false,
			},
		};
	}

	/// <summary>
	/// Appends any cookies set by a response onto an existing cookie header value. The Mendix session
	/// hands out additional cookies (e.g. "xasid", distinct from "__Host-XASID") on the session data
	/// response, which subsequent /xas/ calls require alongside the initial session cookie.
	/// </summary>
	private static string AppendSetCookies(string cookie, ClientSideResponse clientSideResponse)
	{
		if (!clientSideResponse.Headers.TryGetValue("set-cookie", out var setCookieHeader))
		{
			return cookie;
		}

		var newCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

		return $"{cookie}; {newCookies}";
	}

	/// <summary>
	/// Builds the client-side request that fetches the Mendix session data (CSRF token and
	/// deep-link redirect object) for the session established by <see cref="BuildInitialRequest"/>.
	/// </summary>
	private static ClientSideRequest BuildSessionDataRequest(ClientSideResponse clientSideResponse)
	{
		var cookie = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);

		return new ClientSideRequest
		{
			RequestId = 2,
			Url = _xasUrl,
			Method = "POST",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
				{ "content-type", Constants.ApplicationJson },
				{ "cookie", cookie },
			},
			Body = """{"action":"get_session_data","params":{"version":2}}""",
			Options = new ClientSideOptions
			{
				Metadata = { { "cookie", cookie } },
			},
		};
	}

	/// <summary>
	/// Builds the client-side request that runs the deep-link redirect microflow, turning the
	/// redirect object from <see cref="BuildSessionDataRequest"/> into a bin collection enquiry object.
	/// </summary>
	private static ClientSideRequest BuildRedirectActionRequest(ClientSideResponse clientSideResponse)
	{
		var cookie = AppendSetCookies(clientSideResponse.Options.Metadata["cookie"], clientSideResponse);

		using var jsonDocument = JsonDocument.Parse(clientSideResponse.Content);
		var csrfToken = jsonDocument.RootElement.GetProperty("csrftoken").GetString()!;
		var redirectObject = jsonDocument.RootElement.GetProperty("objects").EnumerateArray()
			.First(o => o.GetProperty("objectType").GetString() == _redirectObjectType);
		var redirectGuid = redirectObject.GetProperty("guid").GetString()!;
		var redirectHash = redirectObject.GetProperty("hash").GetString()!;

		var requestBody = $$"""
			{"action":"executeaction","params":{"actionname":"{{_redirectActionName}}","applyto":"selection","guids":["{{redirectGuid}}"]},"objects":[{"attributes":{},"guid":"{{redirectGuid}}","hash":"{{redirectHash}}","objectType":"{{_redirectObjectType}}"}]}
			""";

		return new ClientSideRequest
		{
			RequestId = 3,
			Url = _xasUrl,
			Method = "POST",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
				{ "content-type", Constants.ApplicationJson },
				{ "cookie", cookie },
				{ "x-csrf-token", csrfToken },
			},
			Body = requestBody,
			Options = new ClientSideOptions
			{
				Metadata = { { "cookie", cookie }, { "csrfToken", csrfToken } },
			},
		};
	}

	/// <summary>
	/// Builds the client-side request that searches for addresses matching a postcode, using the bin
	/// collection enquiry object from <see cref="BuildRedirectActionRequest"/>.
	/// </summary>
	private static ClientSideRequest BuildPostcodeSearchRequest(ClientSideResponse clientSideResponse, string postcode)
	{
		var cookie = AppendSetCookies(clientSideResponse.Options.Metadata["cookie"], clientSideResponse);
		var csrfToken = clientSideResponse.Options.Metadata["csrfToken"];

		using var jsonDocument = JsonDocument.Parse(clientSideResponse.Content);
		var enquiryObject = jsonDocument.RootElement.GetProperty("objects").EnumerateArray()
			.First(o => o.GetProperty("objectType").GetString() == _enquiryObjectType);
		var enquiryGuid = enquiryObject.GetProperty("guid").GetString()!;
		var enquiryHash = enquiryObject.GetProperty("hash").GetString()!;
		var enquiryAttributes = enquiryObject.GetProperty("attributes").GetRawText();

		var requestBody = $$$$"""
			{"action":"runtimeOperation","operationId":"{{{{_postcodeSearchOperationId}}}}","params":{"OS_MissedBinEnquiry":{"guid":"{{{{enquiryGuid}}}}"}},"changes":{"{{{{enquiryGuid}}}}":{"EnquiryPostcodeOrStreetName":{"value":"{{{{postcode}}}}"}}},"objects":[{"attributes":{{{{enquiryAttributes}}}},"guid":"{{{{enquiryGuid}}}}","hash":"{{{{enquiryHash}}}}","objectType":"{{{{_enquiryObjectType}}}}"}]}
			""";

		return new ClientSideRequest
		{
			RequestId = 4,
			Url = _xasUrl,
			Method = "POST",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
				{ "content-type", Constants.ApplicationJson },
				{ "cookie", cookie },
				{ "x-csrf-token", csrfToken },
			},
			Body = requestBody,
			Options = new ClientSideOptions
			{
				Metadata =
				{
					{ "cookie", cookie },
					{ "csrfToken", csrfToken },
					{ "enquiryGuid", enquiryGuid },
					{ "enquiryHash", enquiryHash },
					{ "enquiryAttributes", enquiryAttributes },
				},
			},
		};
	}

	/// <summary>
	/// Builds the client-side request that selects a specific address and retrieves its bin
	/// collection dates, using an address entry from <see cref="ParseAddressEntries"/>.
	/// </summary>
	private static ClientSideRequest BuildSelectAddressRequest(
		ClientSideResponse clientSideResponse,
		(string Guid, string Hash, string AttributesRaw, string ChangesRaw, string FullAddress, string Uprn) addressEntry)
	{
		var cookie = AppendSetCookies(clientSideResponse.Options.Metadata["cookie"], clientSideResponse);
		var csrfToken = clientSideResponse.Options.Metadata["csrfToken"];
		var enquiryGuid = clientSideResponse.Options.Metadata["enquiryGuid"];
		var enquiryHash = clientSideResponse.Options.Metadata["enquiryHash"];
		var enquiryAttributes = clientSideResponse.Options.Metadata["enquiryAttributes"];

		var requestBody = $$$$"""
			{"action":"runtimeOperation","operationId":"{{{{_selectAddressOperationId}}}}","params":{"Generic_Address":{"guid":"{{{{addressEntry.Guid}}}}"}},"changes":{"{{{{addressEntry.Guid}}}}":{{{{addressEntry.ChangesRaw}}}}},"objects":[{"attributes":{{{{enquiryAttributes}}}},"guid":"{{{{enquiryGuid}}}}","hash":"{{{{enquiryHash}}}}","objectType":"{{{{_enquiryObjectType}}}}"},{"attributes":{{{{addressEntry.AttributesRaw}}}},"guid":"{{{{addressEntry.Guid}}}}","hash":"{{{{addressEntry.Hash}}}}","objectType":"{{{{_addressObjectType}}}}"}]}
			""";

		return new ClientSideRequest
		{
			RequestId = 5,
			Url = _xasUrl,
			Method = "POST",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
				{ "content-type", Constants.ApplicationJson },
				{ "cookie", cookie },
				{ "x-csrf-token", csrfToken },
			},
			Body = requestBody,
			Options = new ClientSideOptions
			{
				Metadata = { { "enquiryGuid", enquiryGuid } },
			},
		};
	}

	/// <summary>
	/// Parses the postcode search response into the set of matching address entries, each carrying
	/// the Mendix guid/hash/attributes needed to later select that address via <see cref="BuildSelectAddressRequest"/>.
	/// </summary>
	private static List<(string Guid, string Hash, string AttributesRaw, string ChangesRaw, string FullAddress, string Uprn)> ParseAddressEntries(
		ClientSideResponse clientSideResponse)
	{
		using var jsonDocument = JsonDocument.Parse(clientSideResponse.Content);
		var changes = jsonDocument.RootElement.GetProperty("changes");
		var addressObjects = jsonDocument.RootElement.GetProperty("objects").EnumerateArray()
			.Where(o => o.GetProperty("objectType").GetString() == _addressObjectType);

		// Iterate through each address object, and extract its details from the changes
		var addressEntries = new List<(string Guid, string Hash, string AttributesRaw, string ChangesRaw, string FullAddress, string Uprn)>();
		foreach (var addressObject in addressObjects)
		{
			var guid = addressObject.GetProperty("guid").GetString()!;
			var addressChanges = changes.GetProperty(guid);

			addressEntries.Add((
				guid,
				addressObject.GetProperty("hash").GetString()!,
				addressObject.GetProperty("attributes").GetRawText(),
				addressChanges.GetRawText(),
				addressChanges.GetProperty("FullAddress").GetProperty("value").GetString()!,
				addressChanges.GetProperty("UPRN").GetProperty("value").GetString()!
			));
		}

		return addressEntries;
	}

	/// <summary>
	/// Parses the select-address response into the final set of bin collection days.
	/// </summary>
	private GetBinDaysResponse ParseBinDaysResponse(ClientSideResponse clientSideResponse, Address address)
	{
		var enquiryGuid = clientSideResponse.Options.Metadata["enquiryGuid"];

		using var jsonDocument = JsonDocument.Parse(clientSideResponse.Content);
		var enquiryChanges = jsonDocument.RootElement.GetProperty("changes").GetProperty(enquiryGuid);

		// Iterate through each bin type, and create a bin day for any populated collection date
		var binDays = new List<BinDay>();
		foreach (var bin in _binTypes)
		{
			var fieldName = bin.Keys.Single();
			var dateValue = enquiryChanges.GetProperty(fieldName).GetProperty("value");

			if (dateValue.ValueKind != JsonValueKind.String)
			{
				continue;
			}

			var date = DateUtilities.ParseDateExact(dateValue.GetString()!, "dddd dd/MM/yyyy");
			var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, fieldName);

			var binDay = new BinDay
			{
				Date = date,
				Address = address,
				Bins = matchedBins,
			};

			binDays.Add(binDay);
		}

		return new GetBinDaysResponse
		{
			BinDays = ProcessingUtilities.ProcessBinDays(binDays),
		};
	}
}
