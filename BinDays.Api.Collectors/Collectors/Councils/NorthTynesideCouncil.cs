namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for North Tyneside Council.
/// </summary>
internal sealed partial class NorthTynesideCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "North Tyneside Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.northtyneside.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "north-tyneside";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "Household Waste",
			Colour = BinColour.Green,
			Keys = [ "Household" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Grey,
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
			Colour = BinColour.Purple,
			Keys = [ "Food" ],
			Type = BinType.Caddy,
		},
	];

	/// <summary>
	/// Regex for the addresses from the data.
	/// </summary>
	[GeneratedRegex(@"<option value=""(?<uid>[^""]*)"">(?<address>[^<]+)</option>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for the bin days from the data.
	/// </summary>
	[GeneratedRegex(@"<li class=""waste-collection__day[^""]*"">[\s\S]*?datetime=""(?<date>[^""]+)""[\s\S]*?waste-collection__day--type"">\s*(?<service>[^<]+)\s*<[\s\S]*?waste-collection__day--colour")]
	private static partial Regex BinDaysRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		var formattedPostcode = ProcessingUtilities.FormatPostcode(postcode);

		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var encodedPostcode = Uri.EscapeDataString(formattedPostcode);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://www.northtyneside.gov.uk/waste-collection-schedule/find?postcode={encodedPostcode}",
				Method = "GET",
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		else if (clientSideResponse.RequestId == 1)
		{
			// Process addresses from response
			var rawAddresses = AddressRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				var uid = rawAddress.Groups["uid"].Value.Trim();

				if (string.IsNullOrWhiteSpace(uid))
				{
					continue;
				}

				var address = new Address
				{
					Property = rawAddress.Groups["address"].Value.Trim(),
					Postcode = formattedPostcode,
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
				Url = $"https://www.northtyneside.gov.uk/waste-collection-schedule/view/{address.Uid}",
				Method = "GET",
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		else if (clientSideResponse.RequestId == 1)
		{
			// Process bin days from response
			var rawBinDays = BinDaysRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var service = rawBinDay.Groups["service"].Value.Trim();
				var dateString = rawBinDay.Groups["date"].Value.Trim();

				var date = DateUtilities.ParseDateExact(dateString, "yyyy-MM-dd");

				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, service);

				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = matchedBins,
				};

				binDays.Add(binDay);
			}

			var getBinDaysResponse = new GetBinDaysResponse
			{
				BinDays = ProcessingUtilities.ProcessBinDays(binDays),
			};

			return getBinDaysResponse;
		}

		throw new InvalidOperationException("Invalid client-side request.");
	}
}
