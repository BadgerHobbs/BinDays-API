namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for West Lothian Council.
/// </summary>
internal sealed partial class WestLothianCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "West Lothian Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.westlothian.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "west-lothian";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "Garden and Food Waste",
			Colour = BinColour.Brown,
			Keys = [ "BROWN" ],
		},
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Grey,
			Keys = [ "GREY" ],
		},
		new()
		{
			Name = "Plastics, Cartons, Tins and Cans Recycling",
			Colour = BinColour.Green,
			Keys = [ "GREEN" ],
		},
		new()
		{
			Name = "Paper and Cardboard Recycling",
			Colour = BinColour.Blue,
			Keys = [ "BLUE" ],
		},
	];

	/// <summary>
	/// Regex for parsing JSONP responses.
	/// </summary>
	[GeneratedRegex(@"^[^(]+\((?<json>.*)\)$", RegexOptions.Singleline)]
	private static partial Regex JsonpRegex();

	/// <summary>
	/// Regex for the page session id from the hidden input.
	/// </summary>
	[GeneratedRegex(@"name=""WLBINCOLLECTION_PAGESESSIONID""\s+value=""(?<pageSessionId>[^""]+)""")]
	private static partial Regex PageSessionIdRegex();

	/// <summary>
	/// Regex for the session id from the hidden input.
	/// </summary>
	[GeneratedRegex(@"name=""WLBINCOLLECTION_SESSIONID""\s+value=""(?<sessionId>[^""]+)""")]
	private static partial Regex SessionIdRegex();

	/// <summary>
	/// Regex for the nonce from the hidden input.
	/// </summary>
	[GeneratedRegex(@"name=""WLBINCOLLECTION_NONCE""\s+value=""(?<nonce>[^""]+)""")]
	private static partial Regex NonceRegex();

	/// <summary>
	/// Regex for collections json from the hidden input.
	/// </summary>
	[GeneratedRegex(@"name=""WLBINCOLLECTION_PAGE2_COLLECTIONS""[^>]*value=""(?<collections>[^""]+)""", RegexOptions.Singleline)]
	private static partial Regex CollectionsRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var jsonRpcBody = $$"""
			{
			    "id": 1,
			    "method": "postcodeSearch",
			    "params": {
			        "provider": "EndPoint",
			        "postcode": "{{postcode}}"
			    }
			}
			""";

			var queryString = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "callback", "jQuery1" },
				{ "jsonrpc", jsonRpcBody },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://www.westlothian.gov.uk/apiserver/postcode?{queryString}",
				Method = "GET",
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 1)
		{
			var jsonContent = JsonpRegex().Match(clientSideResponse.Content).Groups["json"].Value;
			using var jsonDoc = JsonDocument.Parse(jsonContent);

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var addressElement in jsonDoc.RootElement.GetProperty("result").EnumerateArray())
			{
				var addressParts = new[]
				{
					addressElement.GetProperty("line1").GetString()?.Trim(),
					addressElement.GetProperty("line2").GetString()?.Trim(),
					addressElement.GetProperty("line3").GetString()?.Trim(),
					addressElement.GetProperty("line4").GetString()?.Trim(),
					addressElement.GetProperty("line5").GetString()?.Trim(),
				};

				var property = string.Join(", ", addressParts.Where(part => !string.IsNullOrWhiteSpace(part)));
				var uprn = addressElement.GetProperty("udprn").GetString()!.Trim();

				var address = new Address
				{
					Property = property,
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
		// Prepare client-side request for the form page
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://www.westlothian.gov.uk/bin-collections",
				Method = "GET",
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Submit the selected address to get collection data.
		else if (clientSideResponse.RequestId == 1)
		{
			var pageSessionId = PageSessionIdRegex().Match(clientSideResponse.Content).Groups["pageSessionId"].Value;
			var sessionId = SessionIdRegex().Match(clientSideResponse.Content).Groups["sessionId"].Value;
			var nonce = NonceRegex().Match(clientSideResponse.Content).Groups["nonce"].Value;

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "WLBINCOLLECTION_PAGESESSIONID", pageSessionId },
				{ "WLBINCOLLECTION_SESSIONID", sessionId },
				{ "WLBINCOLLECTION_NONCE", nonce },
				{ "WLBINCOLLECTION_VARIABLES", "e30=" },
				{ "WLBINCOLLECTION_PAGENAME", "PAGE1" },
				{ "WLBINCOLLECTION_PAGEINSTANCE", "0" },
				{ "WLBINCOLLECTION_PAGE1_ADDRESSSTRING", address.Property! },
				{ "WLBINCOLLECTION_PAGE1_UPRN", address.Uid! },
				{ "WLBINCOLLECTION_PAGE1_ADDRESSLOOKUPPOSTCODE", address.Postcode! },
				{ "WLBINCOLLECTION_FORMACTION_NEXT", "WLBINCOLLECTION_PAGE1_NAVBUTTONS" },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"https://www.westlothian.gov.uk/apiserver/formsservice/http/processsubmission?pageSessionId={pageSessionId}&fsid={sessionId}&fsn={nonce}",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
				},
				Body = requestBody,
				Options = new ClientSideOptions
				{
					FollowRedirects = false,
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Follow cookie-verification redirect and load the final collection page.
		else if (clientSideResponse.RequestId == 2)
		{
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var cookie = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);
			var verifyCookieUrl = clientSideResponse.Headers["location"];

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = verifyCookieUrl,
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "cookie", cookie },
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
			var collectionsValue = CollectionsRegex().Match(clientSideResponse.Content).Groups["collections"].Value;
			var collectionsJson = WebUtility.HtmlDecode(collectionsValue);
			using var jsonDoc = JsonDocument.Parse(collectionsJson);

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var collectionElement in jsonDoc.RootElement.EnumerateArray())
			{
				var service = collectionElement.GetProperty("binType").GetString()!.Trim();
				var collectionDate = collectionElement.GetProperty("nextCollectionISO").GetString()!.Trim();
				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, service);

				var binDay = new BinDay
				{
					Date = DateUtilities.ParseDateExact(collectionDate, "yyyy-MM-dd"),
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
