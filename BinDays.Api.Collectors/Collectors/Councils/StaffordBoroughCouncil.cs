namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Stafford Borough Council.
/// </summary>
internal sealed partial class StaffordBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Stafford Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.staffordbc.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "stafford";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Keys = [ "refuse" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Blue,
			Keys = [ "recycling" ],
		},
		// Garden waste has no dated entry of its own; the site only states it is collected
		// alongside recycling for subscribed properties, so it shares the recycling key.
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "recycling" ],
		},
		// Food waste has no dated entry of its own; the site only states it is collected
		// weekly alongside both refuse and recycling, so it shares both keys.
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Grey,
			Keys = [ "refuse", "recycling" ],
			Type = BinType.Caddy,
		},
	];

	/// <summary>
	/// Regex for the addresses from the html.
	/// </summary>
	[GeneratedRegex(@"<a href=""/address/(?<Uid>\d+)"">(?<Address>[^<]+)</a>")]
	private static partial Regex AddressesRegex();

	/// <summary>
	/// Regex for the next collection dates from the html.
	/// </summary>
	[GeneratedRegex(@"<td>Next (?<Type>refuse|recycling)[^<]*collection date</td>\s*<td>\s*[A-Za-z]{3}\s+(?<Day>\d{1,2})\s+(?<Month>[A-Za-z]{3})\s+(?<Year>\d{4})\s*</td>")]
	private static partial Regex BinDaysRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://www.staffordbc.gov.uk/about-my-area?field_add_data_postcode_value={postcode}",
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
			var rawAddresses = AddressesRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				var address = new Address
				{
					Property = rawAddress.Groups["Address"].Value.Trim(),
					Postcode = postcode,
					Uid = rawAddress.Groups["Uid"].Value,
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
				Url = $"https://www.staffordbc.gov.uk/address/{address.Uid}",
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

			// Iterate through each next collection date, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var type = rawBinDay.Groups["Type"].Value;
				var day = rawBinDay.Groups["Day"].Value;
				var month = rawBinDay.Groups["Month"].Value;
				var year = rawBinDay.Groups["Year"].Value;

				var date = DateUtilities.ParseDateExact($"{day} {month} {year}", "d MMM yyyy");

				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, type);

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
