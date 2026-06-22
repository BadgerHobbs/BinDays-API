namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Mid Sussex District Council.
/// </summary>
internal sealed partial class MidSussexDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Mid Sussex District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.midsussex.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "mid-sussex";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Domestic Refuse Waste Collection Service" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Domestic Recycling Waste Collection Service" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Domestic Garden Waste Service" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Green,
			Keys = [ "Domestic Food Waste Service" ],
			Type = BinType.Caddy,
		},
	];

	/// <summary>
	/// The base URL for the Mid Sussex collection service.
	/// </summary>
	private const string _baseUrl = "https://waste.services.midsussex.gov.uk";

	/// <summary>
	/// Regex for extracting the address lookup URL.
	/// </summary>
	[GeneratedRegex(@"<form action=""(?<addressLookupUrl>https://waste\.services\.midsussex\.gov\.uk/mop\.php\?serviceID=A&Track=[^""]+&seq=2)""", RegexOptions.IgnoreCase)]
	private static partial Regex AddressLookupUrlRegex();

	/// <summary>
	/// Regex for extracting addresses from the address list.
	/// </summary>
	[GeneratedRegex(@"href=""mop\.php\?Track=(?<track>[^&""]+)&serviceID=A&seq=3&pIndex=(?<pIndex>\d+)""[^>]*>\s*(?<address>[^<]+)\s*</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for extracting bin collection dates and services.
	/// </summary>
	[GeneratedRegex(@"<p class=""[^""]*fontsize12rem[^""]*"">(?<date>\d{2}/\d{2}/\d{4})</p>\s*</li>\s*<li[^>]*>\s*<p class=""[^""]*fontsize12rem[^""]*"">(?<service>[^<]+)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
	private static partial Regex BinDaysRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting the postcode form
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}/?serviceID=A&seq=1",
				Method = "GET",
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for address lookup
		else if (clientSideResponse.RequestId == 1)
		{
			var addressLookupUrl = AddressLookupUrlRegex().Match(clientSideResponse.Content).Groups["addressLookupUrl"].Value;

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "address_postcode", postcode },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = addressLookupUrl,
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
		else if (clientSideResponse.RequestId == 2)
		{
			var rawAddresses = AddressRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				// Uid format: "{track};{pIndex}"
				var uid = $"{rawAddress.Groups["track"].Value};{rawAddress.Groups["pIndex"].Value}";

				var address = new Address
				{
					Property = rawAddress.Groups["address"].Value.Trim(),
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
		// Uid format: "{track};{pIndex}"
		var parts = address.Uid!.Split(';', 2);
		var track = parts[0];
		var pIndex = parts[1];

		// Prepare client-side request for getting bin collection details
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}/mop.php?Track={track}&serviceID=A&seq=3&pIndex={pIndex}",
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
				var date = DateUtilities.ParseDateExact(rawBinDay.Groups["date"].Value, "dd/MM/yyyy");
				var service = rawBinDay.Groups["service"].Value.Trim();
				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, service);

				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = matchedBins,
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
