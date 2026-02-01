namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Mansfield.
/// </summary>
internal sealed partial class Mansfield : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Mansfield";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.mansfield.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "mansfield";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes = [
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Keys = [ "General Waste Collection Service" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Recycling Waste Collection Service" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden Waste Collection Service" ],
		},
		new()
		{
			Name = "Glass",
			Colour = BinColour.Black,
			Keys = [ "Glass Waste Collection Service" ],
		},
	];

	/// <summary>
	/// The form URL for the collector.
	/// </summary>
	private const string _formUrl = "https://www.mansfield.gov.uk/xfp/form/1339";

	/// <summary>
	/// Regex for the token from the form.
	/// </summary>
	[GeneratedRegex(@"name=""__token"" value=""(?<token>[^""]+)""")]
	private static partial Regex TokenRegex();

	/// <summary>
	/// Regex for the addresses from the data.
	/// </summary>
	[GeneratedRegex(@"<option\s+value=""(?<uid>\d+)""[^>]*>\s*(?<address>[^<]+?)\s*</option>")]
	private static partial Regex AddressRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting token
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _formUrl,
				Method = "GET",
				Headers = new() {
					{"user-agent", Constants.UserAgent},
				},
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
			var token = TokenRegex().Match(clientSideResponse.Content).Groups["token"].Value;
			clientSideResponse.Headers.TryGetValue("set-cookie", out var setCookieHeader);
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader!);

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{"__token", token},
				{"page", "2615"},
				{"locale", "en_GB"},
				{"injectedParams", "{\"formID\":\"1339\"}"},
				{"q3fc8e993e4e89b244317c1f13b6d65c0b0ef1ad2_0_0", postcode},
				{"callback", "{\"action\":\"ic\",\"element\":\"q3fc8e993e4e89b244317c1f13b6d65c0b0ef1ad2\",\"data\":0,\"tableRow\":-1}"},
				{"q177fee160e3d7694451f7d047342e9c0e3ce01c9", string.Empty},
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = _formUrl,
				Method = "POST",
				Headers = new() {
					{"user-agent", Constants.UserAgent},
					{"content-type", "application/x-www-form-urlencoded"},
					{"cookie", requestCookies},
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

			var getAddressesResponse = new GetAddressesResponse
			{
				Addresses = [.. addresses],
			};

			return getAddressesResponse;
		}

		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <inheritdoc/>
	public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting bin days
		if (clientSideResponse == null)
		{
			var fromDate = DateOnly.FromDateTime(DateTime.Now);
			var toDate = fromDate.AddDays(364);

			var requestUrl = $"https://portal.mansfield.gov.uk/mdcwhitespacewebservice/WhiteSpaceWS.asmx/GetCollectionByUPRNAndDatePlus?&apiKey=mDc-wN3-B0f-f4P&UPRN={address.Uid}&ColFromDate={fromDate:yyyy-MM-dd}&ColToDate={toDate:yyyy-MM-dd}";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = requestUrl,
				Method = "GET",
				Headers = new() {
					{"user-agent", Constants.UserAgent},
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
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var collections = jsonDoc.RootElement.GetProperty("Collections");

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var collection in collections.EnumerateArray())
			{
				var service = collection.GetProperty("Service").GetString()!;
				var dateString = collection.GetProperty("Date").GetString()!;

				var collectionDate = DateTime.ParseExact(
					dateString,
					"dd/MM/yyyy HH:mm:ss",
					CultureInfo.InvariantCulture,
					DateTimeStyles.None
				);

				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, service);

				var binDay = new BinDay
				{
					Date = DateOnly.FromDateTime(collectionDate),
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

		throw new InvalidOperationException("Invalid client-side request.");
	}
}
