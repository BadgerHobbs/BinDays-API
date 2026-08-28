namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for West Dunbartonshire Council.
/// </summary>
internal sealed partial class WestDunbartonshireCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "West Dunbartonshire Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.west-dunbarton.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "west-dunbartonshire";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Non-Recyclable" ],
		},
		new()
		{
			Name = "Mixed Recycling (Paper, Cardboard, Cartons, Bottles and Cans)",
			Colour = BinColour.Blue,
			Keys = [ "Blue" ],
		},
		new()
		{
			Name = "Food and Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Brown Bin" ],
		},
	];

	/// <summary>
	/// The bin collection day URL.
	/// </summary>
	private const string _binCollectionDayUrl = "https://www.west-dunbarton.gov.uk/recycling-and-waste/bin-collection-day";

	/// <summary>
	/// Regex for the addresses from the response data.
	/// </summary>
	[GeneratedRegex(@"<option value=""(?<uid>\d+)""[^>]*>\s*(?<address>[^<]+?)\s*</option>", RegexOptions.IgnoreCase)]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for each collection round from the response data.
	/// </summary>
	[GeneratedRegex(@"<div class=""[^""]*round-info[^""]*""[\s\S]*?<div class=""[^""]*round-name[^""]*"">\s*(?<service>[^<]+)\s*</div>(?<content>[\s\S]*?)</div>\s*</div>", RegexOptions.IgnoreCase)]
	private static partial Regex RoundInfoRegex();

	/// <summary>
	/// Regex for the collection dates from a collection round.
	/// </summary>
	[GeneratedRegex(@"<span class=""[^""]*date-string[^""]*"">\s*(?<date>[^<]+)\s*</span>", RegexOptions.IgnoreCase)]
	private static partial Regex CollectionDateRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_binCollectionDayUrl}?postcode={postcode}",
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
				var uid = rawAddress.Groups["uid"].Value;

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
		// Prepare client-side request for getting bin days
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_binCollectionDayUrl}?postcode={address.Postcode!}&uprn={address.Uid!}",
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
			var rawRoundInfos = RoundInfoRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each collection round, and create new bin day objects
			var binDays = new List<BinDay>();
			foreach (Match rawRoundInfo in rawRoundInfos)
			{
				var service = rawRoundInfo.Groups["service"].Value.Trim();
				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, service);

				if (matchedBins.Count == 0)
				{
					continue;
				}

				var rawCollectionDates = CollectionDateRegex().Matches(rawRoundInfo.Groups["content"].Value)!;

				// Iterate through each collection date, and create a new bin day object
				foreach (Match rawCollectionDate in rawCollectionDates)
				{
					var collectionDate = rawCollectionDate.Groups["date"].Value.Trim();

					var binDay = new BinDay
					{
						Date = DateUtilities.ParseDateExact(collectionDate, "d MMMM yyyy"),
						Address = address,
						Bins = matchedBins,
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
