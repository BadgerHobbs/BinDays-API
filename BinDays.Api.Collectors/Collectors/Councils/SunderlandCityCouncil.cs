namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Sunderland City Council.
/// </summary>
internal sealed partial class SunderlandCityCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Sunderland City Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.sunderland.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "sunderland";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Keys = [ "Household Green Bin" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Blue Recycling Bin" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden Waste" ],
		},
	];

	/// <summary>
	/// The URL for the bin day checker form. The 'ccp=true' parameter pre-acknowledges the
	/// cookie-consent prompt so the page renders directly instead of redirecting to it.
	/// </summary>
	private const string _formUrl = "https://www.sunderland.gov.uk/bindays?ccp=true";

	/// <summary>
	/// The URL for submitting a form page.
	/// </summary>
	private const string _processSubmissionUrl = "https://www.sunderland.gov.uk/apiserver/formsservice/http/processsubmission";

	/// <summary>
	/// The value used to trigger the form's "next" action.
	/// </summary>
	private const string _formActionTrigger = "BINCOLLECTIONCHECKERNEWV3_ADDRESSSEARCH_POSTCODETRIGGER";

	/// <summary>
	/// The initial form variable state (base64-encoded JSON) submitted with a postcode search.
	/// The GOSS form requires this to trigger the server-side address lookup.
	/// </summary>
	private static readonly string _initialVariables = Convert.ToBase64String(Encoding.UTF8.GetBytes(
		"""{"buttonpressed":{"value":false,"scope":"SERVERCLIENTWITHUPDATE"},"CHECKADDRESSLISTFOUNDLOCATION":{"value":"NULL","scope":"SERVERCLIENTWITHUPDATE"},"postcodefoundoptionset":{"value":"","scope":"SERVERCLIENTWITHUPDATE"},"postcodefoundfulldetails":{"value":"","scope":"SERVERCLIENTWITHUPDATE"}}"""));

	/// <summary>
	/// Regex for the page session id from the hidden input.
	/// </summary>
	[GeneratedRegex(@"name=""BINCOLLECTIONCHECKERNEWV3_PAGESESSIONID""\s+value=""(?<pageSessionId>[^""]+)""")]
	private static partial Regex PageSessionIdRegex();

	/// <summary>
	/// Regex for the session id from the hidden input.
	/// </summary>
	[GeneratedRegex(@"name=""BINCOLLECTIONCHECKERNEWV3_SESSIONID""\s+value=""(?<sessionId>[^""]+)""")]
	private static partial Regex SessionIdRegex();

	/// <summary>
	/// Regex for the nonce from the hidden input.
	/// </summary>
	[GeneratedRegex(@"name=""BINCOLLECTIONCHECKERNEWV3_NONCE""\s+value=""(?<nonce>[^""]+)""")]
	private static partial Regex NonceRegex();

	/// <summary>
	/// Regex for the serialized form variables emitted on the populated addresses page.
	/// This state carries the selected address details required by the bin day lookup.
	/// </summary>
	[GeneratedRegex(@"BINCOLLECTIONCHECKERNEWV3SerializedVariables\s*=\s*""(?<variables>[^""]+)""")]
	private static partial Regex SerializedVariablesRegex();

	/// <summary>
	/// Regex for the addresses from the option elements.
	/// </summary>
	[GeneratedRegex(@"<option value=""(?<uid>\d+)""[^>]*>\s*(?<address>[^<]+?)\s*</option>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for the bin days from the collection blocks.
	/// </summary>
	[GeneratedRegex(@"myaccount-block__title[^>]*>(?<service>[^<]+)</p>.*?(?<date>[A-Z][a-z]{2} [A-Z][a-z]{2} \d{2} \d{4})", RegexOptions.Singleline)]
	private static partial Regex BinDaysRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for the form page
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _formUrl,
				Method = "GET",
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prime the session: the first postcode submission is consumed by the cookie-verification
		// step, which issues the 'goss-formsservice-clientid' cookie needed for the real search.
		else if (clientSideResponse.RequestId == 1)
		{
			var pageSessionId = PageSessionIdRegex().Match(clientSideResponse.Content).Groups["pageSessionId"].Value;
			var sessionId = SessionIdRegex().Match(clientSideResponse.Content).Groups["sessionId"].Value;
			var nonce = NonceRegex().Match(clientSideResponse.Content).Groups["nonce"].Value;

			var requestBody = BuildAddressSearchFormData(pageSessionId, sessionId, nonce, _initialVariables, "0", postcode, "", "");

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_processSubmissionUrl}?pageSessionId={pageSessionId}&fsid={sessionId}&fsn={nonce}",
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

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Reload the form, now authenticated with the verified session cookie.
		else if (clientSideResponse.RequestId == 2)
		{
			var cookie = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = _formUrl,
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "cookie", cookie },
				},
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "cookie", cookie },
					},
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Submit the postcode search and follow the redirects to the populated addresses page.
		else if (clientSideResponse.RequestId == 3)
		{
			var cookie = clientSideResponse.Options.Metadata["cookie"];

			var pageSessionId = PageSessionIdRegex().Match(clientSideResponse.Content).Groups["pageSessionId"].Value;
			var sessionId = SessionIdRegex().Match(clientSideResponse.Content).Groups["sessionId"].Value;
			var nonce = NonceRegex().Match(clientSideResponse.Content).Groups["nonce"].Value;

			var requestBody = BuildAddressSearchFormData(pageSessionId, sessionId, nonce, _initialVariables, "0", postcode, "", "");

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 4,
				Url = $"{_processSubmissionUrl}?pageSessionId={pageSessionId}&fsid={sessionId}&fsn={nonce}",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
					{ "cookie", cookie },
				},
				Body = requestBody,
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 4)
		{
			var rawAddresses = AddressRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				var address = new Address
				{
					Property = rawAddress.Groups["address"].Value.Trim(),
					Postcode = postcode,
					Uid = rawAddress.Groups["uid"].Value,
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
				Url = _formUrl,
				Method = "GET",
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prime the session: the first postcode submission is consumed by the cookie-verification
		// step, which issues the 'goss-formsservice-clientid' cookie needed for the real search.
		else if (clientSideResponse.RequestId == 1)
		{
			var pageSessionId = PageSessionIdRegex().Match(clientSideResponse.Content).Groups["pageSessionId"].Value;
			var sessionId = SessionIdRegex().Match(clientSideResponse.Content).Groups["sessionId"].Value;
			var nonce = NonceRegex().Match(clientSideResponse.Content).Groups["nonce"].Value;

			var requestBody = BuildAddressSearchFormData(pageSessionId, sessionId, nonce, _initialVariables, "0", address.Postcode!, "", "");

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_processSubmissionUrl}?pageSessionId={pageSessionId}&fsid={sessionId}&fsn={nonce}",
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
		// Reload the form, now authenticated with the verified session cookie.
		else if (clientSideResponse.RequestId == 2)
		{
			var cookie = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = _formUrl,
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "cookie", cookie },
				},
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
		// Submit the postcode search and follow the redirects to the populated addresses page.
		else if (clientSideResponse.RequestId == 3)
		{
			var cookie = clientSideResponse.Options.Metadata["cookie"];

			var pageSessionId = PageSessionIdRegex().Match(clientSideResponse.Content).Groups["pageSessionId"].Value;
			var sessionId = SessionIdRegex().Match(clientSideResponse.Content).Groups["sessionId"].Value;
			var nonce = NonceRegex().Match(clientSideResponse.Content).Groups["nonce"].Value;

			var requestBody = BuildAddressSearchFormData(pageSessionId, sessionId, nonce, _initialVariables, "0", address.Postcode!, "", "");

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 4,
				Url = $"{_processSubmissionUrl}?pageSessionId={pageSessionId}&fsid={sessionId}&fsn={nonce}",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
					{ "cookie", cookie },
				},
				Body = requestBody,
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
		// Select the address and follow the redirects to the bin collections page.
		else if (clientSideResponse.RequestId == 4)
		{
			var cookie = clientSideResponse.Options.Metadata["cookie"];

			var pageSessionId = PageSessionIdRegex().Match(clientSideResponse.Content).Groups["pageSessionId"].Value;
			var sessionId = SessionIdRegex().Match(clientSideResponse.Content).Groups["sessionId"].Value;
			var nonce = NonceRegex().Match(clientSideResponse.Content).Groups["nonce"].Value;
			var variables = SerializedVariablesRegex().Match(clientSideResponse.Content).Groups["variables"].Value;

			var requestBody = BuildAddressSearchFormData(pageSessionId, sessionId, nonce, variables, "1", address.Postcode!, address.Uid!, address.Property!);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 5,
				Url = $"{_processSubmissionUrl}?pageSessionId={pageSessionId}&fsid={sessionId}&fsn={nonce}",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
					{ "cookie", cookie },
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
		else if (clientSideResponse.RequestId == 5)
		{
			var rawBinDays = BinDaysRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var service = rawBinDay.Groups["service"].Value.Trim();
				var date = DateUtilities.ParseDateExact(rawBinDay.Groups["date"].Value, "ddd MMM dd yyyy");

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

		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <summary>
	/// Builds the form data for an address search page submission.
	/// </summary>
	/// <param name="pageSessionId">The page session id from the form.</param>
	/// <param name="sessionId">The session id from the form.</param>
	/// <param name="nonce">The nonce from the form.</param>
	/// <param name="variables">The base64-encoded form variable state.</param>
	/// <param name="pageInstance">The page instance ("0" for the postcode search, "1" for the address selection).</param>
	/// <param name="postcode">The postcode being searched.</param>
	/// <param name="selectedUid">The selected address uid, or empty for the postcode search.</param>
	/// <param name="selectedAddress">The selected address text, or empty for the postcode search.</param>
	/// <returns>The URL-encoded form data string.</returns>
	private static string BuildAddressSearchFormData(
		string pageSessionId,
		string sessionId,
		string nonce,
		string variables,
		string pageInstance,
		string postcode,
		string selectedUid,
		string selectedAddress)
	{
		return ProcessingUtilities.ConvertDictionaryToFormData(new()
		{
			{ "BINCOLLECTIONCHECKERNEWV3_PAGESESSIONID", pageSessionId },
			{ "BINCOLLECTIONCHECKERNEWV3_SESSIONID", sessionId },
			{ "BINCOLLECTIONCHECKERNEWV3_NONCE", nonce },
			{ "BINCOLLECTIONCHECKERNEWV3_VARIABLES", variables },
			{ "BINCOLLECTIONCHECKERNEWV3_PAGENAME", "ADDRESSSEARCH" },
			{ "BINCOLLECTIONCHECKERNEWV3_PAGEINSTANCE", pageInstance },
			{ "BINCOLLECTIONCHECKERNEWV3_ADDRESSSEARCH_SCCPOSTCODE", postcode },
			{ "BINCOLLECTIONCHECKERNEWV3_FORMACTION_NEXT", _formActionTrigger },
			{ "BINCOLLECTIONCHECKERNEWV3_ADDRESSSEARCH_SCCLISTOFADDRESSES", selectedUid },
			{ "BINCOLLECTIONCHECKERNEWV3_ADDRESSSEARCH_POSTCODE", selectedUid.Length > 0 ? postcode : "" },
			{ "BINCOLLECTIONCHECKERNEWV3_ADDRESSSEARCH_UPRN", selectedUid },
			{ "BINCOLLECTIONCHECKERNEWV3_ADDRESSSEARCH_RESIDUALBIN", "" },
			{ "BINCOLLECTIONCHECKERNEWV3_ADDRESSSEARCH_TRADEBIN", "" },
			{ "BINCOLLECTIONCHECKERNEWV3_ADDRESSSEARCH_RECYCLEBIN", "" },
			{ "BINCOLLECTIONCHECKERNEWV3_ADDRESSSEARCH_GARDENBIN", "" },
			{ "BINCOLLECTIONCHECKERNEWV3_ADDRESSSEARCH_NEXTBIN", "" },
			{ "BINCOLLECTIONCHECKERNEWV3_ADDRESSSEARCH_PDFURL", "" },
			{ "BINCOLLECTIONCHECKERNEWV3_ADDRESSSEARCH_LAT", "" },
			{ "BINCOLLECTIONCHECKERNEWV3_ADDRESSSEARCH_LNG", "" },
			{ "BINCOLLECTIONCHECKERNEWV3_ADDRESSSEARCH_ADDRESSTEXT", selectedAddress },
			{ "BINCOLLECTIONCHECKERNEWV3_ADDRESSSEARCH_DATARETURNED", "" },
			{ "BINCOLLECTIONCHECKERNEWV3_ADDRESSSEARCH_DATARETURNED2", "" },
		});
	}
}
