namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Blaby District Council.
/// </summary>
internal sealed partial class BlabyDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Blaby District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.blaby.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "blaby";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Refuse" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Green,
			Keys = [ "Recycling" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Orange,
			Keys = [ "Food" ],
			Type = BinType.Caddy,
		},
	];

	/// <summary>
	/// Regex for the addresses from the address links.
	/// </summary>
	[GeneratedRegex(@"set-location\.php\?ref=(?<uid>\d+)[^""]*""><strong>(?<address>[^<]*)</strong>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for the collection dates and their bins from the all collection dates page.
	/// </summary>
	[GeneratedRegex(@"(?s)<h3 class=""collectiondate"">(?<date>[^<:]+):</h3>(?<bins>.*?)</div>")]
	private static partial Regex BinDaysRegex();

	/// <summary>
	/// Regex for the bin names collected on a date.
	/// </summary>
	[GeneratedRegex(@"<a class=""bintype""[^>]*><img[^>]*>(?<bin>[^<]+)</a>")]
	private static partial Regex BinRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://my.blaby.gov.uk/collections.php?location=change&address={postcode}",
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
			var rawAddresses = AddressRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				var address = new Address
				{
					Property = rawAddress.Groups["address"].Value.Trim(),
					Postcode = postcode,
					Uid = rawAddress.Groups["uid"].Value,
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
		// Prepare client-side request for setting the address on a new session
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://my.blaby.gov.uk/set-location.php?ref={address.Uid}",
				Method = "GET",
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
		// Prepare client-side request for getting bin days
		else if (clientSideResponse.RequestId == 1)
		{
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = "https://my.blaby.gov.uk/collections-all-dates",
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
			var rawBinDays = BinDaysRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			var previousDate = DateOnly.MinValue;
			foreach (Match rawBinDay in rawBinDays)
			{
				// Parse the date (e.g. "12 October")
				var date = DateUtilities.ParseDateInferringYear(rawBinDay.Groups["date"].Value.Trim(), "d MMMM");

				// The dates have no year and are listed in order for around six months ahead, so a date
				// that appears to go backwards belongs to the following year
				if (date < previousDate)
				{
					date = date.AddYears(1);
				}

				previousDate = date;

				// Iterate through each bin collected on the date, and create a new bin day object
				foreach (Match rawBin in BinRegex().Matches(rawBinDay.Groups["bins"].Value)!)
				{
					var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, rawBin.Groups["bin"].Value);

					var binDay = new BinDay
					{
						Date = date,
						Address = address,
						Bins = matchedBinTypes,
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
