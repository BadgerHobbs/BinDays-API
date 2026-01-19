namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Cheshire East Council.
/// </summary>
internal sealed partial class CheshireEastCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Cheshire East Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://online.cheshireeast.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "cheshire-east";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General waste",
			Colour = BinColour.Black,
			Keys = [ "General Waste" ],
			Type = BinType.Bin,
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Grey,
			Keys = [ "Mixed Recycling" ],
			Type = BinType.Bin,
		},
		new()
		{
			Name = "Garden waste",
			Colour = BinColour.Green,
			Keys = [ "Garden Waste" ],
			Type = BinType.Bin,
		},
	];

	/// <summary>
	/// Regex for parsing addresses from the search response.
	/// </summary>
	[GeneratedRegex(@"<a class=""select-csv-address""[^>]*data-uprn=""(?<uprn>[^""]+)""[^>]*>(?<address>[^<]+)</a>", RegexOptions.IgnoreCase)]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for parsing bin day rows from the job list.
	/// </summary>
	[GeneratedRegex(@"BartecSimplifiedJobList_(?<index>\d+)__Name""[^>]*value=""(?<name>[^""]+)"" .*?BartecSimplifiedJobList_\k<index>__ScheduledStart""[^>]*value=""(?<date>[^""]+)""", RegexOptions.Singleline)]
	private static partial Regex BinDayRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting session cookies
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://online.cheshireeast.gov.uk/MyCollectionDay/",
				Method = "GET",
				Headers = new()
				{
					{ "User-Agent", Constants.UserAgent },
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
			var setCookie = clientSideResponse.Headers["set-cookie"];
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookie);
			var requestUrl = $"https://online.cheshireeast.gov.uk/MyCollectionDay/SearchByAjax/Search?postcode={postcode}&propertyname=";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = requestUrl,
				Method = "GET",
				Headers = new()
				{
					{ "User-Agent", Constants.UserAgent },
					{ "x-requested-with", "XMLHttpRequest" },
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
			var rawAddresses = AddressRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				var addressText = rawAddress.Groups["address"].Value.Trim();

				var address = new Address
				{
					Property = addressText,
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
		// Prepare client-side request for getting session cookies
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://online.cheshireeast.gov.uk/MyCollectionDay/",
				Method = "GET",
				Headers = new()
				{
					{ "User-Agent", Constants.UserAgent },
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
			// Build the full address string for the API request
			var onelineAddress = $"{address.Property}, {address.Postcode}";

			var setCookie = clientSideResponse.Headers["set-cookie"];
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookie);
			var requestUrl = $"https://online.cheshireeast.gov.uk/MyCollectionDay/SearchByAjax/GetBartecJobList?uprn={address.Uid}&onelineaddress={onelineAddress}";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = requestUrl,
				Method = "GET",
				Headers = new()
				{
					{ "User-Agent", Constants.UserAgent },
					{ "x-requested-with", "XMLHttpRequest" },
					{ "cookie", requestCookies },
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
			var rawBinDays = BinDayRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var binTypeStr = rawBinDay.Groups["name"].Value.Trim();
				var dateStr = rawBinDay.Groups["date"].Value.Trim();

				// Parse date string (e.g. "13/01/2026 07:00:00")
				var dateTime = DateTime.ParseExact(
					dateStr,
					"dd/MM/yyyy HH:mm:ss",
					CultureInfo.InvariantCulture,
					DateTimeStyles.None
				);

				var binDay = new BinDay
				{
					Date = DateOnly.FromDateTime(dateTime),
					Address = address,
					Bins = ProcessingUtilities.GetMatchingBins(_binTypes, binTypeStr),
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
