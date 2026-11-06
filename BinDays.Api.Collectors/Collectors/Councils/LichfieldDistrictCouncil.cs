namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Lichfield District Council.
/// </summary>
internal sealed partial class LichfieldDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Lichfield District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.lichfielddc.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "lichfield";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	/// <remarks>
	/// Food waste is collected every week alongside whichever of the black or blue bins is due,
	/// and the council does not list separate dates for it.
	/// </remarks>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Black Bin" ],
		},
		new()
		{
			Name = "Glass, Cans & Plastics Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Blue Bin" ],
		},
		new()
		{
			Name = "Paper & Card Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Blue Bag" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Paper & Card Recycling (Purple Bin)",
			Colour = BinColour.Purple,
			Keys = [ "Purple Bin" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Grey,
			Keys = [ "Black Bin", "Blue Bin" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Brown Bin" ],
		},
	];

	/// <summary>
	/// Regex for the bin names and next collection dates from the collection dates page.
	/// </summary>
	[GeneratedRegex(@"</span>(?<service>[^<]+)<span class=""visually-hidden""> collection</span></h3>\s*<div class=""bin-collection-tasks__content"">\s*<p class=""bin-collection-tasks__day"">[^<]*</p>\s*<p class=""bin-collection-tasks__date"">(?<date>[^<]+)</p>")]
	private static partial Regex BinDaysRegex();

	/// <summary>
	/// Regex for the ordinal suffixes of day numbers.
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
				Url = $"https://www.lichfielddc.gov.uk/bartec_ajax/{postcode}",
				Method = "POST",
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

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();

			// The lookup returns "success": false with no addresses when the postcode is not found
			if (jsonDoc.RootElement.GetProperty("success").GetBoolean())
			{
				foreach (var result in jsonDoc.RootElement.GetProperty("addresses").EnumerateArray())
				{
					var address = new Address
					{
						Property = result.GetProperty("label").GetString()!.Trim(),
						Postcode = postcode,
						Uid = result.GetProperty("uprn").GetString()!,
					};

					addresses.Add(address);
				}
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
				Url = $"https://www.lichfielddc.gov.uk/homepage/6/bin-collection-dates?uprn={address.Uid}",
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
				var dateString = OrdinalSuffixRegex().Replace(rawBinDay.Groups["date"].Value.Trim(), string.Empty);

				var date = DateUtilities.ParseDateInferringYear(dateString, "d MMMM");

				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = ProcessingUtilities.GetMatchingBins(_binTypes, service),
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
