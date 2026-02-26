namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Waverley Borough Council.
/// </summary>
internal sealed partial class WaverleyBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Waverley Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.waverley.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "waverley";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Domestic Waste Collection Service" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Recycling Collection Service" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden Waste Collection Service" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Green,
			Keys = [ "Food Waste Collection Service" ],
			Type = BinType.Caddy,
		},
	];

	/// <summary>
	/// The base URL for the Waverley collection service.
	/// </summary>
	private const string _baseUrl = "https://wav-wrp.whitespacews.com";

	/// <summary>
	/// Regex for extracting the track identifier.
	/// </summary>
	[GeneratedRegex(@"https://wav-wrp\.whitespacews\.com\?Track=(?<track>[^&""]+)&serviceID=A&seq=1")]
	private static partial Regex TrackRegex();

	/// <summary>
	/// Regex for extracting addresses and indices.
	/// </summary>
	[GeneratedRegex(@"href=""mop\.php\?Track=[^""]*&serviceID=A&seq=3&pIndex=(?<pIndex>\d+)""[^>]*>\s*(?<address>[^<]+)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for extracting bin collection dates and services.
	/// </summary>
	[GeneratedRegex(@"<p class=""colordarkblue fontfamilyArial fontsize12rem "">(?<date>\d{2}/\d{2}/\d{4})</p>\s*</li>\s*<li[^>]*>\s*<p class=""colordarkblue fontfamilyArial fontsize12rem "">(?<service>[^<]+)</p>", RegexOptions.Singleline)]
	private static partial Regex BinDaysRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting the landing page
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}/",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
				},
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
			var track = TrackRegex().Match(clientSideResponse.Content).Groups["track"].Value;

			var requestBody = $"address_name_number=&address_street=&street_town=&address_postcode={postcode}";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseUrl}/mop.php?serviceID=A&Track={track}&seq=2",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", "application/x-www-form-urlencoded" },
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
				var pIndex = rawAddress.Groups["pIndex"].Value;
				var addressText = rawAddress.Groups["address"].Value.Trim();

				var address = new Address
				{
					Property = addressText,
					Postcode = postcode,
					Uid = pIndex,
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
		var pIndex = address.Uid!;

		// Prepare client-side request for getting the landing page
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}/",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for address lookup
		else if (clientSideResponse.RequestId == 1)
		{
			var track = TrackRegex().Match(clientSideResponse.Content).Groups["track"].Value;

			var requestBody = $"address_name_number=&address_street=&street_town=&address_postcode={address.Postcode!}";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseUrl}/mop.php?serviceID=A&Track={track}&seq=2",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", "application/x-www-form-urlencoded" },
				},
				Body = requestBody,
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "track", track },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for bin collection details
		else if (clientSideResponse.RequestId == 2)
		{
			var track = clientSideResponse.Options.Metadata["track"];

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = $"{_baseUrl}/mop.php?Track={track}&serviceID=A&seq=3&pIndex={pIndex}",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 3)
		{
			var rawBinDays = BinDaysRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var dateText = rawBinDay.Groups["date"].Value;
				var service = rawBinDay.Groups["service"].Value.Trim();

				var date = DateOnly.ParseExact(
					dateText,
					"dd/MM/yyyy",
					CultureInfo.InvariantCulture,
					DateTimeStyles.None
				);

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
