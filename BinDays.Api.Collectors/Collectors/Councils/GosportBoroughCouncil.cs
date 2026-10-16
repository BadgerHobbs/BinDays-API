namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Gosport Borough Council.
/// </summary>
internal sealed partial class GosportBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Gosport Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.gosport.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "gosport";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Domestic" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Green,
			Keys = [ "Recycling" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden" ],
		},
	];

	/// <summary>
	/// The URL of the refuse and recycling collection day search page.
	/// </summary>
	private const string _pageUrl = "https://www.gosport.gov.uk/refuserecyclingdays";

	/// <summary>
	/// Regex to capture the session hidden form fields.
	/// </summary>
	[GeneratedRegex(@"name=""QUERYWARDSCOLLECTIONSWS_(?<name>PAGESESSIONID|SESSIONID|NONCE)"" value=""(?<value>[^""]*)""")]
	private static partial Regex HiddenFieldRegex();

	/// <summary>
	/// Regex to capture the base64 encoded form variables, which contain the address list.
	/// </summary>
	[GeneratedRegex(@"SerializedVariables = ""(?<variables>[^""]+)""")]
	private static partial Regex SerializedVariablesRegex();

	/// <summary>
	/// Regex to capture each collection service and date from the results page.
	/// </summary>
	[GeneratedRegex(@"<b>(?<service>[^<]+)</b> is scheduled to be collected on <b>(?<date>\d{2}/\d{2}/\d{4})</b>")]
	private static partial Regex BinDayRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for the initial page load
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _pageUrl,
				Method = "GET",
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare form submission with the postcode search
		else if (clientSideResponse.RequestId == 1)
		{
			// The empty address fields are required, the search returns no addresses without them
			var clientSideRequest = CreateSubmissionRequest(clientSideResponse.Content, new()
			{
				{ "QUERYWARDSCOLLECTIONSWS_PAGE1_POSTCODE", postcode },
				{ "QUERYWARDSCOLLECTIONSWS_PAGE1_PROPERTY", string.Empty },
				{ "QUERYWARDSCOLLECTIONSWS_PAGE1_UPRN", string.Empty },
				{ "QUERYWARDSCOLLECTIONSWS_PAGE1_REFNO", string.Empty },
				{ "QUERYWARDSCOLLECTIONSWS_FORMACTION_NEXT", "QUERYWARDSCOLLECTIONSWS_PAGE1_GETADDRESSES" },
			});

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Follow the verify cookie redirect, then the redirect to the page containing the addresses
		else if (clientSideResponse.RequestId is 2 or 3)
		{
			var clientSideRequest = CreateRedirectRequest(clientSideResponse);

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 4)
		{
			var variablesBase64 = SerializedVariablesRegex().Match(clientSideResponse.Content).Groups["variables"].Value;
			var variablesJson = Encoding.UTF8.GetString(Convert.FromBase64String(variablesBase64));

			using var variablesDoc = JsonDocument.Parse(variablesJson);
			var rawAddresses = variablesDoc.RootElement.GetProperty("RSARRAY").GetProperty("value").EnumerateArray();

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var rawAddress in rawAddresses)
			{
				var uprn = rawAddress[0].GetString()!;

				// Skip the placeholder option
				if (string.IsNullOrWhiteSpace(uprn))
				{
					continue;
				}

				// Address text ends with the UPRN, e.g. "1 HIGH STREET, GOSPORT, HAMPSHIRE, PO12 1AA, 12345678"
				var property = rawAddress[1].GetString()!.Trim().Replace($", {uprn}", string.Empty);

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

		// Throw exception for invalid request
		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <inheritdoc/>
	public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for the initial page load
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _pageUrl,
				Method = "GET",
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare form submission with the selected address
		else if (clientSideResponse.RequestId == 1)
		{
			var clientSideRequest = CreateSubmissionRequest(clientSideResponse.Content, new()
			{
				{ "QUERYWARDSCOLLECTIONSWS_PAGE1_POSTCODE", address.Postcode! },
				{ "QUERYWARDSCOLLECTIONSWS_PAGE1_PROPERTY", address.Uid! },
				{ "QUERYWARDSCOLLECTIONSWS_PAGE1_UPRN", address.Uid! },
				{ "QUERYWARDSCOLLECTIONSWS_PAGE1_REFNO", address.Uid! },
				{ "QUERYWARDSCOLLECTIONSWS_FORMACTION_NEXT", "QUERYWARDSCOLLECTIONSWS_PAGE1_FIELD9" },
			});

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Follow the verify cookie redirect, then the redirect to the page containing the bin days
		else if (clientSideResponse.RequestId is 2 or 3)
		{
			var clientSideRequest = CreateRedirectRequest(clientSideResponse);

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 4)
		{
			var rawBinDays = BinDayRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var service = rawBinDay.Groups["service"].Value;
				var date = DateUtilities.ParseDateExact(rawBinDay.Groups["date"].Value, "dd/MM/yyyy");

				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = ProcessingUtilities.GetMatchingBins(_binTypes, service),
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
	/// Creates the form submission request for the current page, using the session fields from the initial page load.
	/// </summary>
	private static ClientSideRequest CreateSubmissionRequest(string pageContent, Dictionary<string, string> pageFields)
	{
		var hiddenFieldValues = HiddenFieldRegex().Matches(pageContent)!.ToDictionary(
			x => x.Groups["name"].Value,
			x => x.Groups["value"].Value
		);

		var pageSessionId = hiddenFieldValues["PAGESESSIONID"];
		var sessionId = hiddenFieldValues["SESSIONID"];
		var nonce = hiddenFieldValues["NONCE"];

		Dictionary<string, string> formFields = new()
		{
			{ "QUERYWARDSCOLLECTIONSWS_PAGESESSIONID", pageSessionId },
			{ "QUERYWARDSCOLLECTIONSWS_SESSIONID", sessionId },
			{ "QUERYWARDSCOLLECTIONSWS_NONCE", nonce },
		};

		foreach (var pageField in pageFields)
		{
			formFields.Add(pageField.Key, pageField.Value);
		}

		var clientSideRequest = new ClientSideRequest
		{
			RequestId = 2,
			Url = $"https://www.gosport.gov.uk/apiserver/formsservice/http/processsubmission?pageSessionId={pageSessionId}&fsid={sessionId}&fsn={nonce}",
			Method = "POST",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
				{ "content-type", Constants.FormUrlEncoded },
			},
			Body = ProcessingUtilities.ConvertDictionaryToFormData(formFields),
			Options = new ClientSideOptions
			{
				FollowRedirects = false,
			},
		};

		return clientSideRequest;
	}

	/// <summary>
	/// Creates the request following a form submission redirect, carrying the forms service session cookie.
	/// </summary>
	private static ClientSideRequest CreateRedirectRequest(ClientSideResponse clientSideResponse)
	{
		// The session cookie is set by the form submission response, and must be sent on each redirect
		var cookies = clientSideResponse.RequestId == 2
			? ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"])
			: clientSideResponse.Options.Metadata["cookie"];

		var clientSideRequest = new ClientSideRequest
		{
			RequestId = clientSideResponse.RequestId + 1,
			Url = clientSideResponse.Headers["location"],
			Method = "GET",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
				{ "cookie", cookies },
			},
			Options = new ClientSideOptions
			{
				FollowRedirects = false,
				Metadata =
				{
					{ "cookie", cookies },
				},
			},
		};

		return clientSideRequest;
	}
}
