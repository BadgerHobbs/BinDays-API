namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Lewes District Council.
/// </summary>
internal sealed partial class LewesDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Lewes District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.lewes-eastbourne.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "lewes";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "rubbish" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Green,
			Keys = [ "recycling" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "garden waste" ],
		},
		// Food waste is always collected with the rubbish, and also with the recycling on fortnightly (CV) rounds
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Grey,
			Keys = [ "rubbish", "CV recycling" ],
			Type = BinType.Caddy,
		},
	];

	/// <summary>
	/// Regex for the addresses from the data.
	/// </summary>
	[GeneratedRegex(@"<tr>\s*<td>(?<prefix>[^<]*)</td>\s*<td>(?<name>[^<]*)</td>\s*<td>(?<number>[^<]*)</td>\s*<td>(?<line1>[^<]*)</td>\s*<td>(?<line2>[^<]*)</td>\s*<td>[^<]*</td>\s*<td><a[^>]*href=""house\.php\?uprn=(?<uid>\d+)""")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for the bin days from the data.
	/// </summary>
	[GeneratedRegex(@"[Yy]our next (?<service>[a-z ]+) collection day is:\s*<strong>(?<date>[^<]+)</strong>")]
	private static partial Regex BinDaysRegex();

	/// <summary>
	/// Regex for the collection round type (CV for fortnightly, WK for weekly) from the calendar download link.
	/// </summary>
	[GeneratedRegex(@"downloads/Lewes/(?<round>CV|WK)\d+\.ics")]
	private static partial Regex RoundRegex();

	/// <summary>
	/// Regex for removing ordinal suffixes from dates.
	/// </summary>
	[GeneratedRegex(@"(?<=\d)(st|nd|rd|th)")]
	private static partial Regex OrdinalSuffixRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://environmentfirst.co.uk/results.php",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
				},
				Body = $"postcode={postcode}",
				// Single-address postcodes redirect to the bin days page, the redirect body still lists the address
				Options = new ClientSideOptions
				{
					FollowRedirects = false,
				},
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
				string[] addressParts =
				[
					rawAddress.Groups["prefix"].Value.Trim(),
					rawAddress.Groups["name"].Value.Trim(),
					rawAddress.Groups["number"].Value.Trim(),
					rawAddress.Groups["line1"].Value.Trim(),
					rawAddress.Groups["line2"].Value.Trim(),
				];

				var address = new Address
				{
					Property = string.Join(", ", addressParts.Where(part => !string.IsNullOrWhiteSpace(part))),
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
		// Prepare client-side request for getting bin days
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://environmentfirst.co.uk/house.php?uprn={address.Uid!}",
				Method = "GET",
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 1)
		{
			var rawBinDays = BinDaysRegex().Matches(clientSideResponse.Content)!;
			var round = RoundRegex().Match(clientSideResponse.Content).Groups["round"].Value;

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var service = rawBinDay.Groups["service"].Value.Trim();
				var dateString = rawBinDay.Groups["date"].Value.Trim();

				var cleanedDate = OrdinalSuffixRegex().Replace(dateString, string.Empty);
				var date = DateUtilities.ParseDateExact(cleanedDate, "dddd d MMMM yyyy");

				// Prefix the service with the round type, as food waste collections differ between rounds
				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, $"{round} {service}");

				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = matchedBinTypes,
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
