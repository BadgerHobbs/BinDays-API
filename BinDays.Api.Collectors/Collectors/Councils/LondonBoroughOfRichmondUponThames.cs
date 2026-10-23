namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for London Borough of Richmond upon Thames.
/// </summary>
internal sealed partial class LondonBoroughOfRichmondUponThames : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "London Borough of Richmond upon Thames";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.richmond.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "richmond-upon-thames";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Any,
			Keys = [ "Rubbish and food" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Green,
			Keys = [ "Rubbish and food" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Glass, Cans, Plastics & Cartons",
			Colour = BinColour.Black,
			Keys = [ "Glass, can, plastic and carton recycling" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Paper & Card Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Paper and card recycling" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Garden waste" ],
		},
	];

	/// <summary>
	/// Regex for the route token of the postcode search form.
	/// </summary>
	[GeneratedRegex(@"(?s)name=""postcode"".*?name=""ufprt"" type=""hidden"" value=""(?<ufprt>[^""]+)""")]
	private static partial Regex UfprtRegex();

	/// <summary>
	/// Regex for the addresses from the property options.
	/// </summary>
	[GeneratedRegex(@"<option value=""(?<uid>\d+)"">(?<address>[^<]+)</option>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for the collections and their next dates, skipping services without a date.
	/// </summary>
	[GeneratedRegex(@"<h4>(?<service>[^<]+)</h4>\s*<ul>\s*<li>\s*(?<date>\w+ \d{1,2} \w+ \d{4})")]
	private static partial Regex BinDaysRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting the search form token
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://www.richmond.gov.uk/myrichmond",
				Method = "GET",
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for getting addresses
		else if (clientSideResponse.RequestId == 1)
		{
			var ufprt = UfprtRegex().Match(clientSideResponse.Content).Groups["ufprt"].Value;

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"https://www.richmond.gov.uk/myrichmond?postcode={postcode}&ufprt={ufprt}",
				Method = "GET",
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 2)
		{
			var rawAddresses = AddressRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				var address = new Address
				{
					Property = rawAddress.Groups["address"].Value.Trim(),
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
				Url = $"https://www.richmond.gov.uk/my_richmond?pid={address.Uid}",
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
				// Parse the date (e.g. "Wednesday 28 October 2026")
				var date = DateUtilities.ParseDateExact(rawBinDay.Groups["date"].Value, "dddd d MMMM yyyy");

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
