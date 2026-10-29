namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

/// <summary>
/// Collector implementation for Brighton and Hove City Council.
/// </summary>
internal sealed class BrightonAndHoveCityCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Brighton and Hove City Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.brighton-hove.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "brighton-and-hove";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Keys = [ "Refuse" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Black,
			Keys = [ "Recycling" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Orange,
			Keys = [ "Food Waste" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden Waste" ],
		},
	];

	/// <summary>
	/// The base URL for the council's environmental services (Mendix) app.
	/// </summary>
	private const string _baseUrl = "https://enviroservices.brighton-hove.gov.uk";

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for opening the collections deep link
		if (clientSideResponse == null)
		{
			var clientSideRequest = CreateDeepLinkRequest();

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for getting the session data
		else if (clientSideResponse.RequestId == 1)
		{
			var clientSideRequest = CreateSessionDataRequest(clientSideResponse);

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for searching addresses
		else if (clientSideResponse.RequestId == 2)
		{
			var clientSideRequest = CreateRuntimeOperationRequest(
				clientSideResponse,
				"tglPIXhcJ1abaQ1fhKJvYA",
				"Address",
				"SearchString",
				postcode
			);

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 3)
		{
			var changes = JsonNode.Parse(clientSideResponse.Content)!["changes"]!.AsObject();

			// Iterate through each changed object, and create a new address object for each search result
			var addresses = new List<Address>();
			foreach (var (_, change) in changes)
			{
				if (change!["display"] == null)
				{
					continue;
				}

				var address = new Address
				{
					Property = change["display"]!["value"]!.GetValue<string>().Trim(),
					Postcode = postcode,
					Uid = change["uprn"]!["value"]!.GetValue<string>(),
				};

				addresses.Add(address);
			}

			var getAddressesResponse = new GetAddressesResponse
			{
				Addresses = [.. addresses],
			};

			return getAddressesResponse;
		}

		// Throw exception for invalid request
		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <inheritdoc/>
	public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for opening the collections deep link
		if (clientSideResponse == null)
		{
			var clientSideRequest = CreateDeepLinkRequest();

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting the session data
		else if (clientSideResponse.RequestId == 1)
		{
			var clientSideRequest = CreateSessionDataRequest(clientSideResponse);

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting the collections
		else if (clientSideResponse.RequestId == 2)
		{
			var clientSideRequest = CreateRuntimeOperationRequest(
				clientSideResponse,
				"DExhrgP93VCBOks+s8GjTQ",
				"Collection",
				"uprn",
				address.Uid!
			);

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 3)
		{
			var rawCollections = JsonNode.Parse(clientSideResponse.Content)!["objects"]!.AsArray()
				.Where(rawObject => rawObject!["objectType"]!.GetValue<string>() == "Collections.CollectionsReturned");

			// Iterate through each collection, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var rawCollection in rawCollections)
			{
				var attributes = rawCollection!["attributes"]!;
				var service = attributes["Collection_type"]!["value"]!.GetValue<string>();
				var collectionDate = attributes["Formatted_collection"]!["value"]!.GetValue<string>();

				var date = DateUtilities.ParseDateExact(collectionDate, "dd MMMM yyyy");

				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, service);

				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = matchedBinTypes,
				};

				binDays.Add(binDay);
			}

			var getBinDaysResponse = new GetBinDaysResponse
			{
				BinDays = ProcessingUtilities.ProcessBinDays(binDays),
			};

			return getBinDaysResponse;
		}

		// Throw exception for invalid request
		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <summary>
	/// Creates the client-side request for opening the collections deep link, which starts a session on the collections page.
	/// </summary>
	private static ClientSideRequest CreateDeepLinkRequest()
	{
		var clientSideRequest = new ClientSideRequest
		{
			RequestId = 1,
			Url = $"{_baseUrl}/link/collections",
			Method = "GET",
			Options = new ClientSideOptions
			{
				FollowRedirects = false,
			},
		};

		return clientSideRequest;
	}

	/// <summary>
	/// Creates the client-side request for getting the session data, which creates the collection and address objects.
	/// </summary>
	private static ClientSideRequest CreateSessionDataRequest(ClientSideResponse clientSideResponse)
	{
		var setCookieHeader = clientSideResponse.Headers["set-cookie"];
		var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

		var clientSideRequest = new ClientSideRequest
		{
			RequestId = 2,
			Url = $"{_baseUrl}/xas/",
			Method = "POST",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
				{ "content-type", Constants.ApplicationJson },
				{ "cookie", requestCookies },
			},
			Body = """{"action":"get_session_data","params":{}}""",
		};

		return clientSideRequest;
	}

	/// <summary>
	/// Creates the client-side request for running a runtime operation, sending back the session's collection and address
	/// objects with an attribute of the address set.
	/// </summary>
	private static ClientSideRequest CreateRuntimeOperationRequest(
		ClientSideResponse clientSideResponse,
		string operationId,
		string parameterName,
		string addressAttribute,
		string addressAttributeValue
	)
	{
		var setCookieHeader = clientSideResponse.Headers["set-cookie"];
		var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

		var sessionData = JsonNode.Parse(clientSideResponse.Content)!;
		var csrfToken = sessionData["csrftoken"]!.GetValue<string>();
		var changes = sessionData["changes"]!.AsObject();

		// The session objects (other than the deep link) must be sent back with every operation
		var objects = new JsonArray([.. sessionData["objects"]!.AsArray()
			.Where(rawObject => rawObject!["objectType"]!.GetValue<string>() != "DeepLink.DeepLink")
			.Select(rawObject => rawObject!.DeepClone())]);

		var addressGuid = objects.Single(rawObject => rawObject!["objectType"]!.GetValue<string>() == "BHCCTheme.Address")!["guid"]!.GetValue<string>();
		var parameterGuid = objects.Single(rawObject => rawObject!["objectType"]!.GetValue<string>().EndsWith($".{parameterName}"))!["guid"]!.GetValue<string>();

		changes[addressGuid]![addressAttribute] = new JsonObject
		{
			["value"] = addressAttributeValue,
		};

		var requestBody = $$"""
		{
			"action": "runtimeOperation",
			"operationId": "{{operationId}}",
			"params": {
				"{{parameterName}}": {
					"guid": "{{parameterGuid}}"
				}
			},
			"changes": {{changes.ToJsonString()}},
			"objects": {{objects.ToJsonString()}}
		}
		""";

		var clientSideRequest = new ClientSideRequest
		{
			RequestId = 3,
			Url = $"{_baseUrl}/xas/",
			Method = "POST",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
				{ "content-type", Constants.ApplicationJson },
				{ "cookie", requestCookies },
				{ "x-csrf-token", csrfToken },
			},
			Body = requestBody,
		};

		return clientSideRequest;
	}
}
