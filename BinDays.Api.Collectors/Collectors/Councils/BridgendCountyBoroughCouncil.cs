namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Bridgend County Borough Council.
/// </summary>
internal sealed partial class BridgendCountyBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Bridgend County Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.bridgend.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "bridgend";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Any,
			Keys = [ "Refuse Sacks" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Cardboard Recycling",
			Colour = BinColour.Orange,
			Keys = [ "Recycling collection" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Plastic, Cans, Aerosols & Foil Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Recycling collection" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Paper Recycling",
			Colour = BinColour.White,
			Keys = [ "Recycling collection" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Glass Recycling",
			Colour = BinColour.Black,
			Keys = [ "Recycling collection" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Brown,
			Keys = [ "Recycling collection" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Garden Waste" ],
			Type = BinType.Sack,
		},
		new()
		{
			Name = "Absorbent Hygiene Products",
			Colour = BinColour.Purple,
			Keys = [ "Absorbent Hygiene Products" ],
			Type = BinType.Bag,
		},
	];

	/// <summary>
	/// The URL of the council's resident service portal property pages.
	/// </summary>
	private const string _propertyUrl = "https://bridgendportal.azurewebsites.net/property/";

	/// <summary>
	/// Regex for the addresses from the property search results.
	/// </summary>
	[GeneratedRegex(@"<a href=""/property/(?<uid>\d+)"">(?<address>[^<]+)</li>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for the address from a property page.
	/// </summary>
	[GeneratedRegex(@"<p><strong>(?<address>[^<]+)</strong></p>")]
	private static partial Regex PropertyAddressRegex();

	/// <summary>
	/// Regex for the services and their last and next collection dates from a property page.
	/// </summary>
	[GeneratedRegex(@"(?s)<td class=""service-name"">\s*<a[^>]*>(?<service>[^<]+)</a>.*?<td class=""last-service"">.*?</span>(?<last>[^<]*)</td>.*?<td class=""next-service"">.*?</span>(?<next>[^<]*)</td>")]
	private static partial Regex BinDaysRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "aj", "true" },
				{ "search_property", postcode },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _propertyUrl,
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
		// Process addresses from response
		else if (clientSideResponse.RequestId == 1)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var result = jsonDoc.RootElement.GetProperty("result").GetString()!;

			// A postcode with a single property returns a redirect to its property page rather than
			// a list, so the address has to be read from that page
			if (jsonDoc.RootElement.GetProperty("status").GetString() == "REDIRECT")
			{
				var uid = result.Replace("/property/", string.Empty);

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 2,
					Url = $"{_propertyUrl}{uid}",
					Method = "GET",
					Options = new ClientSideOptions
					{
						Metadata =
						{
							{ "uid", uid },
						},
					},
				};

				var redirectResponse = new GetAddressesResponse
				{
					NextClientSideRequest = clientSideRequest,
				};

				return redirectResponse;
			}

			var rawAddresses = AddressRegex().Matches(result)!;

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
		// Process the single address from the property page
		else if (clientSideResponse.RequestId == 2)
		{
			var address = new Address
			{
				Property = PropertyAddressRegex().Match(clientSideResponse.Content).Groups["address"].Value.Trim(),
				Postcode = postcode,
				Uid = clientSideResponse.Options.Metadata["uid"],
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				Addresses = [address],
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
				Url = $"{_propertyUrl}{address.Uid}",
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
				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, service);

				// Iterate through the last and next collection dates (e.g. "14/10/2026")
				foreach (var collectionDate in new[] { rawBinDay.Groups["last"].Value, rawBinDay.Groups["next"].Value })
				{
					if (string.IsNullOrWhiteSpace(collectionDate))
					{
						continue;
					}

					var binDay = new BinDay
					{
						Date = DateUtilities.ParseDateExact(collectionDate.Trim(), "dd/MM/yyyy"),
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
