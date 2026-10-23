namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Runnymede Borough Council.
/// </summary>
internal sealed partial class RunnymedeBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Runnymede Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.runnymede.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "runnymede";

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
			Name = "Mixed Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Recycling" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Green,
			Keys = [ "Food caddy" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden waste" ],
		},
	];

	/// <summary>
	/// The URL of the council's bin collection day page.
	/// </summary>
	private const string _binCollectionUrl = "https://www.runnymede.gov.uk/homepage/150/check-your-bin-collection-day";

	/// <summary>
	/// Regex for the addresses from the address options.
	/// </summary>
	[GeneratedRegex(@"<option\s+value=""(?<uid>\d+)""\s*>(?<address>[^<]+)</option>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for the services and their collection dates from the results table.
	/// </summary>
	[GeneratedRegex(@"<td><span class=""icon[^""]*""></span>\s*(?<service>[^<]+?)\s*</td><td>(?<date>[^<]+)</td>")]
	private static partial Regex BinDaysRegex();

	/// <summary>
	/// Regex for runs of whitespace, as the council's addresses contain tabs and line breaks.
	/// </summary>
	[GeneratedRegex(@"\s+")]
	private static partial Regex WhitespaceRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_binCollectionUrl}?postcode={postcode}",
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
					Property = WhitespaceRegex().Replace(rawAddress.Groups["address"].Value, " ").Trim(),
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
				Url = $"{_binCollectionUrl}?address={address.Uid}",
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
				// Parse the date (e.g. "Monday, 26 October 2026")
				var date = DateUtilities.ParseDateExact(rawBinDay.Groups["date"].Value.Trim(), "dddd, d MMMM yyyy");

				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, rawBinDay.Groups["service"].Value);

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
