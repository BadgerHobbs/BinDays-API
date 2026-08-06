namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
	/// The URL of the bin collection day checker page.
	/// </summary>
	private const string _pageUrl = "https://www.gateshead.gov.uk/article/3150/Bin-collection-day-checker";

	/// <summary>
	/// The URL for processing form submissions.
	/// </summary>
	private const string _processSubmissionUrl = "https://www.gateshead.gov.uk/apiserver/formsservice/http/processsubmission";

	/// <summary>
	/// Regex to parse JSONP responses.
	/// </summary>
	[GeneratedRegex(@"^[^(]+\((?<json>.*)\)$", RegexOptions.Singleline)]
	private static partial Regex JsonpRegex();

	/// <summary>
	/// Regex to capture hidden form fields.
	/// </summary>
	[GeneratedRegex(@"name=""(?<name>BINCOLLECTIONCHECKER_[^""]+)"" value=""(?<value>[^""]*)""")]
	private static partial Regex HiddenFieldRegex();

	/// <summary>
	/// Regex to capture month headers from the collection table.
	/// </summary>
	[GeneratedRegex(@"<th colspan=""3"">(?<month>[A-Za-z]+)</th>")]
	private static partial Regex MonthHeaderRegex();

	/// <summary>
	/// Regex to capture bin collection rows from the collection table.
	/// </summary>
	[GeneratedRegex(@"<td>(?<day>\d{2})</td>\s*<td>\s*(?<weekday>[A-Za-z]+)\s*</td>\s*<td>\s*<a[^>]*>\s*(?<service>[^<]+?)\s*</a>")]
	private static partial Regex BinRowRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for the postcode search
		if (clientSideResponse == null)
		{
			var jsonPayload = $$$"""
			{"id":1,"method":"postcodeSearch","params":{"provider":"EndPoint","postcode":"{{{postcode}}}"}}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://www.gateshead.gov.uk/apiserver/postcode?callback=cb&jsonrpc={Uri.EscapeDataString(jsonPayload)}",
				Method = "GET",
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 1)
		{
			var json = JsonpRegex().Match(clientSideResponse.Content).Groups["json"].Value;

			using var jsonDoc = JsonDocument.Parse(json);

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var addressElement in jsonDoc.RootElement.GetProperty("result").EnumerateArray())
			{
				var udprn = addressElement.GetProperty("udprn").GetString()!;

				string[] addressParts =
				[
					addressElement.GetProperty("line1").GetString()!,
					addressElement.GetProperty("line2").GetString()!,
					addressElement.GetProperty("line3").GetString()!,
					addressElement.GetProperty("town").GetString()!,
					addressElement.GetProperty("county").GetString()!,
				];

				var property = string.Join(", ", addressParts.Where(p => !string.IsNullOrWhiteSpace(p)));

				// Uid format: "udprn;property" - property is not passed back to GetBinDays otherwise
				var address = new Address
				{
					Property = property,
					Postcode = postcode,
					Uid = $"{udprn};{property}",
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
				NextClientSideRequest = clientSideRequest
			};

			return getBinDaysResponse;
		}
		// Prepare form submission with the selected address
		else if (clientSideResponse.RequestId == 1)
		{
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var hiddenFields = HiddenFieldRegex().Matches(clientSideResponse.Content)!;
			var hiddenFieldValues = hiddenFields.ToDictionary(
				x => x.Groups["name"].Value,
				x => x.Groups["value"].Value
			);

			var pageSessionId = hiddenFieldValues["BINCOLLECTIONCHECKER_PAGESESSIONID"];
			var sessionId = hiddenFieldValues["BINCOLLECTIONCHECKER_SESSIONID"];
			var nonce = hiddenFieldValues["BINCOLLECTIONCHECKER_NONCE"];

			// Uid format: "udprn;property"
			var uidParts = address.Uid!.Split(';', 2);
			var udprn = uidParts[0];
			var property = uidParts[1];

			var formData = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "BINCOLLECTIONCHECKER_PAGESESSIONID", pageSessionId },
				{ "BINCOLLECTIONCHECKER_SESSIONID", sessionId },
				{ "BINCOLLECTIONCHECKER_NONCE", nonce },
				{ "BINCOLLECTIONCHECKER_PAGENAME", "ADDRESSSEARCH" },
				{ "BINCOLLECTIONCHECKER_ADDRESSSEARCH_ADDRESSLOOKUPADDRESS", "0" },
				{ "BINCOLLECTIONCHECKER_ADDRESSSEARCH_UPRN", udprn },
				{ "BINCOLLECTIONCHECKER_ADDRESSSEARCH_ADDRESSTEXT", property },
				{ "BINCOLLECTIONCHECKER_FORMACTION_NEXT", "BINCOLLECTIONCHECKER_ADDRESSSEARCH_NEXTBUTTON" },
			});

			var clientSideRequest = new ClientSideRequest
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

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getBinDaysResponse;
		}
		// Follow the verify cookie redirect
		else if (clientSideResponse.RequestId == 2)
		{
			var cookies = clientSideResponse.Options.Metadata["cookie"];
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

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getBinDaysResponse;
		}
		// Follow the redirect to the page containing the bin days
		else if (clientSideResponse.RequestId == 3)
		{
			var cookies = clientSideResponse.Options.Metadata["cookie"];
			var resultsUrl = clientSideResponse.Headers["location"];

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 4,
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
				monthHeaders.Add((monthHeader.Index, monthHeader.Groups["month"].Value));
			}

			// Iterate through each bin row, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match binRow in BinRowRegex().Matches(clientSideResponse.Content)!)
			{
				var month = monthHeaders.Last(x => x.Index < binRow.Index).Month;
				var day = binRow.Groups["day"].Value;
				var service = binRow.Groups["service"].Value;

				var date = DateUtilities.ParseDateInferringYear($"{day} {month}", "dd MMMM");

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
}
