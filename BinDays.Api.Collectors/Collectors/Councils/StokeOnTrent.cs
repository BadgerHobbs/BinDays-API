namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Stoke-on-Trent.
/// </summary>
internal sealed partial class StokeOnTrent : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Stoke-on-Trent";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.stoke.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "stoke-on-trent";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Grey,
			Keys = [ "EMPTY BINS RESIDUAL BIN" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Blue,
			Keys =
			[
				"EMPTY BINS REC 240 STD",
				"EMPTY BINS MIXED REC 55 BOX",
			],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "EMPTY BINS ORG 240 STD" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Grey,
			Keys = [ "EMPTY BINS EXT FOOD WASTE CADDY" ],
			Type = BinType.Caddy,
		},
	];

	/// <summary>
	/// Regex for addresses from the postcode lookup response.
	/// </summary>
	[GeneratedRegex(@"<Address>\s*<UPRN>(?<uprn>[^<]+)</UPRN>.*?<HouseNo>(?<houseNo>[^<]*)</HouseNo>\s*<Building>(?<building>[^<]*)</Building>\s*<Street>(?<street>[^<]*)</Street>\s*<Locality>(?<locality>[^<]*)</Locality>\s*<Postcode>[^<]*</Postcode>\s*</Address>", RegexOptions.Singleline)]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for bin rounds from the XML response.
	/// </summary>
	[GeneratedRegex(@"<BinRound>\s*<Bin>(?<service>[^<]+)</Bin>\s*<RoundName>[^<]*</RoundName>\s*<DateTime>(?<date>[^<]+)</DateTime>\s*</BinRound>", RegexOptions.Singleline)]
	private static partial Regex BinRoundsRegex();

	/// <summary>
	/// Regex for extracting the collection date from the DateTime value.
	/// </summary>
	[GeneratedRegex(@"^(?<date>\d{2}/\d{2}/\d{4})")]
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
				Url = $"https://www.stoke.gov.uk/jadu/custom/webserviceLookUps/BarTecWebServices_llpg_lookup_postcode.php?postcode={postcode}",
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
				var uprn = rawAddress.Groups["uprn"].Value.Trim();

				if (string.IsNullOrWhiteSpace(uprn))
				{
					continue;
				}

				var houseNumber = WebUtility.HtmlDecode(rawAddress.Groups["houseNo"].Value).Trim();
				var building = WebUtility.HtmlDecode(rawAddress.Groups["building"].Value).Trim();
				var street = WebUtility.HtmlDecode(rawAddress.Groups["street"].Value).Trim();
				var locality = WebUtility.HtmlDecode(rawAddress.Groups["locality"].Value).Trim();
				string[] parts = [houseNumber, building, street, locality, postcode];
				var property = string.Join(", ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));

				var address = new Address
				{
					Property = property,
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
		// Prepare client-side request for getting bin days
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://www.stoke.gov.uk/jadu/custom/webserviceLookUps/BarTecWebServices_missed_bin_calendar.php?UPRN={address.Uid!}",
				Method = "POST",
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
			var rawBinRounds = BinRoundsRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each bin round, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinRound in rawBinRounds)
			{
				var service = rawBinRound.Groups["service"].Value.Trim();
				var collectionDate = CollectionDateRegex().Match(rawBinRound.Groups["date"].Value.Trim()).Groups["date"].Value;
				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, service);

				var date = DateUtilities.ParseDateExact(collectionDate, "dd/MM/yyyy");

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
