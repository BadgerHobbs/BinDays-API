namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Melton Borough Council.
/// </summary>
internal sealed partial class MeltonBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Melton Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.melton.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "melton";

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
			Colour = BinColour.Brown,
			Keys = [ "Recycling" ],
		},
	];

	/// <summary>
	/// Regex for the addresses from the address links.
	/// </summary>
	[GeneratedRegex(@"set-location\?id=(?<uid>\d+)[^""]*""><strong>(?<address>[^<]*)</strong>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for the services and their collection dates from the collections page.
	/// </summary>
	[GeneratedRegex(@"<h2>(?<service>[^<]+)</h2>\s*<p>Next [^<]*collections will be:</p>\s*<p><strong>(?<dates>[^<]+)</strong>")]
	private static partial Regex BinDaysRegex();

	/// <summary>
	/// Regex for the dates within a service's collection dates.
	/// </summary>
	[GeneratedRegex(@"\d{2}/\d{2}/\d{4}")]
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
				Url = $"https://my.melton.gov.uk/collections?location=change&address={postcode}",
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
					Property = WebUtility.HtmlDecode(rawAddress.Groups["address"].Value).Trim(),
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
		// Prepare client-side request for setting the address on a new session
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://my.melton.gov.uk/set-location?id={address.Uid}",
				Method = "GET",
				Options = new ClientSideOptions
				{
					FollowRedirects = false,
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting bin days
		else if (clientSideResponse.RequestId == 1)
		{
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = "https://my.melton.gov.uk/collections",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "cookie", cookies },
				},
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

			// Iterate through each service, and create a new bin day object for each of its dates
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, rawBinDay.Groups["service"].Value.Trim());

				foreach (Match rawDate in DateRegex().Matches(rawBinDay.Groups["dates"].Value)!)
				{
					var binDay = new BinDay
					{
						Date = DateUtilities.ParseDateExact(rawDate.Value, "dd/MM/yyyy"),
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
