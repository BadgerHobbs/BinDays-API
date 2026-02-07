namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
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

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "Mixed Dry Recycling",
			Colour = BinColour.Green,
			Keys = [ "GREEN 240L", ],
		},
		new()
		{
			Name = "Glass Recycling",
			Colour = BinColour.Green,
			Keys = [ "GLASS BAG", ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "BROWN 240L", ],
		},
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "BLACK 240L", ],
		},
	];

	/// <summary>
	/// The URL of the web form for bin collection lookups.
	/// </summary>
	private const string _formUrl = "https://selfservice.broxtowe.gov.uk/renderform.aspx?t=217&k=9D2EF214E144EE796430597FB475C3892C43C528";

	/// <summary>
	/// The ASP.NET script manager target for AJAX requests.
	/// </summary>
	private const string _scriptManagerTarget = "ctl00$ContentPlaceHolder1$APUP_5683";

	/// <summary>
	/// The event target for postcode search button.
	/// </summary>
	private const string _searchEventTarget = "ctl00$ContentPlaceHolder1$FF5683BTN";

	/// <summary>
	/// The event target for address dropdown selection.
	/// </summary>
	private const string _addressEventTarget = "ctl00$ContentPlaceHolder1$FF5683DDL";

	/// <summary>
	/// Regex for parsing hidden fields from AJAX responses.
	/// </summary>
	[GeneratedRegex(@"hiddenField\|(?<name>__VIEWSTATEGENERATOR|__EVENTVALIDATION|__VIEWSTATE)\|(?<value>[^|]+)")]
	private static partial Regex AjaxHiddenFieldRegex();

	/// <summary>
	/// Regex for parsing hidden fields from HTML responses.
	/// </summary>
	[GeneratedRegex(@"name=""(?<name>__VIEWSTATEGENERATOR|__EVENTVALIDATION|__VIEWSTATE)""[^>]+value=""(?<value>[^""]+)""")]
	private static partial Regex HtmlHiddenFieldRegex();

	/// <summary>
	/// Regex for parsing address options.
	/// </summary>
	[GeneratedRegex(@"<option value=""(?<uid>[^""]+)"">(?<address>[^<]+)</option>")]
	private static partial Regex AddressRegex();

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
			var clientSideRequest = CreatePostcodeSearchRequest(clientSideResponse, postcode);

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 2)
		{
			var rawAddresses = AddressRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				var uid = rawAddress.Groups["uid"].Value;

				if (string.IsNullOrWhiteSpace(uid) || uid == "0")
				{
					continue;
				}

				var property = WebUtility.HtmlDecode(rawAddress.Groups["address"].Value).Trim();

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
		// Prepare client-side request for postcode search
		else if (clientSideResponse.RequestId == 1)
		{
			var clientSideRequest = CreatePostcodeSearchRequest(clientSideResponse, address.Postcode!);

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for selecting the address
		else if (clientSideResponse.RequestId == 2)
		{
			var cookie = clientSideResponse.Options.Metadata["cookie"];

			var viewState = GetHiddenField(clientSideResponse.Content, "__VIEWSTATE");
			var viewStateGenerator = GetHiddenField(clientSideResponse.Content, "__VIEWSTATEGENERATOR");
			var eventValidation = GetHiddenField(clientSideResponse.Content, "__EVENTVALIDATION");

			var formData = new Dictionary<string, string>
			{
				{ "ctl00$ScriptManager1", $"{_scriptManagerTarget}|{_addressEventTarget}" },
				{ "ctl00$ContentPlaceHolder1$txtPositionLL", string.Empty },
				{ "ctl00$ContentPlaceHolder1$txtPosition", string.Empty },
				{ "ctl00$ContentPlaceHolder1$FF5683DDL", address.Uid! },
				{ "__EVENTTARGET", _addressEventTarget },
				{ "__EVENTARGUMENT", string.Empty },
				{ "__LASTFOCUS", string.Empty },
				{ "__VIEWSTATE", viewState },
				{ "__VIEWSTATEGENERATOR", viewStateGenerator },
				{ "__EVENTVALIDATION", eventValidation },
				{ "__ASYNCPOST", "true" },
			};

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = _formUrl,
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "x-requested-with", "XMLHttpRequest" },
					{ "x-microsoftajax", "Delta=true" },
					{ "content-type", "application/x-www-form-urlencoded; charset=utf-8" },
					{ "cookie", cookie },
				},
				Body = ProcessingUtilities.ConvertDictionaryToFormData(formData),
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "cookie", cookie },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request to fetch bin collections
		else if (clientSideResponse.RequestId == 3)
		{
			var cookie = clientSideResponse.Options.Metadata["cookie"];

			var viewState = GetHiddenField(clientSideResponse.Content, "__VIEWSTATE");
			var viewStateGenerator = GetHiddenField(clientSideResponse.Content, "__VIEWSTATEGENERATOR");
			var eventValidation = GetHiddenField(clientSideResponse.Content, "__EVENTVALIDATION");

			var formData = new Dictionary<string, string>
			{
				{ "ctl00$ContentPlaceHolder1$txtPositionLL", string.Empty },
				{ "ctl00$ContentPlaceHolder1$txtPosition", string.Empty },
				{ "__EVENTTARGET", "ctl00$ContentPlaceHolder1$btnSubmit" },
				{ "__EVENTARGUMENT", string.Empty },
				{ "__LASTFOCUS", string.Empty },
				{ "__VIEWSTATE", viewState },
				{ "__VIEWSTATEGENERATOR", viewStateGenerator },
				{ "__EVENTVALIDATION", eventValidation },
			};

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 4,
				Url = _formUrl,
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", "application/x-www-form-urlencoded" },
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
		else if (clientSideResponse.RequestId == 4)
		{
			var rawBinRows = BinRowRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each bin row, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinRow in rawBinRows)
			{
				var service = WebUtility.HtmlDecode(rawBinRow.Groups["service"].Value).Trim();
				var nextCollection = WebUtility.HtmlDecode(rawBinRow.Groups["next"].Value).Trim();

				var date = DateOnly.ParseExact(
					nextCollection,
					"dddd, dd MMMM yyyy",
					CultureInfo.InvariantCulture,
					DateTimeStyles.None
				);

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
			Url = _formUrl,
			Method = "GET",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
			},
		};
	}

	/// <summary>
	/// Creates a client-side request for postcode search.
	/// </summary>
	private static ClientSideRequest CreatePostcodeSearchRequest(ClientSideResponse clientSideResponse, string postcode)
	{
		clientSideResponse.Headers.TryGetValue("set-cookie", out var setCookieHeader);
		var cookie = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader!);

		var viewState = GetHiddenField(clientSideResponse.Content, "__VIEWSTATE");
		var viewStateGenerator = GetHiddenField(clientSideResponse.Content, "__VIEWSTATEGENERATOR");
		var eventValidation = GetHiddenField(clientSideResponse.Content, "__EVENTVALIDATION");

		var formData = new Dictionary<string, string>
		{
			{ "ctl00$ScriptManager1", $"{_scriptManagerTarget}|{_searchEventTarget}" },
			{ "__EVENTTARGET", _searchEventTarget },
			{ "__EVENTARGUMENT", string.Empty },
			{ "__VIEWSTATE", viewState },
			{ "__VIEWSTATEGENERATOR", viewStateGenerator },
			{ "__EVENTVALIDATION", eventValidation },
			{ "ctl00$ContentPlaceHolder1$txtPositionLL", string.Empty },
			{ "ctl00$ContentPlaceHolder1$txtPosition", string.Empty },
			{ "ctl00$ContentPlaceHolder1$FF5683TB", postcode },
			{ "__ASYNCPOST", "true" },
		};

		return new ClientSideRequest
		{
			RequestId = 2,
			Url = _formUrl,
			Method = "POST",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
				{ "x-requested-with", "XMLHttpRequest" },
				{ "x-microsoftajax", "Delta=true" },
				{ "content-type", "application/x-www-form-urlencoded; charset=utf-8" },
				{ "cookie", cookie },
			},
			Body = ProcessingUtilities.ConvertDictionaryToFormData(formData),
			Options = new ClientSideOptions
			{
				Metadata =
				{
					{ "cookie", cookie },
				},
			},
		};
	}

	/// <summary>
	/// Extracts hidden field values from HTML or AJAX responses.
	/// </summary>
	private static string GetHiddenField(string content, string fieldName)
	{
		// First, try to find the field in the AJAX response format.
		var ajaxMatch = AjaxHiddenFieldRegex().Matches(content)!.FirstOrDefault(m => m.Groups["name"].Value == fieldName);
		if (ajaxMatch is not null)
		{
			return ajaxMatch.Groups["value"].Value;
		}

		// If not found, try the standard HTML input format.
		var htmlMatch = HtmlHiddenFieldRegex().Matches(content)!.FirstOrDefault(m => m.Groups["name"].Value == fieldName);
		if (htmlMatch is not null)
		{
			return htmlMatch.Groups["value"].Value;
		}

		// If the field is not found in either format, throw an exception.
		throw new InvalidOperationException($"Hidden field '{fieldName}' not found in response content.");
	}
}
