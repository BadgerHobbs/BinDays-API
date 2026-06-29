namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Braintree District Council.
/// </summary>
internal sealed partial class BraintreeDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Braintree District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.braintree.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "braintree";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Grey,
			Keys =
			[
				"General Waste",
				"Grey Bin",
				"Black Bin",
			],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Blue,
			Keys =
			[
				"Mixed Recycling",
				"Clear Sack",
			],
		},
		new()
		{
			Name = "Paper and Card",
			Colour = BinColour.Red,
			Keys =
			[
				"Paper and Card",
				"Blue Bin",
			],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Green,
			Keys =
			[
				"Food Recycling",
				"Food Bin",
				"Food Caddy",
			],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Garden Bin" ],
		},
		new()
		{
			Name = "Absorbent Hygiene Products",
			Colour = BinColour.White,
			Keys = [ "AHP" ],
			Type = BinType.Sack,
		},
	];

	/// <summary>
	/// The Braintree bin collection form URL.
	/// </summary>
	private const string _formUrl = "https://www.braintree.gov.uk/xfp/form/554";

	/// <summary>
	/// The fixed form page value required by the council endpoint.
	/// </summary>
	private const string _page = "5730";

	/// <summary>
	/// The postcode form field name for the property lookup form.
	/// </summary>
	private const string _postcodeFieldName = "qe15dda0155d237d1ea161004d1839e3369ed4831_0_0";

	/// <summary>
	/// Regex for extracting the CSRF token from the form page.
	/// </summary>
	[GeneratedRegex(@"name=""__token"" value=""(?<token>[^""]+)""")]
	private static partial Regex TokenRegex();

	/// <summary>
	/// Regex for extracting addresses from option elements.
	/// </summary>
	[GeneratedRegex(@"<option\s+value=""(?<uid>[^""]*)""[^>]*>\s*(?<address>[^<]+)\s*</option>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for extracting bin services and dates from the date display blocks.
	/// </summary>
	[GeneratedRegex(@"<div class=""date_display""[^>]*>\s*(?:<img[^>]*>\s*)?<h3[^>]*>\s*(?<service>.*?)\s*</h3>\s*<p>\s*(?<date>\d{2}/\d{2}/\d{4})\s*</p>", RegexOptions.Singleline)]
	private static partial Regex BinDaysRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting the form token
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _formUrl,
				Method = "GET",
			};

			return new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};
		}
		// Prepare client-side request for address lookup
		else if (clientSideResponse.RequestId == 1)
		{
			var token = TokenRegex().Match(clientSideResponse.Content).Groups["token"].Value;

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "__token", token },
				{ "page", _page },
				{ "locale", "en_GB" },
				{ _postcodeFieldName, postcode },
				{ "callback", "{\"action\":\"ic\",\"element\":\"qe15dda0155d237d1ea161004d1839e3369ed4831\",\"data\":0,\"tableRow\":-1}" },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = _formUrl,
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
				},
				Body = requestBody,
			};

			return new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 2)
		{
			var rawAddresses = AddressRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				var uid = rawAddress.Groups["uid"].Value;

				if (string.IsNullOrWhiteSpace(uid) || uid == "0")
				{
					continue;
				}

				var address = new Address
				{
					Property = rawAddress.Groups["address"].Value.Trim(),
					Postcode = postcode,
					Uid = uid,
				};

				addresses.Add(address);
			}

			return new GetAddressesResponse
			{
				Addresses = [.. addresses],
			};
		}

		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <inheritdoc/>
	public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting the form token
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _formUrl,
				Method = "GET",
			};

			return new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};
		}
		// Prepare client-side request for getting bin days
		else if (clientSideResponse.RequestId == 1)
		{
			var token = TokenRegex().Match(clientSideResponse.Content).Groups["token"].Value;

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "__token", token },
				{ "page", _page },
				{ "locale", "en_GB" },
				{ _postcodeFieldName, address.Postcode! },
				{ "qe15dda0155d237d1ea161004d1839e3369ed4831_1_0", address.Uid! },
				{ "next", "Next" },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = _formUrl,
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
				},
				Body = requestBody,
			};

			return new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 2)
		{
			var rawBinDays = BinDaysRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var service = rawBinDay.Groups["service"].Value.Trim();
				var collectionDate = rawBinDay.Groups["date"].Value.Trim();

				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, service);

				var binDay = new BinDay
				{
					Date = DateUtilities.ParseDateExact(collectionDate, "dd/MM/yyyy"),
					Address = address,
					Bins = matchedBinTypes,
				};

				binDays.Add(binDay);
			}

			return new GetBinDaysResponse
			{
				BinDays = ProcessingUtilities.ProcessBinDays(binDays),
			};
		}

		throw new InvalidOperationException("Invalid client-side request.");
	}
}
