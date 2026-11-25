namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Castle Point Borough Council.
/// </summary>
internal sealed partial class CastlePointBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Castle Point Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.castlepoint.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "castle-point";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "Paper & Cardboard Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Blue sacks" ],
			Type = BinType.Sack,
		},
		new()
		{
			Name = "Plastics & Cans Recycling",
			Colour = BinColour.Pink,
			Keys = [ "Pink sacks" ],
			Type = BinType.Sack,
		},
		new()
		{
			Name = "Glass Recycling",
			Colour = BinColour.Green,
			Keys = [ "Glass" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Green,
			Keys = [ "Food waste" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden waste" ],
		},
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Black sacks" ],
			Type = BinType.Sack,
		},
	];

	/// <summary>
	/// The base URL of the council's street lookup.
	/// </summary>
	private const string _baseUrl = "https://apps.castlepoint.gov.uk/cpapps/index.cfm";

	/// <summary>
	/// Regex for each town's list of streets from the street search results.
	/// </summary>
	[GeneratedRegex(@"<h3>(?<town>[^<]+)</h3>(?<streets>[\s\S]*?)</ol>")]
	private static partial Regex TownRegex();

	/// <summary>
	/// Regex for the streets from a town's list of streets.
	/// </summary>
	[GeneratedRegex(@"roadID=(?<uid>\d+)"">\s*(?<street>[^<]+?)\s*</a>")]
	private static partial Regex StreetRegex();

	/// <summary>
	/// Regex for the calendar key, which lists the containers collected for each type of collection.
	/// </summary>
	[GeneratedRegex(@"<(?:span|div) class=""(?<collection>pink|normal|green)""[^>]*>(?<containers>[\s\S]*?)</div>")]
	private static partial Regex KeyRegex();

	/// <summary>
	/// Regex for each month's table from the calendar.
	/// </summary>
	[GeneratedRegex(@"<th colspan=""7"">[\s\S]*?\{ts '(?<yearMonth>\d{4}-\d{2})[\s\S]*?</table>")]
	private static partial Regex MonthRegex();

	/// <summary>
	/// Regex for the collection days from a month's table.
	/// </summary>
	[GeneratedRegex(@"<td class=""(?<collection>pink|normal|green)[^""]*"">(?<day>\d+)</td>")]
	private static partial Regex CollectionDayRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting the location of the postcode
		if (clientSideResponse == null)
		{
			var clientSideRequest = GeocodingUtilities.CreatePostcodesIoRequest(postcode, 1);

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for getting the street name of the location
		else if (clientSideResponse.RequestId == 1)
		{
			var clientSideRequest = GeocodingUtilities.CreateNominatimReverseGeocodeRequest(clientSideResponse.Content, 2);

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for getting streets, as the council only offers a street search
		else if (clientSideResponse.RequestId == 2)
		{
			// The council lists street names without apostrophes (e.g. "ST MARYS ROAD")
			var street = GeocodingUtilities.ParseRoadName(clientSideResponse.Content)
				.Replace("'", string.Empty)
				.Replace("’", string.Empty);

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "searchterm", street },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = $"{_baseUrl}?fa=mystreet.search",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
				},
				Body = requestBody,
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Process streets from response
		else if (clientSideResponse.RequestId == 3)
		{
			var rawTowns = TownRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each town's streets, and create a new address object
			var addresses = new List<Address>();
			foreach (Match rawTown in rawTowns)
			{
				var town = rawTown.Groups["town"].Value.Trim();
				var rawStreets = StreetRegex().Matches(rawTown.Groups["streets"].Value)!;

				foreach (Match rawStreet in rawStreets)
				{
					var address = new Address
					{
						Property = $"{rawStreet.Groups["street"].Value}, {town}",
						Postcode = postcode,
						Uid = rawStreet.Groups["uid"].Value,
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
				Url = $"{_baseUrl}?fa=myStreet.displayDetails&roadID={address.Uid}",
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
			// The containers collected differ by street, so match the bins on each collection's entry in the key
			// (e.g. "Recycling Collection - Blue sacks - paper and cardboard, Pink sacks - cans and plastics")
			var collectionBinTypes = new Dictionary<string, IReadOnlyCollection<Bin>>();
			foreach (Match rawKey in KeyRegex().Matches(clientSideResponse.Content)!)
			{
				var containers = rawKey.Groups["containers"].Value;

				collectionBinTypes[rawKey.Groups["collection"].Value] = ProcessingUtilities.GetMatchingBins(_binTypes, containers);
			}

			var rawMonths = MonthRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each month's collection days, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawMonth in rawMonths)
			{
				var yearMonth = rawMonth.Groups["yearMonth"].Value;
				var rawBinDays = CollectionDayRegex().Matches(rawMonth.Value)!;

				foreach (Match rawBinDay in rawBinDays)
				{
					var date = DateUtilities.ParseDateExact($"{yearMonth}-{rawBinDay.Groups["day"].Value}", "yyyy-MM-d");

					var binDay = new BinDay
					{
						Date = date,
						Address = address,
						Bins = collectionBinTypes[rawBinDay.Groups["collection"].Value],
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
