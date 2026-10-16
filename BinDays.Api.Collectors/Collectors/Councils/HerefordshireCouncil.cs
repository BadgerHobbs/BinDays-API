namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Herefordshire Council.
/// </summary>
internal sealed partial class HerefordshireCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Herefordshire Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.herefordshire.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "herefordshire";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "General rubbish - black bin" ],
		},
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "General rubbish - black sacks" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Green,
			Keys = [ "Recycling" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden waste" ],
		},
	];

	/// <summary>
	/// The URL of the check bin collection day page.
	/// </summary>
	private const string _pageUrl = "https://www.herefordshire.gov.uk/rubbish-recycling/check-bin-collection-day";

	/// <summary>
	/// Regex for the addresses from the postcode search results.
	/// </summary>
	[GeneratedRegex(@"<a href=""/rubbish-recycling/check-bin-collection-day\?blpu_uprn=(?<uprn>\d+)[^""]*"">(?<address>[^<]+)</a>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for each service heading and its list of collections.
	/// </summary>
	[GeneratedRegex(@"<h3 class=""c-supplement__heading"">(?<service>[^<]+)</h3>\s*<ul class=""c-supplement__list"">(?<collections>.*?)</ul>", RegexOptions.Singleline)]
	private static partial Regex ServiceRegex();

	/// <summary>
	/// Regex for the dates in a service's list of collections, e.g. "Friday 23 October 2026 (next collection)".
	/// </summary>
	[GeneratedRegex(@"<li>\s*(?<date>[A-Za-z]+ \d{1,2} [A-Za-z]+ \d{4}) \(")]
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
				Url = $"{_pageUrl}?postcode={postcode}",
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
					Property = rawAddress.Groups["address"].Value.Trim(),
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
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_pageUrl}?blpu_uprn={address.Uid}",
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
			var rawServices = ServiceRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each service, and create a new bin day object for each of its collections
			var binDays = new List<BinDay>();
			foreach (Match rawService in rawServices)
			{
				var service = rawService.Groups["service"].Value;
				var rawDates = DateRegex().Matches(rawService.Groups["collections"].Value)!;

				// Iterate through each date, and create a new bin day object
				foreach (Match rawDate in rawDates)
				{
					var date = DateUtilities.ParseDateExact(rawDate.Groups["date"].Value, "dddd d MMMM yyyy");

					var binDay = new BinDay
					{
						Date = date,
						Address = address,
						Bins = ProcessingUtilities.GetMatchingBins(_binTypes, service),
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
