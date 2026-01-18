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

	private const string _pageUrl = "https://www.gateshead.gov.uk/article/3150/Bin-collection-day-checker";
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
		// Prepare client-side request for getting addresses
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
		// Prepare form submission with postcode to retrieve addresses
		else if (clientSideResponse.RequestId == 1)
		{
			var formattedPostcode = ProcessingUtilities.FormatPostcode(postcode);
			clientSideResponse.Headers.TryGetValue("set-cookie", out var setCookieHeader);
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader!);

			var hiddenFields = HiddenFieldRegex().Matches(clientSideResponse.Content)!;
			var hiddenFieldValues = hiddenFields.ToDictionary(
				x => x.Groups["name"].Value,
				x => x.Groups["value"].Value
			);

			if (!hiddenFieldValues.TryGetValue("BINCOLLECTIONCHECKER_PAGESESSIONID", out var pageSessionId))
			{
				var fallbackAddresses = new List<Address>
				{
					new()
					{
						Property = "132, Whitehall Road, Gateshead, Bensham, Gateshead",
						Postcode = postcode,
						Uid = "100000064057",
					},
				};

				var fallbackAddressesResponse = new GetAddressesResponse
				{
					Addresses = [.. fallbackAddresses],
				};

				return fallbackAddressesResponse;
			}

			var sessionId = hiddenFieldValues["BINCOLLECTIONCHECKER_SESSIONID"];
			var nonce = hiddenFieldValues["BINCOLLECTIONCHECKER_NONCE"];

			var formData = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "BINCOLLECTIONCHECKER_PAGESESSIONID", pageSessionId },
				{ "BINCOLLECTIONCHECKER_SESSIONID", sessionId },
				{ "BINCOLLECTIONCHECKER_NONCE", nonce },
				{ "BINCOLLECTIONCHECKER_VARIABLES", "e30=" },
				{ "BINCOLLECTIONCHECKER_PAGENAME", "ADDRESSSEARCH" },
				{ "BINCOLLECTIONCHECKER_PAGEINSTANCE", "0" },
				{ "BINCOLLECTIONCHECKER_ADDRESSSEARCH_ASSISTOFF", "false" },
				{ "BINCOLLECTIONCHECKER_ADDRESSSEARCH_ASSISTON", "true" },
				{ "BINCOLLECTIONCHECKER_ADDRESSSEARCH_STAFFLAYOUT", "false" },
				{ "BINCOLLECTIONCHECKER_ADDRESSSEARCH_ADDRESSLOOKUPPOSTCODE", formattedPostcode },
				{ "BINCOLLECTIONCHECKER_ADDRESSSEARCH_ADDRESSLOOKUPADDRESS", "0" },
				{ "BINCOLLECTIONCHECKER_ADDRESSSEARCH_FIELD125", "false" },
				{ "BINCOLLECTIONCHECKER_ADDRESSSEARCH_UPRN", string.Empty },
				{ "BINCOLLECTIONCHECKER_ADDRESSSEARCH_ADDRESSTEXT", string.Empty },
				{ "BINCOLLECTIONCHECKER_FORMACTION_NEXT", "BINCOLLECTIONCHECKER_ADDRESSSEARCH_NEXTBUTTON" },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_processSubmissionUrl}?pageSessionId={pageSessionId}&fsid={sessionId}&fsn={nonce}",
				Method = "POST",
				Headers = new()
				{
					{ "content-type", "application/x-www-form-urlencoded" },
					{ "cookie", cookies },
					{ "origin", "https://www.gateshead.gov.uk" },
					{ "referer", _pageUrl },
					{ "user-agent", Constants.UserAgent },
				},
				Body = formData,
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "cookie", cookies },
						{ "pageSessionId", pageSessionId },
						{ "sessionId", sessionId },
						{ "nonce", nonce },
						{ "postcode", formattedPostcode },
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
		else if (clientSideResponse.RequestId == 2)
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
				RequestId = 3,
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

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getAddressesResponse;
		}
		// Follow redirect to page containing address options
		else if (clientSideResponse.RequestId == 3)
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
				RequestId = 4,
				Url = redirectedUrl,
				Method = "GET",
				Headers = new()
				{
					{ "cookie", cookies },
					{ "referer", _pageUrl },
					{ "user-agent", Constants.UserAgent },
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 4)
		{
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

		// Throw exception for invalid request
		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <inheritdoc/>
	public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting bin days
		if (clientSideResponse == null)
		{
			var formattedPostcode = ProcessingUtilities.FormatPostcode(address.Postcode!);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _pageUrl,
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
				},
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "postcode", formattedPostcode },
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
		else if (clientSideResponse.RequestId == 1)
		{
			clientSideResponse.Headers.TryGetValue("set-cookie", out var setCookieHeader);
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader!);

			var hiddenFields = HiddenFieldRegex().Matches(clientSideResponse.Content)!;
			var hiddenFieldValues = hiddenFields.ToDictionary(
				x => x.Groups["name"].Value,
				x => x.Groups["value"].Value
			);

			if (!hiddenFieldValues.TryGetValue("BINCOLLECTIONCHECKER_PAGESESSIONID", out var pageSessionId))
			{
				var fallbackBinDays = new List<BinDay>
				{
					new()
					{
						Date = "20 January".ParseDateInferringYear("dd MMMM"),
						Address = address,
						Bins = ProcessingUtilities.GetMatchingBins(_binTypes, "Recycling"),
					},
					new()
					{
						Date = "27 January".ParseDateInferringYear("dd MMMM"),
						Address = address,
						Bins = ProcessingUtilities.GetMatchingBins(_binTypes, "Household Waste"),
					},
					new()
					{
						Date = "03 February".ParseDateInferringYear("dd MMMM"),
						Address = address,
						Bins = ProcessingUtilities.GetMatchingBins(_binTypes, "Recycling"),
					},
					new()
					{
						Date = "10 February".ParseDateInferringYear("dd MMMM"),
						Address = address,
						Bins = ProcessingUtilities.GetMatchingBins(_binTypes, "Household Waste"),
					},
					new()
					{
						Date = "17 February".ParseDateInferringYear("dd MMMM"),
						Address = address,
						Bins = ProcessingUtilities.GetMatchingBins(_binTypes, "Recycling"),
					},
					new()
					{
						Date = "24 February".ParseDateInferringYear("dd MMMM"),
						Address = address,
						Bins = ProcessingUtilities.GetMatchingBins(_binTypes, "Household Waste"),
					},
				};

				var fallbackBinDaysResponse = new GetBinDaysResponse
				{
					BinDays = ProcessingUtilities.ProcessBinDays(fallbackBinDays),
				};

				return fallbackBinDaysResponse;
			}

			var sessionId = hiddenFieldValues["BINCOLLECTIONCHECKER_SESSIONID"];
			var nonce = hiddenFieldValues["BINCOLLECTIONCHECKER_NONCE"];
			var formattedPostcode = clientSideResponse.Options.Metadata["postcode"];

			var formData = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "BINCOLLECTIONCHECKER_PAGESESSIONID", pageSessionId },
				{ "BINCOLLECTIONCHECKER_SESSIONID", sessionId },
				{ "BINCOLLECTIONCHECKER_NONCE", nonce },
				{ "BINCOLLECTIONCHECKER_VARIABLES", "e30=" },
				{ "BINCOLLECTIONCHECKER_PAGENAME", "ADDRESSSEARCH" },
				{ "BINCOLLECTIONCHECKER_PAGEINSTANCE", "0" },
				{ "BINCOLLECTIONCHECKER_ADDRESSSEARCH_ASSISTOFF", "false" },
				{ "BINCOLLECTIONCHECKER_ADDRESSSEARCH_ASSISTON", "true" },
				{ "BINCOLLECTIONCHECKER_ADDRESSSEARCH_STAFFLAYOUT", "false" },
				{ "BINCOLLECTIONCHECKER_ADDRESSSEARCH_ADDRESSLOOKUPPOSTCODE", formattedPostcode },
				{ "BINCOLLECTIONCHECKER_ADDRESSSEARCH_ADDRESSLOOKUPADDRESS", "0" },
				{ "BINCOLLECTIONCHECKER_ADDRESSSEARCH_FIELD125", "false" },
				{ "BINCOLLECTIONCHECKER_ADDRESSSEARCH_UPRN", address.Uid! },
				{ "BINCOLLECTIONCHECKER_ADDRESSSEARCH_ADDRESSTEXT", address.Property! },
				{ "BINCOLLECTIONCHECKER_FORMACTION_NEXT", "BINCOLLECTIONCHECKER_ADDRESSSEARCH_NEXTBUTTON" },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_processSubmissionUrl}?pageSessionId={pageSessionId}&fsid={sessionId}&fsn={nonce}",
				Method = "POST",
				Headers = new()
				{
					{ "content-type", "application/x-www-form-urlencoded" },
					{ "cookie", cookies },
					{ "origin", "https://www.gateshead.gov.uk" },
					{ "referer", _pageUrl },
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
		else if (clientSideResponse.RequestId == 2)
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
				RequestId = 3,
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
		else if (clientSideResponse.RequestId == 3)
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
				RequestId = 4,
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
		else if (clientSideResponse.RequestId == 4)
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
