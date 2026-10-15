namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Flintshire County Council.
/// </summary>
internal sealed partial class FlintshireCountyCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Flintshire County Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.flintshire.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "flintshire";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Black Bin" ],
		},
		// The paper, plastics and glass containers are all collected as part of the weekly recycling
		new()
		{
			Name = "Paper & Cardboard Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Recycling" ],
			Type = BinType.Sack,
		},
		new()
		{
			Name = "Plastics, Metals & Cartons Recycling",
			Colour = BinColour.Grey,
			Keys = [ "Recycling" ],
			Type = BinType.Sack,
		},
		new()
		{
			Name = "Glass Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Recycling" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Green,
			Keys = [ "Food Waste" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Brown Bin" ],
		},
		new()
		{
			Name = "Nappies & Absorbent Hygiene Products",
			Colour = BinColour.Orange,
			Keys = [ "AHP" ],
			Type = BinType.Box,
		},
	];

	/// <summary>
	/// Regex for the bin days from the collection rows.
	/// </summary>
	[GeneratedRegex(@"<div class=""col-lg-2 col-md-2 col-sm-2"">\s*(?<date>\d{2}/\d{2}/\d{4})\s*</div>\s*<div class=""col-lg-2 col-md-2 col-sm-2"">[^<]*</div>\s*<div class=""col-lg-3 col-md-3 col-sm-3"">\s*(?<service>[^<]+?)\s*</div>")]
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
				Url = $"https://digital.flintshire.gov.uk/FCC_BinDay/Home/FindAddresses?pcode={postcode}",
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
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var rawAddresses = jsonDoc.RootElement.EnumerateArray().ToList();

			// Most properties are listed in both English and Welsh, so prefer the English address where there is one
			var englishUprns = rawAddresses
				.Where(rawAddress => rawAddress.GetProperty("AddLanguage").GetString() == "ENG")
				.Select(rawAddress => rawAddress.GetProperty("UNIQUEIDENTIFIER").GetString()!)
				.ToHashSet();

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var rawAddress in rawAddresses)
			{
				var uprn = rawAddress.GetProperty("UNIQUEIDENTIFIER").GetString()!;

				if (rawAddress.GetProperty("AddLanguage").GetString() != "ENG" && englishUprns.Contains(uprn))
				{
					continue;
				}

				var address = new Address
				{
					Property = rawAddress.GetProperty("FULLADD").GetString()!.Trim(),
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
				Url = $"https://digital.flintshire.gov.uk/FCC_BinDay/Home/Details2/{address.Uid}",
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
				var service = rawBinDay.Groups["service"].Value;
				var date = DateUtilities.ParseDateExact(rawBinDay.Groups["date"].Value, "dd/MM/yyyy");

				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, service);

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
