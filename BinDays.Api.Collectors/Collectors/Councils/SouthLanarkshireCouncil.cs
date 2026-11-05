namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for South Lanarkshire Council.
/// </summary>
internal sealed partial class SouthLanarkshireCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "South Lanarkshire Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.southlanarkshire.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "south-lanarkshire";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "non-recyclable waste" ],
		},
		new()
		{
			Name = "Paper & Card Recycling",
			Colour = BinColour.Blue,
			Keys = [ "paper and card recycling" ],
		},
		new()
		{
			Name = "Glass, Cans & Plastics",
			Colour = BinColour.Grey,
			Keys = [ "glass, cans and plastics" ],
		},
		new()
		{
			Name = "Food & Garden Waste",
			Colour = new("Burgundy", "#B8253F"),
			Keys = [ "food and garden waste" ],
		},
	];

	/// <summary>
	/// The South Lanarkshire bin collection dates form URL.
	/// </summary>
	private const string _formUrl = "https://www.southlanarkshire.gov.uk/xfp/form/831";

	/// <summary>
	/// The fixed form page value required by the council endpoint.
	/// </summary>
	private const string _page = "3956";

	/// <summary>
	/// The form question holding the postcode lookup and the address selection.
	/// </summary>
	private const string _addressQuestion = "qe317470a539308eb2748f7382025636415ab0fcb";

	/// <summary>
	/// The placeholder option value for "I cannot find my property".
	/// </summary>
	private const string _propertyNotFoundUid = "111111";

	/// <summary>
	/// Regex for extracting the CSRF token from the form page.
	/// </summary>
	[GeneratedRegex(@"name=""__token"" value=""(?<token>[^""]+)""")]
	private static partial Regex TokenRegex();

	/// <summary>
	/// Regex for extracting addresses from option elements.
	/// </summary>
	[GeneratedRegex(@"<option\s+value=""(?<uid>\d+)""[^>]*>\s*(?<address>[^<]+?)\s*</option>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for extracting the bin types and dates from the rows of the bin table, skipping bins without a date.
	/// </summary>
	[GeneratedRegex(@"<tr>(?:(?!</tr>).)*?<a [^>]*>(?<service>[^<]+)</a>(?:(?!</tr>).)*?<h4>(?<date>\d{2}/\d{2}/\d{4})</h4>", RegexOptions.Singleline)]
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

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for address lookup
		else if (clientSideResponse.RequestId == 1)
		{
			var token = TokenRegex().Match(clientSideResponse.Content).Groups["token"].Value;

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "__token", token },
				{ "page", _page },
				{ $"{_addressQuestion}_0_0", postcode },
				{ "callback", $$"""{"action":"ic","element":"{{_addressQuestion}}","data":0,"tableRow":-1}""" },
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
				var uid = rawAddress.Groups["uid"].Value;

				// Skip the "I cannot find my property" option
				if (uid == _propertyNotFoundUid)
				{
					continue;
				}

				var address = new Address
				{
					Property = rawAddress.Groups["address"].Value,
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
		// Prepare client-side request for getting the form token
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _formUrl,
				Method = "GET",
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
			var token = TokenRegex().Match(clientSideResponse.Content).Groups["token"].Value;

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "__token", token },
				{ "page", _page },
				{ $"{_addressQuestion}_0_0", address.Postcode! },
				{ $"{_addressQuestion}_1_0", address.Uid! },
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
				var date = DateUtilities.ParseDateExact(rawBinDay.Groups["date"].Value, "dd/MM/yyyy");

				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, rawBinDay.Groups["service"].Value);

				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = matchedBinTypes,
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
