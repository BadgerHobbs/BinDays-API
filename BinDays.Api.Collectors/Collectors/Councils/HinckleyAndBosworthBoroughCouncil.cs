namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Hinckley and Bosworth Borough Council.
/// </summary>
internal sealed partial class HinckleyAndBosworthBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Hinckley and Bosworth Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.hinckley-bosworth.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "hinckley-and-bosworth";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "black bin" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Blue,
			Keys = [ "blue lidded bin" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Grey,
			Keys = [ "food waste caddy" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "brown bin" ],
		},
	];

	/// <summary>
	/// The base URL of the council's website.
	/// </summary>
	private const string _baseUrl = "https://www.hinckley-bosworth.gov.uk";

	/// <summary>
	/// Regex for the addresses from the address search results.
	/// </summary>
	[GeneratedRegex(@"<a href=""/set-location\?id=(?<uprn>\d+)[^""]*""><strong>(?<address>[^<]+)</strong></a>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for each collection date and the bin images listed under it.
	/// </summary>
	[GeneratedRegex(@"<h3 class=""collectiondate"">(?<date>[^<]+):</h3>(?<bins>.*?)</div>", RegexOptions.Singleline)]
	private static partial Regex BinDayRegex();

	/// <summary>
	/// Regex for the alt text of each bin image, e.g. "blue lidded bin".
	/// </summary>
	[GeneratedRegex(@"alt=""(?<bin>[^""]+)""")]
	private static partial Regex BinRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}/address-search?fpcode={postcode}",
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
					Uid = rawAddress.Groups["uprn"].Value,
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
			// The page reads the address from the location cookie the site sets when an address is chosen,
			// and only needs the round parameter to be present
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}/alldates?round={address.Uid}",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "cookie", $$"""mylocation={"uprn":"{{address.Uid}}"}""" },
				},
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
			var rawBinDays = BinDayRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each collection date, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				// Dates have no year, but the weekday pins it, e.g. "Friday 16 October"
				var date = DateUtilities.ParseDateInferringYear(rawBinDay.Groups["date"].Value, "dddd d MMMM");

				// Bins are shown as images, e.g. alt="black bin", alt="food waste caddy"
				var bins = string.Join(", ", BinRegex().Matches(rawBinDay.Groups["bins"].Value)!.Select(x => x.Groups["bin"].Value));

				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = ProcessingUtilities.GetMatchingBins(_binTypes, bins),
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
