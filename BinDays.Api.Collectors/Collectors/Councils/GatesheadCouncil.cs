namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Gateshead Council.
/// </summary>
internal sealed partial class GatesheadCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Gateshead Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.gateshead.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "gateshead";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "Household Waste",
			Colour = BinColour.Green,
			Keys = [ "Household Waste" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Recycling" ],
		},
	];

	/// <summary>
	/// The URL of the bin collection checker page.
	/// </summary>
	private const string _pageUrl = "https://www.gateshead.gov.uk/article/3150/Bin-collection-day-checker";

	/// <summary>
	/// The URL for processing form submissions.
	/// </summary>
	private const string _processSubmissionUrl = "https://www.gateshead.gov.uk/apiserver/formsservice/http/processsubmission";

	/// <summary>
	/// Regex to capture hidden form fields.
	/// </summary>
	[GeneratedRegex(@"name=""(?<name>BINCOLLECTIONCHECKER_[^""]+)"" value=""(?<value>[^""]*)""")]
	private static partial Regex HiddenFieldRegex();

	/// <summary>
	/// Regex to capture address options.
	/// </summary>
	[GeneratedRegex(@"<option\s+value=""(?<value>[^""]+)"">\s*(?<text>[^<]+)\s*</option>", RegexOptions.IgnoreCase)]
	private static partial Regex AddressOptionRegex();

	/// <summary>
	/// Regex to capture month headers.
	/// </summary>
	[GeneratedRegex(@"<th[^>]*>\s*(?<month>[A-Za-z]+)\s*</th>", RegexOptions.IgnoreCase)]
	private static partial Regex MonthHeaderRegex();

	/// <summary>
	/// Regex to capture bin collection rows.
	/// </summary>
	[GeneratedRegex(@"<td[^>]*>\s*(?<day>\d{2})\s*</td>\s*<td[^>]*>\s*(?<weekday>[^<]+)\s*</td>\s*<td[^>]*>\s*(?<service>[^<]+)\s*</td>", RegexOptions.IgnoreCase)]
	private static partial Regex BinRowRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for initial page load (Cloudflare cookie challenge)
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _pageUrl,
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getAddressesResponse;
		}
		// Retry with Cloudflare cookie to load the actual form page
		else if (clientSideResponse.RequestId == 1)
		{
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = _pageUrl,
				Method = "GET",
				Headers = new()
				{
					{ "cookie", cookies },
					{ "user-agent", Constants.UserAgent },
				},
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "cookie", cookies },
						{ "postcode", postcode },
					},
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getAddressesResponse;
		}
		// Prepare form submission with postcode to retrieve addresses
		else if (clientSideResponse.RequestId == 2)
		{
			var metadata = clientSideResponse.Options.Metadata;
			var cookies = metadata["cookie"];
			if (clientSideResponse.Headers.TryGetValue("set-cookie", out var setCookieHeader))
			{
				cookies = $"{cookies}; {ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader!)}";
			}

			var hiddenFields = HiddenFieldRegex().Matches(clientSideResponse.Content)!;
			var hiddenFieldValues = hiddenFields.ToDictionary(
				x => x.Groups["name"].Value,
				x => x.Groups["value"].Value
			);

			var pageSessionId = hiddenFieldValues["BINCOLLECTIONCHECKER_PAGESESSIONID"];
			var sessionId = hiddenFieldValues["BINCOLLECTIONCHECKER_SESSIONID"];
			var nonce = hiddenFieldValues["BINCOLLECTIONCHECKER_NONCE"];
			var storedPostcode = metadata["postcode"];

			var formData = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "BINCOLLECTIONCHECKER_PAGESESSIONID", pageSessionId },
				{ "BINCOLLECTIONCHECKER_SESSIONID", sessionId },
				{ "BINCOLLECTIONCHECKER_NONCE", nonce },
				{ "BINCOLLECTIONCHECKER_VARIABLES", "e30=" },
				{ "BINCOLLECTIONCHECKER_PAGENAME", "ADDRESSSEARCH" },
				{ "BINCOLLECTIONCHECKER_PAGEINSTANCE", "0" },
				{ "BINCOLLECTIONCHECKER_ADDRESSSEARCH_ASSISTON", "true" },
				{ "BINCOLLECTIONCHECKER_ADDRESSSEARCH_ADDRESSLOOKUPPOSTCODE", storedPostcode },
				{ "BINCOLLECTIONCHECKER_ADDRESSSEARCH_ADDRESSLOOKUPADDRESS", "0" },
				{ "BINCOLLECTIONCHECKER_FORMACTION_NEXT", "BINCOLLECTIONCHECKER_ADDRESSSEARCH_NEXTBUTTON" },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = $"{_processSubmissionUrl}?pageSessionId={pageSessionId}&fsid={sessionId}&fsn={nonce}",
				Method = "POST",
				Headers = new()
				{
					{ "content-type", "application/x-www-form-urlencoded" },
					{ "cookie", cookies },
					{ "user-agent", Constants.UserAgent },
				},
				Body = formData,
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "cookie", cookies },
						{ "postcode", storedPostcode },
					},
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getAddressesResponse;
		}
		// Follow verify cookie redirect
		else if (clientSideResponse.RequestId == 3)
		{
			var metadata = clientSideResponse.Options.Metadata;
			var cookies = metadata["cookie"];
			if (clientSideResponse.Headers.TryGetValue("set-cookie", out var setCookieHeader))
			{
				cookies = $"{cookies}; {ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader!)}";
			}

			var verifyCookieUrl = clientSideResponse.Headers["location"];

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 4,
				Url = verifyCookieUrl,
				Method = "GET",
				Headers = new()
				{
					{ "cookie", cookies },
					{ "referer", _pageUrl },
					{ "user-agent", Constants.UserAgent },
				},
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "cookie", cookies },
						{ "postcode", metadata["postcode"] },
					},
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getAddressesResponse;
		}
		// Follow redirect to page containing address options
		else if (clientSideResponse.RequestId == 4)
		{
			var metadata = clientSideResponse.Options.Metadata;
			var cookies = metadata["cookie"];
			if (clientSideResponse.Headers.TryGetValue("set-cookie", out var setCookieHeader))
			{
				cookies = $"{cookies}; {ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader!)}";
			}

			var redirectedUrl = clientSideResponse.Headers["location"];

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 5,
				Url = redirectedUrl,
				Method = "GET",
				Headers = new()
				{
					{ "cookie", cookies },
					{ "referer", _pageUrl },
					{ "user-agent", Constants.UserAgent },
				},
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "postcode", metadata["postcode"] },
					},
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 5)
		{
			var metadata = clientSideResponse.Options.Metadata;
			var rawAddresses = AddressOptionRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				var uid = rawAddress.Groups["value"].Value.Trim();
				var property = rawAddress.Groups["text"].Value.Trim();

				if (uid == "0" || property.Equals("Select an address..", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				var address = new Address
				{
					Property = property,
					Postcode = metadata["postcode"],
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

		// Throw exception for invalid request
		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <inheritdoc/>
	public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for initial page load (Cloudflare cookie challenge)
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _pageUrl,
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getBinDaysResponse;
		}
		// Retry with Cloudflare cookie to load the actual form page
		else if (clientSideResponse.RequestId == 1)
		{
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = _pageUrl,
				Method = "GET",
				Headers = new()
				{
					{ "cookie", cookies },
					{ "user-agent", Constants.UserAgent },
				},
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "cookie", cookies },
						{ "postcode", address.Postcode! },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getBinDaysResponse;
		}
		// Prepare form submission with selected address
		else if (clientSideResponse.RequestId == 2)
		{
			var metadata = clientSideResponse.Options.Metadata;
			var cookies = metadata["cookie"];
			if (clientSideResponse.Headers.TryGetValue("set-cookie", out var setCookieHeader))
			{
				cookies = $"{cookies}; {ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader!)}";
			}

			var hiddenFields = HiddenFieldRegex().Matches(clientSideResponse.Content)!;
			var hiddenFieldValues = hiddenFields.ToDictionary(
				x => x.Groups["name"].Value,
				x => x.Groups["value"].Value
			);

			var pageSessionId = hiddenFieldValues["BINCOLLECTIONCHECKER_PAGESESSIONID"];
			var sessionId = hiddenFieldValues["BINCOLLECTIONCHECKER_SESSIONID"];
			var nonce = hiddenFieldValues["BINCOLLECTIONCHECKER_NONCE"];
			var postcode = metadata["postcode"];

			var formData = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "BINCOLLECTIONCHECKER_PAGESESSIONID", pageSessionId },
				{ "BINCOLLECTIONCHECKER_SESSIONID", sessionId },
				{ "BINCOLLECTIONCHECKER_NONCE", nonce },
				{ "BINCOLLECTIONCHECKER_VARIABLES", "e30=" },
				{ "BINCOLLECTIONCHECKER_PAGENAME", "ADDRESSSEARCH" },
				{ "BINCOLLECTIONCHECKER_PAGEINSTANCE", "0" },
				{ "BINCOLLECTIONCHECKER_ADDRESSSEARCH_ASSISTON", "true" },
				{ "BINCOLLECTIONCHECKER_ADDRESSSEARCH_ADDRESSLOOKUPPOSTCODE", postcode },
				{ "BINCOLLECTIONCHECKER_ADDRESSSEARCH_ADDRESSLOOKUPADDRESS", "0" },
				{ "BINCOLLECTIONCHECKER_ADDRESSSEARCH_UPRN", address.Uid! },
				{ "BINCOLLECTIONCHECKER_ADDRESSSEARCH_ADDRESSTEXT", address.Property! },
				{ "BINCOLLECTIONCHECKER_FORMACTION_NEXT", "BINCOLLECTIONCHECKER_ADDRESSSEARCH_NEXTBUTTON" },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = $"{_processSubmissionUrl}?pageSessionId={pageSessionId}&fsid={sessionId}&fsn={nonce}",
				Method = "POST",
				Headers = new()
				{
					{ "content-type", "application/x-www-form-urlencoded" },
					{ "cookie", cookies },
					{ "user-agent", Constants.UserAgent },
				},
				Body = formData,
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "cookie", cookies },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getBinDaysResponse;
		}
		// Follow verify cookie redirect
		else if (clientSideResponse.RequestId == 3)
		{
			var metadata = clientSideResponse.Options.Metadata;
			var cookies = metadata["cookie"];
			if (clientSideResponse.Headers.TryGetValue("set-cookie", out var setCookieHeader))
			{
				cookies = $"{cookies}; {ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader!)}";
			}

			var verifyCookieUrl = clientSideResponse.Headers["location"];

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 4,
				Url = verifyCookieUrl,
				Method = "GET",
				Headers = new()
				{
					{ "cookie", cookies },
					{ "referer", _pageUrl },
					{ "user-agent", Constants.UserAgent },
				},
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "cookie", cookies },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getBinDaysResponse;
		}
		// Follow redirect to page with bin days
		else if (clientSideResponse.RequestId == 4)
		{
			var metadata = clientSideResponse.Options.Metadata;
			var cookies = metadata["cookie"];
			if (clientSideResponse.Headers.TryGetValue("set-cookie", out var setCookieHeader))
			{
				cookies = $"{cookies}; {ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader!)}";
			}

			var redirectedUrl = clientSideResponse.Headers["location"];

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 5,
				Url = redirectedUrl,
				Method = "GET",
				Headers = new()
				{
					{ "cookie", cookies },
					{ "referer", _pageUrl },
					{ "user-agent", Constants.UserAgent },
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getBinDaysResponse;
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 5)
		{
			var monthHeaders = new List<(int Index, string Month)>();
			foreach (Match monthHeader in MonthHeaderRegex().Matches(clientSideResponse.Content)!)
			{
				monthHeaders.Add((monthHeader.Index, monthHeader.Groups["month"].Value.Trim()));
			}

			var binDays = new List<BinDay>();
			foreach (Match binRow in BinRowRegex().Matches(clientSideResponse.Content)!)
			{
				var month = monthHeaders.Last(x => x.Index < binRow.Index).Month;
				var day = binRow.Groups["day"].Value.Trim();
				var service = binRow.Groups["service"].Value.Trim();

				var date = $"{day} {month}".ParseDateInferringYear("dd MMMM");

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

		// Throw exception for invalid request
		throw new InvalidOperationException("Invalid client-side request.");
	}
}
