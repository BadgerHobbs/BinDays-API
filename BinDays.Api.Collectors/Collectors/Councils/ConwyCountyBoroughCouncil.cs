namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Conwy County Borough Council.
/// </summary>
internal sealed partial class ConwyCountyBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Conwy County Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.conwy.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "conwy";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Refuse" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Green,
			Keys = [ "Recycle" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Cardboard Recycling",
			Colour = BinColour.Green,
			Keys = [ "Recycle" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Brown,
			Keys = [ "food waste" ],
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
			Name = "Textiles & Small Electricals",
			Colour = BinColour.Pink,
			Keys = [ "Electrical and textile" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Coffee Pods",
			Colour = BinColour.White,
			Keys = [ "Podback" ],
			Type = BinType.Bag,
		},
	];

	/// <summary>
	/// The base URL of the council's collection day forms.
	/// </summary>
	private const string _baseUrl = "https://www.conwy.gov.uk/Contensis-Forms/erf";

	/// <summary>
	/// Regex for the addresses from the data.
	/// </summary>
	[GeneratedRegex(@"uprn=(?<uid>\d+)"">(?<address>[^<]+)</a>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for the name of the collections page from the address links, as the council renames it each year
	/// (e.g. "collection-result-soap-xmas2026.asp").
	/// </summary>
	[GeneratedRegex(@"href=""(?<page>collection-result[^""?]+)\?")]
	private static partial Regex CollectionsPageRegex();

	/// <summary>
	/// Regex for each collection day, with its date and the list of services collected.
	/// </summary>
	[GeneratedRegex(@"<div id=""content"">\s*(?<date>[^<]+?)\s*</div>(?<services>[\s\S]*?)<div id=""content1"">")]
	private static partial Regex BinDaysRegex();

	/// <summary>
	/// Regex for the services from a collection day's list of services.
	/// </summary>
	[GeneratedRegex(@"<li>(?<service>[^<(]+)")]
	private static partial Regex ServiceRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = CreateAddressesRequest(postcode),
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
		// Prepare client-side request for getting addresses, which link to the current collections page
		if (clientSideResponse == null)
		{
			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = CreateAddressesRequest(address.Postcode!),
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting bin days
		else if (clientSideResponse.RequestId == 1)
		{
			var collectionsPage = CollectionsPageRegex().Match(clientSideResponse.Content).Groups["page"].Value;

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseUrl}/{collectionsPage}?ilangid=1&uprn={address.Uid}",
				Method = "GET",
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 2)
		{
			var rawBinDays = BinDaysRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var date = DateUtilities.ParseDateExact(rawBinDay.Groups["date"].Value, "dddd, dd/MM/yyyy");
				var rawServices = ServiceRegex().Matches(rawBinDay.Groups["services"].Value)!;

				// Iterate through each service collected on the date, and create a new bin day object
				foreach (Match rawService in rawServices)
				{
					var service = rawService.Groups["service"].Value;
					var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, service);

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

	/// <summary>
	/// Creates the client-side request for getting the addresses of a postcode.
	/// </summary>
	private static ClientSideRequest CreateAddressesRequest(string postcode)
	{
		var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
		{
			{ "pcode", postcode },
		});

		var clientSideRequest = new ClientSideRequest
		{
			RequestId = 1,
			Url = $"{_baseUrl}/collection-address-soap.asp?ilangid=1",
			Method = "POST",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
				{ "content-type", Constants.FormUrlEncoded },
			},
			Body = requestBody,
		};

		return clientSideRequest;
	}
}
