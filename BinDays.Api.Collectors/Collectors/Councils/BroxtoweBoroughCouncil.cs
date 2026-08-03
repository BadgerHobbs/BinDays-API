namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Broxtowe Borough Council.
/// </summary>
internal sealed partial class BroxtoweBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Broxtowe Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.broxtowe.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "broxtowe";

	/// <inheritdoc/>
	public override int Version => 2;

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "Mixed Dry Recycling",
			Colour = BinColour.Green,
			Keys = [ "GREEN 240L" ],
		},
		new()
		{
			Name = "Glass Recycling",
			Colour = BinColour.Green,
			Keys = [ "GLASS BAG", "RED 140L" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "BROWN 240L" ],
		},
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "BLACK 240L" ],
		},
	];

	/// <summary>
	/// Regex for parsing HTML input values by name.
	/// </summary>
	[GeneratedRegex(@"<input[^>]*name=""(?<name>[^""]+)""[^>]*value=""(?<value>[^""]*)""[^>]*>", RegexOptions.IgnoreCase)]
	private static partial Regex InputValueRegex();

	/// <summary>
	/// Regex for parsing bin rows.
	/// </summary>
	[GeneratedRegex(@"<tr>\s*<td>(?<service>[^<]+)</td>\s*<td>(?<day>[^<]+)</td>\s*<td>(?<last>[^<]*)</td>\s*<td>(?<next>[^<]+)</td>\s*</tr>", RegexOptions.Singleline)]
	private static partial Regex BinRowRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for initial form load
		if (clientSideResponse == null)
		{
			var clientSideRequest = CreateInitialFormRequest();

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for postcode search
		else if (clientSideResponse.RequestId == 1)
		{
			var cookie = GetRequestCookie(clientSideResponse);

			Dictionary<string, string> formData = new()
			{
				{ "query", postcode },
			};

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = "https://selfservice.broxtowe.gov.uk/core/addresslookup",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
					{ "cookie", cookie },
				},
				Body = ProcessingUtilities.ConvertDictionaryToFormData(formData),
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
			using var document = JsonDocument.Parse(clientSideResponse.Content);

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var rawAddress in document.RootElement.EnumerateArray())
			{
				var uid = rawAddress.GetProperty("Key").GetString()!;
				var property = WebUtility.HtmlDecode(rawAddress.GetProperty("Value").GetString()!).Trim();

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
		// Prepare client-side request for initial form load
		if (clientSideResponse == null)
		{
			var clientSideRequest = CreateInitialFormRequest();

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request to fetch bin collections
		else if (clientSideResponse.RequestId == 1)
		{
			var cookie = GetRequestCookie(clientSideResponse);

			var requestVerificationToken = GetInputValue(clientSideResponse.Content, "__RequestVerificationToken");
			var formGuid = GetInputValue(clientSideResponse.Content, "FormGuid");
			var objectTemplateId = GetInputValue(clientSideResponse.Content, "ObjectTemplateID");
			var currentSectionId = GetInputValue(clientSideResponse.Content, "CurrentSectionID");

			Dictionary<string, string> formData = new()
			{
				{ "__RequestVerificationToken", requestVerificationToken },
				{ "FormGuid", formGuid },
				{ "ObjectTemplateID", objectTemplateId },
				{ "Trigger", "submit" },
				{ "CurrentSectionID", currentSectionId },
				{ "FF5683", address.Uid! },
			};

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = "https://selfservice.broxtowe.gov.uk/renderform/Form",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
					{ "cookie", cookie },
				},
				Body = ProcessingUtilities.ConvertDictionaryToFormData(formData),
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
			var rawBinRows = BinRowRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each bin row, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinRow in rawBinRows)
			{
				var service = WebUtility.HtmlDecode(rawBinRow.Groups["service"].Value).Trim();
				var nextCollection = WebUtility.HtmlDecode(rawBinRow.Groups["next"].Value).Trim();

				var date = DateUtilities.ParseDateExact(nextCollection, "dddd, dd MMMM yyyy");

				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, service);

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

	/// <summary>
	/// Creates the initial client-side request to load the form.
	/// </summary>
	private static ClientSideRequest CreateInitialFormRequest()
	{
		return new ClientSideRequest
		{
			RequestId = 1,
			Url = "https://selfservice.broxtowe.gov.uk/renderform?t=217&k=9D2EF214E144EE796430597FB475C3892C43C528",
			Method = "GET",
		};
	}

	/// <summary>
	/// Parses the request cookie value from the response headers.
	/// </summary>
	private static string GetRequestCookie(ClientSideResponse clientSideResponse)
	{
		var setCookieHeader = clientSideResponse.Headers["set-cookie"];
		var cookie = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

		return cookie;
	}

	/// <summary>
	/// Extracts an input value by its name from the HTML response.
	/// </summary>
	private static string GetInputValue(string content, string inputName)
	{
		var rawInputs = InputValueRegex().Matches(content)!;
		foreach (Match rawInput in rawInputs)
		{
			if (rawInput.Groups["name"].Value != inputName)
			{
				continue;
			}

			return WebUtility.HtmlDecode(rawInput.Groups["value"].Value);
		}

		throw new InvalidOperationException($"Input '{inputName}' not found in response content.");
	}
}
