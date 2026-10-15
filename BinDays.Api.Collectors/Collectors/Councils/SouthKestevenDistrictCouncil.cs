namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for South Kesteven District Council.
/// </summary>
internal sealed partial class SouthKestevenDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "South Kesteven District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.southkesteven.gov.uk/binday");

	/// <inheritdoc/>
	public override string GovUkId => "south-kesteven";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "240 Litre Refuse" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Grey,
			Keys = [ "240 Litre Recycling" ],
		},
		new()
		{
			Name = "Paper & Card Recycling",
			Colour = BinColour.Purple,
			Keys = [ "240 Litre Paper and Card" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Orange,
			Keys = [ "23lt Food Caddy" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "GREEN" ],
		},
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Pink,
			Keys = [ "Refuse Sack" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Clear,
			Keys = [ "Recycling Sack" ], // Also matches "Pap Recycling Sack", the same clear sack collected on paper and card weeks
			Type = BinType.Bag,
		},
	];

	/// <summary>
	/// Regex for the __RequestVerificationToken value.
	/// </summary>
	[GeneratedRegex(@"<input[^>]*name=""__RequestVerificationToken""[^>]*value=""(?<token>[^""]+)""", RegexOptions.IgnoreCase)]
	private static partial Regex RequestVerificationTokenRegex();

	/// <summary>
	/// Regex for the FormGuid value.
	/// </summary>
	[GeneratedRegex(@"<input[^>]*name=""FormGuid""[^>]*value=""(?<formGuid>[^""]+)""", RegexOptions.IgnoreCase)]
	private static partial Regex FormGuidRegex();

	/// <summary>
	/// Regex for the bin days from the collections table.
	/// </summary>
	[GeneratedRegex(@"<td class='Alloy-table-col'>(?<date>[^<]+)</td>\s*<td class='Alloy-table-col'>(?<service>[^<]+)</td>")]
	private static partial Regex BinDaysRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			Dictionary<string, string> requestFormData = new()
			{
				{ "query", postcode },
			};

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://selfservice.southkesteven.gov.uk/core/addresslookup",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
				},
				Body = ProcessingUtilities.ConvertDictionaryToFormData(requestFormData),
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
			using var document = JsonDocument.Parse(clientSideResponse.Content);

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var rawAddress in document.RootElement.EnumerateObject())
			{
				var address = new Address
				{
					Property = rawAddress.Value.GetString()!.Trim(),
					Postcode = postcode,
					Uid = rawAddress.Name,
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
		// Prepare client-side request for getting form cookies and tokens
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://selfservice.southkesteven.gov.uk/renderform?t=213&k=2074C945A63DDC0D18F1EB74DA230AC3122958B1",
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
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var requestVerificationToken = RequestVerificationTokenRegex().Match(clientSideResponse.Content).Groups["token"].Value;
			var formGuid = FormGuidRegex().Match(clientSideResponse.Content).Groups["formGuid"].Value;

			Dictionary<string, string> requestFormData = new()
			{
				{ "__RequestVerificationToken", requestVerificationToken },
				{ "FormGuid", formGuid },
				{ "ObjectTemplateID", "213" },
				{ "Trigger", "submit" },
				{ "CurrentSectionID", "488" },
				{ "FF5265", address.Uid! },
			};

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = "https://selfservice.southkesteven.gov.uk/renderform/Form",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
					{ "cookie", requestCookies },
				},
				Body = ProcessingUtilities.ConvertDictionaryToFormData(requestFormData),
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
				var service = rawBinDay.Groups["service"].Value.Trim();
				var collectionDate = rawBinDay.Groups["date"].Value.Trim();

				var date = DateUtilities.ParseDateExact(collectionDate, "dddd dd MMMM, yyyy");

				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, service);

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
