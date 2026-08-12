namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Boston Borough Council.
/// </summary>
internal sealed partial class BostonBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Boston Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.boston.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "boston";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Keys = [ "Green Refuse Bin" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Blue Recycling Bin" ],
		},
		new()
		{
			Name = "Paper/Cardboard Recycling",
			Colour = BinColour.Purple,
			Keys = [ "Purple/Purple-lidded Paper/Card Bin" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Brown Garden Waste Bin" ],
		},
	];

	/// <summary>
	/// The URL of the waste collections lookup page.
	/// </summary>
	private const string _pageUrl = "https://www.boston.gov.uk/article/27449/Your-Waste-Collections";

	/// <summary>
	/// The URL for processing form submissions.
	/// </summary>
	private const string _processSubmissionUrl = "https://www.boston.gov.uk/apiserver/formsservice/http/processsubmission";

	/// <summary>
	/// Regex to capture hidden form fields.
	/// </summary>
	[GeneratedRegex(@"name=""(?<name>BBCWASTECOLLECTIONSV2_[A-Z]+)"" value=""(?<value>[^""]*)""")]
	private static partial Regex HiddenFieldRegex();

	/// <summary>
	/// Regex to capture addresses from the address selection dropdown.
	/// </summary>
	[GeneratedRegex(@"<option value=""(?<uprn>\d+)""\s*>(?<address>[^<]+)</option>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex to capture bin collection entries from the results page.
	/// </summary>
	[GeneratedRegex(@"<a class=""item__link"" href=""[^""]*"">(?<service>[^<]+)</a></h2>\s*<div><strong>Next:\s*</strong>(?<date>[^<]+)</div>", RegexOptions.Singleline)]
	private static partial Regex BinDaysRegex();

	/// <summary>
	/// Regex for removing ordinal suffixes from date strings.
	/// </summary>
	[GeneratedRegex(@"(?<=\d)(st|nd|rd|th)")]
	private static partial Regex OrdinalSuffixRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for the initial page load
		if (clientSideResponse == null)
		{
			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = CreateInitialPageRequest(),
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for submitting the postcode search
		else if (clientSideResponse.RequestId == 1)
		{
			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = CreatePostcodeSearchRequest(clientSideResponse, postcode),
			};

			return getAddressesResponse;
		}
		// Follow the verify cookie redirect
		else if (clientSideResponse.RequestId == 2)
		{
			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = CreateVerifyCookieRequest(clientSideResponse),
			};

			return getAddressesResponse;
		}
		// Follow the redirect to the page containing the address list
		else if (clientSideResponse.RequestId == 3)
		{
			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = CreateAddressListRequest(clientSideResponse),
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
				var uprn = rawAddress.Groups["uprn"].Value;

				var address = new Address
				{
					Property = rawAddress.Groups["address"].Value.Trim(),
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
			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = CreateInitialPageRequest(),
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for submitting the postcode search
		else if (clientSideResponse.RequestId == 1)
		{
			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = CreatePostcodeSearchRequest(clientSideResponse, address.Postcode!),
			};

			return getBinDaysResponse;
		}
		// Follow the verify cookie redirect
		else if (clientSideResponse.RequestId == 2)
		{
			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = CreateVerifyCookieRequest(clientSideResponse),
			};

			return getBinDaysResponse;
		}
		// Follow the redirect to the page containing the address list
		else if (clientSideResponse.RequestId == 3)
		{
			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = CreateAddressListRequest(clientSideResponse),
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for selecting the address
		else if (clientSideResponse.RequestId == 4)
		{
			var cookies = clientSideResponse.Options.Metadata["cookie"];
			var (pageSessionId, sessionId, nonce) = ParseHiddenFields(clientSideResponse.Content);

			var formData = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "BBCWASTECOLLECTIONSV2_PAGESESSIONID", pageSessionId },
				{ "BBCWASTECOLLECTIONSV2_SESSIONID", sessionId },
				{ "BBCWASTECOLLECTIONSV2_NONCE", nonce },
				{ "BBCWASTECOLLECTIONSV2_PAGENAME", "ADDRESS" },
				{ "BBCWASTECOLLECTIONSV2_ADDRESS_FIELD1041", "true" },
				{ "BBCWASTECOLLECTIONSV2_ADDRESS_FIELD1042", "true" },
				{ "BBCWASTECOLLECTIONSV2_ADDRESS_ADDRESSUPRN", address.Uid! },
				{ "BBCWASTECOLLECTIONSV2_FORMACTION_NEXT", "BBCWASTECOLLECTIONSV2_ADDRESS_NEXT3" },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 5,
				Url = $"{_processSubmissionUrl}?pageSessionId={pageSessionId}&fsid={sessionId}&fsn={nonce}",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
					{ "cookie", cookies },
				},
				Body = formData,
				Options = new ClientSideOptions
				{
					FollowRedirects = false,
					Metadata =
					{
						{ "cookie", cookies },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Follow the redirect to the page containing the bin days
		else if (clientSideResponse.RequestId == 5)
		{
			var cookies = clientSideResponse.Options.Metadata["cookie"];
			var resultsUrl = clientSideResponse.Headers["location"];

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 6,
				Url = resultsUrl,
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "cookie", cookies },
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 6)
		{
			var rawBinDays = BinDaysRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var service = rawBinDay.Groups["service"].Value.Trim();
				var dateString = OrdinalSuffixRegex().Replace(rawBinDay.Groups["date"].Value.Trim(), "");
				var date = DateUtilities.ParseDateInferringYear(dateString, "dddd d MMMM");

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
	/// Creates the initial client-side request for loading the waste collections page.
	/// </summary>
	private static ClientSideRequest CreateInitialPageRequest()
	{
		return new ClientSideRequest
		{
			RequestId = 1,
			Url = _pageUrl,
			Method = "GET",
		};
	}

	/// <summary>
	/// Creates the client-side request that submits the postcode search on the waste collections page.
	/// </summary>
	private static ClientSideRequest CreatePostcodeSearchRequest(ClientSideResponse clientSideResponse, string postcode)
	{
		var setCookieHeader = clientSideResponse.Headers["set-cookie"];
		var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

		var (pageSessionId, sessionId, nonce) = ParseHiddenFields(clientSideResponse.Content);

		var formData = ProcessingUtilities.ConvertDictionaryToFormData(new()
		{
			{ "BBCWASTECOLLECTIONSV2_PAGESESSIONID", pageSessionId },
			{ "BBCWASTECOLLECTIONSV2_SESSIONID", sessionId },
			{ "BBCWASTECOLLECTIONSV2_NONCE", nonce },
			{ "BBCWASTECOLLECTIONSV2_PAGENAME", "COLLECTIONS" },
			{ "BBCWASTECOLLECTIONSV2_COLLECTIONS_SEARCHPROPERTYNAMENUMBER", "" },
			{ "BBCWASTECOLLECTIONSV2_COLLECTIONS_SEARCHPOSTCODE", postcode },
			{ "BBCWASTECOLLECTIONSV2_FORMACTION_NEXT", "BBCWASTECOLLECTIONSV2_COLLECTIONS_START10" },
		});

		return new ClientSideRequest
		{
			RequestId = 2,
			Url = $"{_processSubmissionUrl}?pageSessionId={pageSessionId}&fsid={sessionId}&fsn={nonce}",
			Method = "POST",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
				{ "content-type", Constants.FormUrlEncoded },
				{ "cookie", cookies },
			},
			Body = formData,
			Options = new ClientSideOptions
			{
				FollowRedirects = false,
				Metadata =
				{
					{ "cookie", cookies },
				},
			},
		};
	}

	/// <summary>
	/// Creates the client-side request that follows the verify-cookie redirect after a form submission.
	/// </summary>
	private static ClientSideRequest CreateVerifyCookieRequest(ClientSideResponse clientSideResponse)
	{
		var cookies = clientSideResponse.Options.Metadata["cookie"];
		if (clientSideResponse.Headers.TryGetValue("set-cookie", out var setCookieHeader))
		{
			cookies = $"{cookies}; {ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader!)}";
		}

		var verifyCookieUrl = clientSideResponse.Headers["location"];

		return new ClientSideRequest
		{
			RequestId = 3,
			Url = verifyCookieUrl,
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
	}

	/// <summary>
	/// Creates the client-side request that follows the redirect to the page containing the address list.
	/// </summary>
	private static ClientSideRequest CreateAddressListRequest(ClientSideResponse clientSideResponse)
	{
		var cookies = clientSideResponse.Options.Metadata["cookie"];
		var addressListUrl = clientSideResponse.Headers["location"];

		return new ClientSideRequest
		{
			RequestId = 4,
			Url = addressListUrl,
			Method = "GET",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
				{ "cookie", cookies },
			},
			Options = new ClientSideOptions
			{
				Metadata =
				{
					{ "cookie", cookies },
				},
			},
		};
	}

	/// <summary>
	/// Parses the page session ID, session ID, and nonce hidden fields from a form page response.
	/// </summary>
	private static (string PageSessionId, string SessionId, string Nonce) ParseHiddenFields(string content)
	{
		var hiddenFields = HiddenFieldRegex().Matches(content)!;
		var hiddenFieldValues = hiddenFields.ToDictionary(
			x => x.Groups["name"].Value,
			x => x.Groups["value"].Value
		);

		return (
			hiddenFieldValues["BBCWASTECOLLECTIONSV2_PAGESESSIONID"],
			hiddenFieldValues["BBCWASTECOLLECTIONSV2_SESSIONID"],
			hiddenFieldValues["BBCWASTECOLLECTIONSV2_NONCE"]
		);
	}
}
