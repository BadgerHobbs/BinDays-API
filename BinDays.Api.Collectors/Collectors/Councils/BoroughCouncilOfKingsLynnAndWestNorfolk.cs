namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Borough Council of Kings Lynn and West Norfolk.
/// </summary>
internal sealed partial class BoroughCouncilOfKingsLynnAndWestNorfolk : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Borough Council of Kings Lynn and West Norfolk";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.west-norfolk.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "kings-lynn-and-west-norfolk";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	/// <remarks>
	/// Food waste is collected every week alongside whichever of the black or green bins is due,
	/// and the council does not list separate dates for it.
	/// </remarks>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Black bin" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Green,
			Keys = [ "Green bin" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Grey,
			Keys = [ "Black bin", "Green bin" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Brown bin" ],
		},
	];

	/// <summary>
	/// The base URL for the council's address lookup scripts.
	/// </summary>
	private const string _lookupUrl = "https://www.west-norfolk.gov.uk/site/custom_scripts/mynearest/set";

	/// <summary>
	/// Regex for the addresses and their session ids from the address results.
	/// </summary>
	[GeneratedRegex(@"send_address2\((?<id>\d+)\);"">(?<address>[^<]+)</a>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for the UPRN cookie set when an address is selected.
	/// </summary>
	[GeneratedRegex(@"bcklwn_uprn=(?<uprn>\d+)")]
	private static partial Regex UprnRegex();

	/// <summary>
	/// Regex for the collection dates and their bin images from the collection dates page.
	/// </summary>
	[GeneratedRegex(@"<h3 class='collectiondate'>(?<date>[^<]+):</h3>(?<bins>.*?)</div>", RegexOptions.Singleline)]
	private static partial Regex BinDaysRegex();

	/// <summary>
	/// Regex for the bin names from the bin image alt text.
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
				Url = $"{_lookupUrl}/results.php",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
				},
				Body = $"address={postcode}",
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Select each address in turn, as the results only give session ids and the UPRN is set as a cookie when an address is selected
		else if (clientSideResponse.RequestId >= 1)
		{
			string addressResults;
			string cookie;
			string uprns;

			if (clientSideResponse.RequestId == 1)
			{
				addressResults = clientSideResponse.Content;
				cookie = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);
				uprns = string.Empty;
			}
			else
			{
				addressResults = clientSideResponse.Options.Metadata["addressResults"];
				cookie = clientSideResponse.Options.Metadata["cookie"];

				var uprn = UprnRegex().Match(clientSideResponse.Headers["set-cookie"]).Groups["uprn"].Value;
				uprns = $"{clientSideResponse.Options.Metadata["uprns"]}{uprn};";
			}

			var rawAddresses = AddressRegex().Matches(addressResults)!;

			// The address selected by request N is at index N - 2, so the next one is at index N - 1
			var nextIndex = clientSideResponse.RequestId - 1;

			if (nextIndex < rawAddresses.Count)
			{
				var clientSideRequest = new ClientSideRequest
				{
					RequestId = clientSideResponse.RequestId + 1,
					Url = $"{_lookupUrl}/index.php?id={rawAddresses[nextIndex].Groups["id"].Value}",
					Method = "GET",
					Headers = new()
					{
						{ "user-agent", Constants.UserAgent },
						{ "cookie", cookie },
					},
					Options = new ClientSideOptions
					{
						FollowRedirects = false,
						Metadata =
						{
							{ "addressResults", addressResults },
							{ "cookie", cookie },
							{ "uprns", uprns },
						},
					},
				};

				var getAddressesResponse = new GetAddressesResponse
				{
					NextClientSideRequest = clientSideRequest,
				};

				return getAddressesResponse;
			}

			var uprnValues = uprns.Split(';');

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			for (var i = 0; i < rawAddresses.Count; i++)
			{
				var address = new Address
				{
					Property = WebUtility.HtmlDecode(rawAddresses[i].Groups["address"].Value).Trim(),
					Postcode = postcode,
					Uid = uprnValues[i],
				};

				addresses.Add(address);
			}

			var getAddressesResponseWithAddresses = new GetAddressesResponse
			{
				Addresses = [.. addresses],
			};

			return getAddressesResponseWithAddresses;
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
				Url = "https://www.west-norfolk.gov.uk/info/20174/bins_and_recycling_collection_dates",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "cookie", $"bcklwn_uprn={address.Uid}" },
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
			var rawBinDays = BinDaysRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var date = DateUtilities.ParseDateExact(rawBinDay.Groups["date"].Value.Trim(), "dddd d MMMM yyyy");

				var rawBins = BinRegex().Matches(rawBinDay.Groups["bins"].Value)!;

				// Iterate through each bin image, and match it to the bin types
				var matchedBins = new List<Bin>();
				foreach (Match rawBin in rawBins)
				{
					matchedBins.AddRange(ProcessingUtilities.GetMatchingBins(_binTypes, rawBin.Groups["bin"].Value));
				}

				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = [.. matchedBins],
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
