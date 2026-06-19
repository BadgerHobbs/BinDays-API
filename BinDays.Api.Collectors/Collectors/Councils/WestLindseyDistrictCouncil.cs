namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for West Lindsey District Council.
/// </summary>
internal sealed partial class WestLindseyDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "West Lindsey District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.west-lindsey.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "west-lindsey";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "BLACK" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Blue,
			Keys = [ "BLUE" ],
		},
		new()
		{
			Name = "Paper and Cardboard Recycling",
			Colour = BinColour.Purple,
			Keys = [ "PURPLE" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "GREEN" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Grey,
			Keys = [ "ORANGE" ], // Website uses the "wasterORANGE" CSS class for the grey food caddy
			Type = BinType.Caddy,
		},
	];

	/// <summary>
	/// The base URL for the StatMap cluster service.
	/// </summary>
	private const string _clusterServiceUrl = "https://wlnk.statmap.co.uk/map/Cluster.svc";

	/// <summary>
	/// The script path required by the StatMap endpoints, pre-URL-encoded form of \Cluster\Cluster.AuroraScript$.
	/// </summary>
	private const string _scriptPath = "%5CCluster%5CCluster.AuroraScript%24";

	/// <summary>
	/// Regex for stripping HTML tags from address descriptions.
	/// </summary>
	[GeneratedRegex(@"<[^>]+>")]
	private static partial Regex HtmlTagRegex();

	/// <summary>
	/// Regex for the DR1 bin content block in the update response.
	/// </summary>
	[GeneratedRegex(@"document\.getElementById\(""DR1""\)\.innerHTML=""(?<content>[\s\S]*?)"";")]
	private static partial Regex BinContentRegex();

	/// <summary>
	/// Regex for each bin row and its two reported collection dates.
	/// </summary>
	[GeneratedRegex(@"waster(?<service>[A-Z]+)'>[^<]+</span>\s*bin is [^,]+,\s*(?<firstDate>\d{1,2}/\d{1,2})<br\s*/>\s*and then\s*(?<secondDate>\d{1,2}/\d{1,2})")]
	private static partial Regex BinDayRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_clusterServiceUrl}/findLocation?script={_scriptPath}&address={postcode}",
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
			var rawAddresses = jsonDoc.RootElement.GetProperty("Locations").EnumerateArray();

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var rawAddress in rawAddresses)
			{
				var uid = rawAddress.GetProperty("Id").GetString()!.Trim();
				var rawDescription = rawAddress.GetProperty("Description").GetString()!;
				var property = HtmlTagRegex().Replace(rawDescription, string.Empty).Trim();

				var address = new Address
				{
					Property = property,
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
				Url = $"{_clusterServiceUrl}/getpage?script={_scriptPath}&taskId=bins&format=js&updateOnly=true&query=id={address.Uid!}",
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
			var rawBinContent = BinContentRegex().Match(clientSideResponse.Content).Groups["content"].Value;
			var binContent = rawBinContent
				.Replace(@"\u003c", "<", StringComparison.Ordinal)
				.Replace(@"\u003e", ">", StringComparison.Ordinal)
				.Replace(@"\u0027", "'", StringComparison.Ordinal)
				.Replace(@"\r\n", string.Empty, StringComparison.Ordinal);

			var rawBinDays = BinDayRegex().Matches(binContent)!;

			// Iterate through each bin row, and create new bin day objects
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var service = rawBinDay.Groups["service"].Value.Trim();
				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, service);

				// Iterate through each listed collection date for the bin row
				foreach (var collectionDate in new[]
				{
					rawBinDay.Groups["firstDate"].Value.Trim(),
					rawBinDay.Groups["secondDate"].Value.Trim(),
				})
				{
					var binDay = new BinDay
					{
						Date = DateUtilities.ParseDateInferringYear(collectionDate, "d/MM"),
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
