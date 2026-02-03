namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Tameside Metropolitan Borough Council.
/// </summary>
internal sealed partial class TamesideMetropolitanBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Tameside Metropolitan Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.tameside.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "tameside";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Keys = [ "green_bin_icon" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Black,
			Keys = [ "black_bin_icon" ],
		},
		new()
		{
			Name = "Paper",
			Colour = BinColour.Blue,
			Keys = [ "blue_bin_icon" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "brown_bin_icon" ],
		},
	];

	/// <summary>
	/// Regex for extracting addresses.
	/// </summary>
	[GeneratedRegex(@"<option\s+value=""(?<uid>[^""]+)"">\s*(?<address>[^<]+)\s*</option>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for extracting year sections.
	/// </summary>
	[GeneratedRegex(@"<fieldset class=""year"">\s*<h3 class=""yearHeader"">(?<year>\d{4})</h3>(?<content>.*?)</fieldset>", RegexOptions.Singleline)]
	private static partial Regex YearRegex();

	/// <summary>
	/// Regex for extracting month rows.
	/// </summary>
	[GeneratedRegex(@"<tr class=""month"">\s*<td class=""month"">(?<month>[^<]+)</td>(?<cells>.*?)</tr>", RegexOptions.Singleline)]
	private static partial Regex MonthRegex();

	/// <summary>
	/// Regex for extracting individual day cells.
	/// </summary>
	[GeneratedRegex(@"<td class=""wrapper day"">(?<cell>.*?)</td>", RegexOptions.Singleline)]
	private static partial Regex DayCellRegex();

	/// <summary>
	/// Regex for extracting the collection day.
	/// </summary>
	[GeneratedRegex(@"<div class=""day"">(?<day>\d+)", RegexOptions.Singleline)]
	private static partial Regex DayRegex();

	/// <summary>
	/// Regex for extracting bin icons.
	/// </summary>
	[GeneratedRegex(@"alt=""(?<bin>[^""]+)""", RegexOptions.Singleline)]
	private static partial Regex BinIconRegex();

	/// <summary>
	/// Creates a client-side request for getting the initial session cookie.
	/// </summary>
	private static ClientSideRequest CreateSessionCookieRequest()
	{
		return new ClientSideRequest
		{
			RequestId = 1,
			Url = "https://public.tameside.gov.uk/forms/bin-dates.asp",
			Method = "GET",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
			},
		};
	}

	/// <summary>
	/// Creates a client-side request for posting the postcode.
	/// </summary>
	private static ClientSideRequest CreatePostcodeRequest(string postcode, string sessionCookie)
	{
		var formattedPostcode = ProcessingUtilities.FormatPostcode(postcode);

		Dictionary<string, string> requestHeaders = new()
		{
			{ "content-type", "application/x-www-form-urlencoded" },
			{ "cookie", $"cookieconsent_dismissed=yes; {sessionCookie}" },
			{ "user-agent", Constants.UserAgent },
		};

		var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
		{
			{ "F01_I02_Postcode", formattedPostcode },
			{ "F01_I03_Street", string.Empty },
			{ "F01_I04_Town", string.Empty },
			{ "Form_1", "Continue" },
			{ "history", ",1," },
		});

		return new ClientSideRequest
		{
			RequestId = 2,
			Url = "https://public.tameside.gov.uk/forms/bin-dates.asp",
			Method = "POST",
			Headers = requestHeaders,
			Body = requestBody,
		};
	}

	/// <summary>
	/// Processes a day cell to extract bin day information.
	/// </summary>
	private BinDay? ProcessDayCell(Match dayMatch, string month, string year, Address address)
	{
		var day = DayRegex().Match(dayMatch.Groups["cell"].Value).Groups["day"].Value;
		if (string.IsNullOrWhiteSpace(day))
		{
			return null;
		}

		var date = DateOnly.ParseExact(
			$"{day} {month} {year}",
			"d MMMM yyyy",
			CultureInfo.InvariantCulture,
			DateTimeStyles.None
		);

		var bins = new List<Bin>();
		foreach (Match binIcon in BinIconRegex().Matches(dayMatch.Groups["cell"].Value)!)
		{
			bins.AddRange(ProcessingUtilities.GetMatchingBins(_binTypes, binIcon.Groups["bin"].Value));
		}

		if (bins.Count == 0)
		{
			return null;
		}

		return new BinDay
		{
			Date = date,
			Address = address,
			Bins = [.. bins],
		};
	}

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting session cookie
		if (clientSideResponse == null)
		{
			var clientSideRequest = CreateSessionCookieRequest();

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for getting addresses
		else if (clientSideResponse.RequestId == 1)
		{
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var sessionCookie = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var clientSideRequest = CreatePostcodeRequest(postcode, sessionCookie);

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 2)
		{
			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (Match rawAddress in AddressRegex().Matches(clientSideResponse.Content)!)
			{
				var uid = rawAddress.Groups["uid"].Value.Trim();
				if (string.IsNullOrWhiteSpace(uid))
				{
					continue;
				}

				var address = new Address
				{
					Property = rawAddress.Groups["address"].Value.Trim(),
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
		// Prepare client-side request for getting session cookie
		if (clientSideResponse == null)
		{
			var clientSideRequest = CreateSessionCookieRequest();

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for confirming postcode
		else if (clientSideResponse.RequestId == 1)
		{
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var sessionCookie = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var clientSideRequest = CreatePostcodeRequest(address.Postcode!, sessionCookie);

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting bin days
		else if (clientSideResponse.RequestId == 2)
		{
			var formattedPostcode = ProcessingUtilities.FormatPostcode(address.Postcode!);
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var sessionCookie = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			Dictionary<string, string> requestHeaders = new()
			{
				{ "content-type", "application/x-www-form-urlencoded" },
				{ "cookie", $"cookieconsent_dismissed=yes; {sessionCookie}" },
				{ "user-agent", Constants.UserAgent },
			};

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "F03_I01_SelectAddress", address.Uid! },
				{ "AdvanceSearch", "Continue" },
				{ "F01_I02_Postcode", formattedPostcode },
				{ "F01_I03_Street", string.Empty },
				{ "F01_I04_Town", string.Empty },
				{ "history", ",1,3," },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = "https://public.tameside.gov.uk/forms/bin-dates.asp",
				Method = "POST",
				Headers = requestHeaders,
				Body = requestBody,
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
			var binDays = new List<BinDay>();

			// Iterate through each year block, and parse bin days
			foreach (Match yearMatch in YearRegex().Matches(clientSideResponse.Content)!)
			{
				var year = yearMatch.Groups["year"].Value;

				// Iterate through each month, and parse the day cells
				foreach (Match monthMatch in MonthRegex().Matches(yearMatch.Groups["content"].Value)!)
				{
					var month = monthMatch.Groups["month"].Value.Trim();
					var cellsContent = monthMatch.Groups["cells"].Value;

					foreach (Match dayMatch in DayCellRegex().Matches(cellsContent)!)
					{
						var binDay = ProcessDayCell(dayMatch, month, year, address);
						if (binDay != null)
						{
							binDays.Add(binDay);
						}
					}
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
