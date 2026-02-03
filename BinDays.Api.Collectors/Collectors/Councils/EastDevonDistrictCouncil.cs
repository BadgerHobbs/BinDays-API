namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for East Devon District Council.
/// </summary>
internal sealed partial class EastDevonDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "East Devon District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://eastdevon.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "east-devon";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Rubbish" ],
		},
		new()
		{
			Name = "Paper, Glass & Cardboard Recycling",
			Colour = BinColour.Green,
			Keys = [ "Recycling and food waste" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Plastics & Tins Recycling",
			Colour = BinColour.Green,
			Keys = [ "Recycling and food waste" ],
			Type = BinType.Sack,
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Blue,
			Keys = [ "Recycling and food waste" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Green waste" ],
		},
	];

	/// <summary>
	/// Regex for parsing month headers and collection entries from the calendar.
	/// </summary>
	[GeneratedRegex(@"<li class=""eventmonth""[^>]*><h2>(?<month>[A-Za-z]+)(?:\s+\d{4})?</h2></li>|<li><span class=""collectiondate[^""]*"">(?<date>[^<]+)</span>(?<bins>.*?)</li>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
	private static partial Regex CollectionEntryRegex();

	/// <summary>
	/// Regex for extracting bin names from a collection entry.
	/// </summary>
	[GeneratedRegex(@"<span class=""collection-[^""]+"">(?<bin>[^<]+)</span>", RegexOptions.IgnoreCase)]
	private static partial Regex BinNameRegex();

	/// <summary>
	/// Regex for extracting the day number from a date string.
	/// </summary>
	[GeneratedRegex(@"\d+")]
	private static partial Regex DayNumberRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://eastdevon.gov.uk/addressfinder?qtype=bins&term={postcode}",
				Method = "GET",
				Headers = new()
				{
					{"User-Agent", Constants.UserAgent},
					{"X-Requested-With", "XMLHttpRequest"},
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 1)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var addresses = new List<Address>();

			// Iterate through each address, and create a new address object
			foreach (var element in jsonDoc.RootElement.EnumerateArray())
			{
				var property = element.GetProperty("label").GetString()!.Trim();
				var uprn = element.GetProperty("UPRN").GetString()!.Trim();

				if (string.IsNullOrWhiteSpace(uprn))
				{
					continue;
				}

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
				Url = $"https://eastdevon.gov.uk/recycling-and-waste/recycling-waste-information/when-is-my-bin-collected/future-collections-calendar/?UPRN={address.Uid}",
				Method = "GET",
				Headers = new()
				{
					{"User-Agent", Constants.UserAgent},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getBinDaysResponse;
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 1)
		{
			var currentMonth = string.Empty;
			var binDays = new List<BinDay>();

			// Iterate through each calendar entry, and build bin day objects
			foreach (Match collectionEntry in CollectionEntryRegex().Matches(clientSideResponse.Content)!)
			{
				var month = collectionEntry.Groups["month"].Value;
				if (!string.IsNullOrWhiteSpace(month))
				{
					currentMonth = WebUtility.HtmlDecode(month).Trim();
					continue;
				}

				var dateText = WebUtility.HtmlDecode(collectionEntry.Groups["date"].Value).Trim();
				var day = DayNumberRegex().Match(dateText).Value;

				var date = $"{day} {currentMonth}".ParseDateInferringYear("d MMMM");

				var binsHtml = WebUtility.HtmlDecode(collectionEntry.Groups["bins"].Value);
				var bins = new List<Bin>();

				// Iterate through each bin, and map to configured bin types
				foreach (Match binMatch in BinNameRegex().Matches(binsHtml)!)
				{
					var binName = binMatch.Groups["bin"].Value.Trim();
					var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, binName);

					bins.AddRange(matchedBins);
				}

				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = [.. bins],
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
