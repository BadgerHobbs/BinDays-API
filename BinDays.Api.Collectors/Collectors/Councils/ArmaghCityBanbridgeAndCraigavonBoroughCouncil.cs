namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Armagh City, Banbridge and Craigavon Borough Council.
/// </summary>
internal sealed partial class ArmaghCityBanbridgeAndCraigavonBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Armagh City, Banbridge and Craigavon Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.armaghbanbridgecraigavon.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "armagh-banbridge-craigavon";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Domestic Collections" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Green,
			Keys = [ "Recycling Collections" ],
		},
		new()
		{
			Name = "Garden & Food Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden & Food Collections" ],
		},
	];

	/// <summary>
	/// Regex for the addresses from the option elements.
	/// </summary>
	[GeneratedRegex(@"<option value=""(?<uid>\d{6,})"">\s*(?<address>[^<]*?)\s*</option>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for the collection sections, each with a heading followed by its dates.
	/// </summary>
	[GeneratedRegex(@"(?s)<h2><i class=""fa fa-[\w-]+"" aria-hidden=""true""></i>\s*(?<service>[^<]+?)\s*</h2>(?<dates>.*?)<img")]
	private static partial Regex BinDaysRegex();

	/// <summary>
	/// Regex for the collection dates within a section.
	/// </summary>
	[GeneratedRegex(@"fa-calendar"" aria-hidden=""true""></i>\s*\w+ (?<date>\d{2}/\d{2}/\d{4})")]
	private static partial Regex DateRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://www.armaghbanbridgecraigavon.gov.uk/resident/binday-address/?postcode={postcode}",
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
					Property = rawAddress.Groups["address"].Value,
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
				Url = $"https://www.armaghbanbridgecraigavon.gov.uk/resident/binday-result/?address={address.Uid}",
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

			// Iterate through each collection section, and create a new bin day object for each of its dates
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var service = rawBinDay.Groups["service"].Value;

				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, service);

				// Iterate through each date in the section, and create a new bin day object
				foreach (Match rawDate in DateRegex().Matches(rawBinDay.Groups["dates"].Value)!)
				{
					// Parse the date (e.g. "20/10/2026")
					var date = DateUtilities.ParseDateExact(rawDate.Groups["date"].Value, "dd/MM/yyyy");

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
