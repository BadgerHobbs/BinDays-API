namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for South Cambridgeshire District Council.
/// </summary>
internal sealed partial class SouthCambridgeshireDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "South Cambridgeshire District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.scambs.gov.uk");

	/// <inheritdoc/>
	public override string GovUkId => "south-cambridgeshire";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Black bin" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Blue bin" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Green bin" ],
		},
	];

	/// <summary>
	/// Regex for parsing addresses from the response.
	/// </summary>
	[GeneratedRegex(@"data-address=""(?<address>[^""]+)""[^>]*data-id=""(?<uprn>[^""]+)""", RegexOptions.Singleline)]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for parsing bin days from the table rows HTML.
	/// </summary>
	[GeneratedRegex(@"<tr[^>]*>(?<row>.*?)</tr>", RegexOptions.Singleline)]
	private static partial Regex BinDayRowRegex();

	/// <summary>
	/// Regex for parsing the date from a table row.
	/// </summary>
	[GeneratedRegex(@"<th[^>]*scope=""row"">(?<date>[^<]+)", RegexOptions.Singleline)]
	private static partial Regex BinDayDateRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting initial cookies
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://www.greatercambridgewaste.org/find-your-bin-collection-day",
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
		// Prepare client-side request for getting addresses
		else if (clientSideResponse.RequestId == 1)
		{
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"https://www.greatercambridgewaste.org/bin-calendar/addresses?postcode={postcode}",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "cookie", requestCookies },
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 2)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var addressesHtml = jsonDoc.RootElement.GetProperty("addresses").GetString()!;

			var rawAddresses = AddressRegex().Matches(addressesHtml)!;

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				var address = new Address
				{
					Property = rawAddress.Groups["address"].Value.Trim(),
					Postcode = postcode,
					Uid = rawAddress.Groups["uprn"].Value,
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
		// Prepare client-side request for getting initial cookies
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://www.greatercambridgewaste.org/find-your-bin-collection-day",
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
		// Prepare client-side request for getting bin days
		else if (clientSideResponse.RequestId == 1)
		{
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);
			var cookies = $"{requestCookies}; bin_calendar_uprn={address.Uid}";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"https://www.greatercambridgewaste.org/bin-calendar/collections?uprn={address.Uid}&numberOfCollections=12",
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
		else if (clientSideResponse.RequestId == 2)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var tableRowsHtml = jsonDoc.RootElement.GetProperty("tableRows").GetString()!;

			var rawBinDays = BinDayRowRegex().Matches(tableRowsHtml)!;

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var rowHtml = rawBinDay.Groups["row"].Value;
				var dateString = BinDayDateRegex().Match(rowHtml).Groups["date"].Value.Trim();

				if (string.IsNullOrWhiteSpace(dateString))
				{
					continue;
				}

				var bins = ProcessingUtilities.GetMatchingBins(_binTypes, rowHtml);

				if (bins.Count == 0)
				{
					continue;
				}

				var date = DateUtilities.ParseDateExact(dateString, "dddd d MMMM yyyy");

				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = bins,
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
