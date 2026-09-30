namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Wirral Council.
/// </summary>
internal sealed partial class WirralCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Wirral Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.wirral.gov.uk/bins-and-recycling/bin-collection-dates");

	/// <inheritdoc/>
	public override string GovUkId => "wirral";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes = [
		new()
		{
			Name = "Non-recyclable waste",
			Colour = BinColour.Green,
			Keys = [ "Green non-recyclable" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Grey,
			Keys = [ "Grey recycling" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Grey,
			Keys = [ "Grey food waste" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Brown garden waste" ],
		},
	];

	/// <summary>
	/// Regex for the addresses from the options elements.
	/// </summary>
	[GeneratedRegex(@"<option value=""(?<uid>[^""]*)""[^>]*>\s*(?<address>[^<]+)\s*</option>")]
	private static partial Regex AddressesRegex();

	/// <summary>
	/// Regex for the bin days from the data elements.
	/// </summary>
	[GeneratedRegex(@"<li class=""waste-collection__day[^""]*"">[\s\S]*?datetime=""(?<date>[^""]+)""[\s\S]*?waste-collection__day--type"">\s*(?<service>[^<]+)\s*<")]
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
				Url = $"https://www.wirral.gov.uk/bins-and-recycling/bin-collection-dates/find?postcode={postcode}",
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
			var rawAddresses = AddressesRegex().Matches(clientSideResponse.Content)!;

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
				Url = $"https://www.wirral.gov.uk/bins-and-recycling/bin-collection-dates/view/{address.Uid!}",
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

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var service = rawBinDay.Groups["service"].Value.Trim();
				var dateString = rawBinDay.Groups["date"].Value.Trim();

				var date = DateUtilities.ParseDateExact(dateString, "dd-MM-yyyy");

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
