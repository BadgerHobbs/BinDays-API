namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for South Norfolk Council.
/// </summary>
internal sealed partial class SouthNorfolkCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "South Norfolk Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.southnorfolkandbroadland.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "south-norfolk";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes = [
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "RefuseBin", ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Green,
			Keys = [ "RecycleBin", ],
		},
	];

	/// <summary>
	/// The base URL for the South Norfolk collection service.
	/// </summary>
	private const string _baseUrl = "https://collections-southnorfolk.azurewebsites.net";

	/// <summary>
	/// Regex for extracting address details from the HTML response.
	/// </summary>
	[GeneratedRegex(@"data-address='(?<address>[^']+?)'\s+data-uprn='(?<uprn>[^']+)'", RegexOptions.Singleline)]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for extracting the encoded calendar content from the SOAP response.
	/// </summary>
	[GeneratedRegex(@"<getRoundCalendarForUPRNResult>(?<calendar>.*?)</getRoundCalendarForUPRNResult>", RegexOptions.Singleline)]
	private static partial Regex CalendarContentRegex();

	/// <summary>
	/// Regex for extracting month calendar tables.
	/// </summary>
	[GeneratedRegex(@"<table[^>]*?>.*?<b>(?<month>[A-Za-z]+) (?<year>\d{4})</b>.*?</table>", RegexOptions.Singleline)]
	private static partial Regex MonthTableRegex();

	/// <summary>
	/// Regex for extracting table rows.
	/// </summary>
	[GeneratedRegex(@"<tr>(?<row>.*?)</tr>", RegexOptions.Singleline)]
	private static partial Regex RowRegex();

	/// <summary>
	/// Regex for extracting table cells.
	/// </summary>
	[GeneratedRegex(@"<td[^>]*>(?<cell>.*?)</td>", RegexOptions.Singleline)]
	private static partial Regex CellRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for loading the calendar page
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}/calendar.aspx",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for fetching addresses
		else if (clientSideResponse.RequestId == 1)
		{
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var requestBody = JsonSerializer.Serialize(new
			{
				functionHashData = $"SearchForProp#CalendarSearch##{postcode}#####",
				fky = "getCal",
				url = $"{_baseUrl}/calendar.aspx",
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseUrl}/GetData.aspx/GetCalInfo",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", "application/json; charset=utf-8" },
					{ "x-requested-with", "XMLHttpRequest" },
					{ "cookie", requestCookies },
				},
				Body = requestBody,
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Process addresses from the JSON response
		else if (clientSideResponse.RequestId == 2)
		{
			using var outerDocument = JsonDocument.Parse(clientSideResponse.Content);
			var encodedContent = outerDocument.RootElement.GetProperty("d").GetString()!;

			using var innerDocument = JsonDocument.Parse(encodedContent);
			var addressesHtml = innerDocument.RootElement.GetProperty("HTML").GetString()!;

			var rawAddresses = AddressRegex().Matches(addressesHtml)!;

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				var uprn = rawAddress.Groups["uprn"].Value;

				if (string.IsNullOrWhiteSpace(uprn))
				{
					continue;
				}

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
		// Prepare client-side request for loading the calendar page
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}/calendar.aspx",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for fetching the calendar data
		else if (clientSideResponse.RequestId == 1)
		{
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var requestBody = $$"""
			<?xml version="1.0" encoding="utf-8"?>
			<soap:Envelope xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
			  <soap:Body>
			    <getRoundCalendarForUPRN xmlns="http://webaspx-collections.azurewebsites.net/">
			      <council>SNO</council>
			      <UPRN>{{address.Uid!}}</UPRN>
			      <from>Chtml</from>
			    </getRoundCalendarForUPRN>
			  </soap:Body>
			</soap:Envelope>
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseUrl}/WSCollExternal.asmx",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", "text/xml; charset=utf-8" },
					{ "cookie", requestCookies },
				},
				Body = requestBody,
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin collection dates from the SOAP response
		else if (clientSideResponse.RequestId == 2)
		{
			var encodedCalendar = CalendarContentRegex().Match(clientSideResponse.Content).Groups["calendar"].Value;
			var decodedCalendar = WebUtility.HtmlDecode(encodedCalendar);

			var rawMonthTables = MonthTableRegex().Matches(decodedCalendar)!;

			// Iterate through each month table, and extract collection dates
			var binDays = new List<BinDay>();
			foreach (Match monthTable in rawMonthTables)
			{
				var monthName = monthTable.Groups["month"].Value;
				var yearValue = monthTable.Groups["year"].Value;

				var firstOfMonth = DateUtilities.ParseDateExact(
					$"1 {monthName} {yearValue}",
					"d MMMM yyyy"
				);

				var daysInMonth = DateTime.DaysInMonth(firstOfMonth.Year, firstOfMonth.Month);

				var offset = firstOfMonth.DayOfWeek switch
				{
					DayOfWeek.Monday => 0,
					DayOfWeek.Tuesday => 1,
					DayOfWeek.Wednesday => 2,
					DayOfWeek.Thursday => 3,
					DayOfWeek.Friday => 4,
					DayOfWeek.Saturday => 5,
					DayOfWeek.Sunday => 6,
					_ => throw new InvalidOperationException("Invalid day of week."),
				};

				var rows = RowRegex().Matches(monthTable.Value)!;

				for (var rowIndex = 2; rowIndex < rows.Count; rowIndex++)
				{
					var weekIndex = rowIndex - 2;
					var cells = CellRegex().Matches(rows[rowIndex].Groups["row"].Value)!;

					for (var cellIndex = 1; cellIndex < cells.Count; cellIndex++)
					{
						var columnIndex = cellIndex - 1;
						var day = weekIndex * 7 + columnIndex - offset + 1;

						if (day < 1 || day > daysInMonth)
						{
							continue;
						}

						var cellContent = cells[cellIndex].Groups["cell"].Value;

						if (!cellContent.Contains("<svg", StringComparison.Ordinal))
						{
							continue;
						}

						var binKey = cellContent.Contains("Rec date", StringComparison.OrdinalIgnoreCase) ? "RecycleBin" : "RefuseBin";
						var date = new DateOnly(firstOfMonth.Year, firstOfMonth.Month, day);
						var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, binKey);

						var binDay = new BinDay
						{
							Date = date,
							Address = address,
							Bins = matchedBinTypes,
						};

						binDays.Add(binDay);
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
