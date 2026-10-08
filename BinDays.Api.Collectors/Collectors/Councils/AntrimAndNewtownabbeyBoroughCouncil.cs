namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Antrim and Newtownabbey Borough Council.
/// </summary>
internal sealed partial class AntrimAndNewtownabbeyBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Antrim and Newtownabbey Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://antrimandnewtownabbey.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "antrim-newtownabbey";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Black bins" ],
		},
		new()
		{
			Name = "Food & Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Brown bins" ],
		},
		new()
		{
			Name = "Paper & Cardboard Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Wheelie boxes" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Plastics, Cartons, Tins & Foil Recycling",
			Colour = BinColour.Red,
			Keys = [ "Wheelie boxes" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Glass Recycling",
			Colour = BinColour.Green,
			Keys = [ "Wheelie boxes" ],
			Type = BinType.Box,
		},
	];

	/// <summary>
	/// The URL of the council's bin checker page.
	/// </summary>
	private const string _binCheckerUrl = "https://antrimandnewtownabbey.gov.uk/binchecker/";

	/// <summary>
	/// Regex for the viewstate token from the input field.
	/// </summary>
	[GeneratedRegex(@"id=""__VIEWSTATE"" value=""(?<viewState>[^""]*)""")]
	private static partial Regex ViewStateRegex();

	/// <summary>
	/// Regex for the CSRF token from the input field.
	/// </summary>
	[GeneratedRegex(@"id=""__CMSCsrfToken"" value=""(?<csrfToken>[^""]*)""")]
	private static partial Regex CsrfTokenRegex();

	/// <summary>
	/// Regex for the addresses from the option elements.
	/// </summary>
	[GeneratedRegex(@"<option value=""(?<uid>\d+)"">(?<address>[^<]*)</option>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for the collection dates and their bins from the schedule boxes.
	/// </summary>
	[GeneratedRegex(@"(?s)<div class=""feature-box bins"">.*?lblDate"">(?<date>[^<]+)</span>(?<bins>.*?)</div>")]
	private static partial Regex BinDaysRegex();

	/// <summary>
	/// Regex for the bin names within a schedule box.
	/// </summary>
	[GeneratedRegex(@"<strong>(?<bin>[^<]+)</strong>")]
	private static partial Regex BinRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting tokens and cookies
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _binCheckerUrl,
				Method = "GET",
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for getting addresses
		else if (clientSideResponse.RequestId == 1)
		{
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var clientSideRequest = CreateSearchRequest(2, postcode, cookies, clientSideResponse.Content);

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId is 2 or 3)
		{
			var rawAddresses = AddressRegex().Matches(clientSideResponse.Content)!;

			// A few addresses are stored with an unspaced postcode (e.g. "BT414SF"), and the council's
			// search is a plain text match, so retry once without the space when nothing is found
			if (rawAddresses.Count == 0 && clientSideResponse.RequestId == 2)
			{
				var cookies = clientSideResponse.Options.Metadata["cookies"];

				var clientSideRequest = CreateSearchRequest(3, postcode.Replace(" ", ""), cookies, clientSideResponse.Content);

				var retryGetAddressesResponse = new GetAddressesResponse
				{
					NextClientSideRequest = clientSideRequest,
				};

				return retryGetAddressesResponse;
			}

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
		// Prepare client-side request for getting bin days
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_binCheckerUrl}?Size=20&Id={address.Uid}",
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
				// Parse the date (e.g. "Thu 15 Oct, 2026")
				var date = DateUtilities.ParseDateExact(rawBinDay.Groups["date"].Value.Trim(), "ddd d MMM, yyyy");

				// Iterate through each bin collected on the date, and create a new bin day object
				foreach (Match rawBin in BinRegex().Matches(rawBinDay.Groups["bins"].Value)!)
				{
					var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, rawBin.Groups["bin"].Value);

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
	/// Creates the client-side request for searching addresses, using the tokens from the previous page.
	/// </summary>
	private static ClientSideRequest CreateSearchRequest(int requestId, string searchText, string cookies, string content)
	{
		var viewState = ViewStateRegex().Match(content).Groups["viewState"].Value;
		var csrfToken = CsrfTokenRegex().Match(content).Groups["csrfToken"].Value;

		var clientSideRequest = new ClientSideRequest
		{
			RequestId = requestId,
			Url = _binCheckerUrl,
			Method = "POST",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
				{ "content-type", Constants.FormUrlEncoded },
				{ "cookie", cookies },
			},
			Body = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "__CMSCsrfToken", csrfToken },
				{ "__VIEWSTATE", viewState },
				{ "p$lt$ctl07$pageplaceholder$p$lt$ctl02$BinCollectionLookup$txtBinSearch", searchText },
				{ "p$lt$ctl07$pageplaceholder$p$lt$ctl02$BinCollectionLookup$btnBinSearch", "Go" },
			}),
			Options = new ClientSideOptions
			{
				Metadata =
				{
					{ "cookies", cookies },
				},
			},
		};

		return clientSideRequest;
	}
}
