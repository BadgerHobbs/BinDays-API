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
/// Collector implementation for East Lindsey District Council.
/// </summary>
internal sealed partial class EastLindseyDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "East Lindsey District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.e-lindsey.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "east-lindsey";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "Domestic Waste",
			Colour = BinColour.Black,
			Keys = [ "wastenextref" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Grey,
			Keys = [ "wastenextrec" ],
		},
		new()
		{
			Name = "Paper Recycling",
			Colour = BinColour.Purple,
			Keys = [ "wastenextpur" ],
		},
		new()
		{
			Name = "Green Waste (Subscription Required)",
			Colour = BinColour.Green,
			Keys = [ "greenfirst" ],
		},
	];

	/// <summary>
	/// Full-season collection dates for each green waste round, keyed as "{Day}{Week}" (e.g. "MondayA").
	/// The live API only ever exposes "greenfirst" - a single fixed season-start date per round, already
	/// in the past for most of the year - with no field for subsequent collections. These dates are
	/// transcribed from the council's own published PDF calendars (linked from the results page's
	/// "Downloads" section) for the 2026/27 season, since every address on a round is collected on the
	/// same identical dates regardless of subscription status. Must be extended with next season's
	/// calendar before a round's dates run out; GetBinDays throws rather than falling silent when that
	/// happens.
	/// </summary>
	private static readonly Dictionary<string, DateOnly[]> _greenWasteCalendar = new()
	{
		["MondayA"] = [
			new(2026, 3, 30), new(2026, 4, 13), new(2026, 4, 27), new(2026, 5, 11), new(2026, 5, 25),
			new(2026, 6, 8), new(2026, 6, 22), new(2026, 7, 6), new(2026, 7, 20), new(2026, 8, 3),
			new(2026, 8, 17), new(2026, 8, 31), new(2026, 9, 14), new(2026, 9, 28), new(2026, 10, 12),
			new(2026, 10, 26), new(2026, 11, 9), new(2026, 12, 7), new(2027, 1, 18), new(2027, 2, 15),
			new(2027, 3, 15),
		],
		["MondayB"] = [
			new(2026, 4, 6), new(2026, 4, 20), new(2026, 5, 4), new(2026, 5, 18), new(2026, 6, 1),
			new(2026, 6, 15), new(2026, 6, 29), new(2026, 7, 13), new(2026, 7, 27), new(2026, 8, 10),
			new(2026, 8, 24), new(2026, 9, 7), new(2026, 9, 21), new(2026, 10, 5), new(2026, 10, 19),
			new(2026, 11, 2), new(2026, 11, 30), new(2027, 1, 11), new(2027, 2, 8), new(2027, 3, 8),
			new(2027, 3, 22),
		],
		["TuesdayA"] = [
			new(2026, 3, 31), new(2026, 4, 14), new(2026, 4, 28), new(2026, 5, 12), new(2026, 5, 26),
			new(2026, 6, 9), new(2026, 6, 23), new(2026, 7, 7), new(2026, 7, 21), new(2026, 8, 4),
			new(2026, 8, 18), new(2026, 9, 1), new(2026, 9, 15), new(2026, 9, 29), new(2026, 10, 13),
			new(2026, 10, 27), new(2026, 11, 10), new(2026, 12, 8), new(2027, 1, 19), new(2027, 2, 16),
			new(2027, 3, 16),
		],
		["TuesdayB"] = [
			new(2026, 4, 7), new(2026, 4, 21), new(2026, 5, 5), new(2026, 5, 19), new(2026, 6, 2),
			new(2026, 6, 16), new(2026, 6, 30), new(2026, 7, 14), new(2026, 7, 28), new(2026, 8, 11),
			new(2026, 8, 25), new(2026, 9, 8), new(2026, 9, 22), new(2026, 10, 6), new(2026, 10, 20),
			new(2026, 11, 3), new(2026, 12, 1), new(2027, 1, 12), new(2027, 2, 9), new(2027, 3, 9),
			new(2027, 3, 23),
		],
		["WednesdayA"] = [
			new(2026, 4, 1), new(2026, 4, 15), new(2026, 4, 29), new(2026, 5, 13), new(2026, 5, 27),
			new(2026, 6, 10), new(2026, 6, 24), new(2026, 7, 8), new(2026, 7, 22), new(2026, 8, 5),
			new(2026, 8, 19), new(2026, 9, 2), new(2026, 9, 16), new(2026, 9, 30), new(2026, 10, 14),
			new(2026, 10, 28), new(2026, 11, 11), new(2026, 12, 9), new(2027, 1, 20), new(2027, 2, 17),
			new(2027, 3, 17),
		],
		["WednesdayB"] = [
			new(2026, 4, 8), new(2026, 4, 22), new(2026, 5, 6), new(2026, 5, 20), new(2026, 6, 3),
			new(2026, 6, 17), new(2026, 7, 1), new(2026, 7, 15), new(2026, 7, 29), new(2026, 8, 12),
			new(2026, 8, 26), new(2026, 9, 9), new(2026, 9, 23), new(2026, 10, 7), new(2026, 10, 21),
			new(2026, 11, 4), new(2026, 12, 2), new(2027, 1, 13), new(2027, 2, 10), new(2027, 3, 10),
			new(2027, 3, 24),
		],
		["ThursdayA"] = [
			new(2026, 4, 2), new(2026, 4, 16), new(2026, 4, 30), new(2026, 5, 14), new(2026, 5, 28),
			new(2026, 6, 11), new(2026, 6, 25), new(2026, 7, 9), new(2026, 7, 23), new(2026, 8, 6),
			new(2026, 8, 20), new(2026, 9, 3), new(2026, 9, 17), new(2026, 10, 1), new(2026, 10, 15),
			new(2026, 10, 29), new(2026, 11, 12), new(2026, 12, 10), new(2027, 1, 21), new(2027, 2, 18),
			new(2027, 3, 18),
		],
		["ThursdayB"] = [
			new(2026, 4, 9), new(2026, 4, 23), new(2026, 5, 7), new(2026, 5, 21), new(2026, 6, 4),
			new(2026, 6, 18), new(2026, 7, 2), new(2026, 7, 16), new(2026, 7, 30), new(2026, 8, 13),
			new(2026, 8, 27), new(2026, 9, 10), new(2026, 9, 24), new(2026, 10, 8), new(2026, 10, 22),
			new(2026, 11, 5), new(2026, 12, 3), new(2027, 1, 14), new(2027, 2, 11), new(2027, 3, 11),
			new(2027, 3, 25),
		],
		["FridayA"] = [
			new(2026, 4, 3), new(2026, 4, 17), new(2026, 5, 1), new(2026, 5, 15), new(2026, 5, 29),
			new(2026, 6, 12), new(2026, 6, 26), new(2026, 7, 10), new(2026, 7, 24), new(2026, 8, 7),
			new(2026, 8, 21), new(2026, 9, 4), new(2026, 9, 18), new(2026, 10, 2), new(2026, 10, 16),
			new(2026, 10, 30), new(2026, 11, 13), new(2026, 12, 11), new(2027, 1, 22), new(2027, 2, 19),
			new(2027, 3, 19),
		],
		["FridayB"] = [
			new(2026, 4, 10), new(2026, 4, 24), new(2026, 5, 8), new(2026, 5, 22), new(2026, 6, 5),
			new(2026, 6, 19), new(2026, 7, 3), new(2026, 7, 17), new(2026, 7, 31), new(2026, 8, 14),
			new(2026, 8, 28), new(2026, 9, 11), new(2026, 9, 25), new(2026, 10, 9), new(2026, 10, 23),
			new(2026, 11, 6), new(2026, 12, 4), new(2027, 1, 15), new(2027, 2, 12), new(2027, 3, 12),
			new(2027, 3, 26),
		],
	};

	/// <summary>
	/// Regex to unwrap the JSONP response from the postcode search.
	/// </summary>
	[GeneratedRegex(@"^[^(]+\((?<json>.*)\)$", RegexOptions.Singleline)]
	private static partial Regex JsonpRegex();

	/// <summary>
	/// Regex to capture the GOSS form prefix and its session hidden field values.
	/// </summary>
	[GeneratedRegex(@"name=""(?<prefix>[A-Z0-9]+)_(?<field>PAGESESSIONID|SESSIONID|NONCE)"" value=""(?<value>[^""]*)""")]
	private static partial Regex FormFieldRegex();

	/// <summary>
	/// Regex to capture the base64-encoded form data blob embedded in the results page.
	/// The collection dates are rendered client-side from this data, not present as static HTML.
	/// </summary>
	[GeneratedRegex(@"[A-Z0-9]+FormData\s*=\s*""(?<data>[^""]+)""")]
	private static partial Regex FormDataRegex();

	/// <summary>
	/// Regex to remove ordinal suffixes from dates.
	/// </summary>
	[GeneratedRegex(@"(?<=\d)(st|nd|rd|th)")]
	private static partial Regex OrdinalSuffixRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for the postcode search
		if (clientSideResponse == null)
		{
			var jsonPayload = $$$"""
			{"id":1,"method":"postcodeSearch","params":{"postcode":"{{{postcode}}}"}}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://www.e-lindsey.gov.uk/apiserver/postcode?callback=cb&jsonrpc={Uri.EscapeDataString(jsonPayload)}",
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
			var json = JsonpRegex().Match(clientSideResponse.Content).Groups["json"].Value;

			using var jsonDoc = JsonDocument.Parse(json);

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var addressElement in jsonDoc.RootElement.GetProperty("result").EnumerateArray())
			{
				var uprn = addressElement.GetProperty("uprn").GetString()!;
				var source = addressElement.GetProperty("source").GetString()!;

				string[] addressParts =
				[
					addressElement.GetProperty("line1").GetString()!,
					addressElement.GetProperty("line2").GetString()!,
					addressElement.GetProperty("line3").GetString()!,
					addressElement.GetProperty("line4").GetString()!,
					addressElement.GetProperty("line5").GetString()!,
					addressElement.GetProperty("town").GetString()!,
					addressElement.GetProperty("county").GetString()!,
					addressElement.GetProperty("postcode").GetString()!,
				];

				var property = string.Join(", ", addressParts.Where(p => !string.IsNullOrWhiteSpace(p)));

				// Uid format: "uprn;source;property" - source and property are required by the
				// form submission in GetBinDays and are not otherwise derivable from the address.
				var address = new Address
				{
					Property = property,
					Postcode = postcode,
					Uid = $"{uprn};{source};{property}",
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
				Url = "https://www.e-lindsey.gov.uk/mywastecollections",
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
			var formFields = FormFieldRegex().Matches(clientSideResponse.Content)!;
			var prefix = formFields.First().Groups["prefix"].Value;
			var fieldValues = formFields.ToDictionary(
				x => x.Groups["field"].Value,
				x => x.Groups["value"].Value
			);

			var pageSessionId = fieldValues["PAGESESSIONID"];
			var sessionId = fieldValues["SESSIONID"];
			var nonce = fieldValues["NONCE"];

			// Uid format: "uprn;source;property"
			var uidParts = address.Uid!.Split(';', 3);
			var uprn = uidParts[0];
			var source = uidParts[1];
			var property = uidParts[2];

			var variablesJson = $$$"""
			{"ADDRESSSOURCE":{"value":"{{{source}}}","scope":"SERVERCLIENTWITHUPDATE"},"ADDRESSUPRN":{"value":"{{{uprn}}}","scope":"SERVERCLIENTWITHUPDATE"}}
			""";
			var variables = Convert.ToBase64String(Encoding.UTF8.GetBytes(variablesJson));

			var formData = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ $"{prefix}_PAGESESSIONID", pageSessionId },
				{ $"{prefix}_SESSIONID", sessionId },
				{ $"{prefix}_NONCE", nonce },
				{ $"{prefix}_VARIABLES", variables },
				{ $"{prefix}_PAGENAME", "LOOKUP" },
				{ $"{prefix}_PAGEINSTANCE", "0" },
				{ $"{prefix}_LOOKUP_ADDRESSLOOKUPPOSTCODE", address.Postcode! },
				{ $"{prefix}_LOOKUP_ADDRESSLOOKUPADDRESS", "0" },
				{ $"{prefix}_LOOKUP_CHOSENADDRESS", property },
				{ $"{prefix}_LOOKUP_TESTDATELAYOUT", "false" },
				{ $"{prefix}_FORMACTION_NEXT", $"{prefix}_LOOKUP_FIELD2" },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"https://www.e-lindsey.gov.uk/apiserver/formsservice/http/processsubmission?pageSessionId={pageSessionId}&fsid={sessionId}&fsn={nonce}",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
				},
				Body = formData,
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
		// Follow the verify cookie redirect
		else if (clientSideResponse.RequestId == 2)
		{
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

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
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Follow the redirect to the page containing the bin days
		else if (clientSideResponse.RequestId == 3)
		{
			var cookies = clientSideResponse.Options.Metadata["cookie"];
			if (clientSideResponse.Headers.TryGetValue("set-cookie", out var setCookieHeader))
			{
				cookies = $"{cookies}; {ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader!)}";
			}

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
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin days from the embedded form data
		else if (clientSideResponse.RequestId == 4)
		{
			var formDataBase64 = FormDataRegex().Match(clientSideResponse.Content).Groups["data"].Value;
			var formDataJson = Encoding.UTF8.GetString(Convert.FromBase64String(formDataBase64));

			using var formDataDoc = JsonDocument.Parse(formDataJson);
			var resultElement = formDataDoc.RootElement.GetProperty("RESULTS_1").GetProperty("FIELD12").GetProperty("result").EnumerateArray().First();

			string[] dateFields = ["wastenextref", "wastenextrec", "wastenextpur"];

			// Iterate through each collection date field, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var dateField in dateFields)
			{
				var dateString = resultElement.GetProperty(dateField).GetString()!;

				dateString = OrdinalSuffixRegex().Replace(dateString, string.Empty);

				var date = DateUtilities.ParseDateExact(dateString, "dddd d MMMM yyyy");

				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = ProcessingUtilities.GetMatchingBins(_binTypes, dateField),
				};

				binDays.Add(binDay);
			}

			// Green waste has no per-address "next collection" field - the property is on a fixed
			// round (day of week + A/B fortnight) that collects on the same dates as every other
			// property on that round, so the hardcoded season calendar is used instead. Properties
			// without a green waste round report "null" for these fields rather than being absent.
			var greenDay = resultElement.GetProperty("greenday").GetString()!;
			var greenWeek = resultElement.GetProperty("greenweek").GetString()!;

			if (greenDay != "null")
			{
				var greenWasteDates = _greenWasteCalendar[$"{greenDay}{greenWeek}"];

				if (greenWasteDates[^1] < DateOnly.FromDateTime(DateTime.Now))
				{
					throw new InvalidOperationException(
						$"Green waste calendar for round '{greenDay}{greenWeek}' ends {greenWasteDates[^1]:d} with no further dates - extend _greenWasteCalendar with next season's calendar."
					);
				}

				foreach (var greenWasteDate in greenWasteDates)
				{
					var binDay = new BinDay
					{
						Date = greenWasteDate,
						Address = address,
						Bins = ProcessingUtilities.GetMatchingBins(_binTypes, "greenfirst"),
					};

					binDays.Add(binDay);
				}
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
