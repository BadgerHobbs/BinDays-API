namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Stockton-on-Tees Borough Council.
/// </summary>
internal sealed partial class StocktonOnTeesBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Stockton-on-Tees Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.stockton.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "stockton-on-tees";

	/// <summary>
	/// The list of bin types for this collector. The weekly recycling containers and the food
	/// waste caddy are all collected on the recycling day, which the council reports as one service.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Keys = [ "waste" ],
		},
		new()
		{
			Name = "Plastics, Tins & Cartons Recycling",
			Colour = BinColour.Blue,
			Keys = [ "recycling" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Cardboard & Paper Recycling",
			Colour = BinColour.White,
			Keys = [ "recycling" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Glass & Batteries Recycling",
			Colour = BinColour.Blue,
			Keys = [ "recycling" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Grey,
			Keys = [ "recycling" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "garden" ],
		},
	];

	/// <summary>
	/// The prefix used by every field of the council's bin collection form.
	/// </summary>
	private const string _formPrefix = "LOOKUPBINDATESBYADDRESSSKIPOUTOFREGIONV2";

	/// <summary>
	/// Regex to parse JSONP responses.
	/// </summary>
	[GeneratedRegex(@"^[^(]+\((?<json>.*)\)$", RegexOptions.Singleline)]
	private static partial Regex JsonpRegex();

	/// <summary>
	/// Regex to capture the form session fields required to submit the selected address.
	/// </summary>
	[GeneratedRegex(@"name=""LOOKUPBINDATESBYADDRESSSKIPOUTOFREGIONV2_(?<name>PAGESESSIONID|SESSIONID|NONCE)"" value=""(?<value>[^""]+)""")]
	private static partial Regex SessionFieldRegex();

	/// <summary>
	/// Regex for the bin days from the data. The service is taken from the block's class modifier
	/// rather than its title, as the "Waste" title is a substring of the "Garden Waste" title.
	/// </summary>
	[GeneratedRegex(@"myaccount-block__title--(?<service>[a-z]+)"">[^<]*</p>\s*<p>Next collection:</p>\s*<p class=""myaccount-block__date[^""]*"">\s*(?<date>[^<]+?)\s*</p>")]
	private static partial Regex BinDaysRegex();

	/// <summary>
	/// Regex for the garden waste bin day from the data, which the council renders as prose.
	/// </summary>
	[GeneratedRegex(@"myaccount-block__title--(?<service>[a-z]+)"">[^<]*</p>\s*<p>[^<]*next collection is:\s*<strong>\s*(?<date>[^<]+?)\s*</strong>")]
	private static partial Regex GardenBinDaysRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for the postcode search
		if (clientSideResponse == null)
		{
			var jsonPayload = $$$"""
			{"id":1,"method":"postcodeSearch","params":{"postcode":"{{{postcode}}}"}}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://www.stockton.gov.uk/apiserver/postcode?callback=cb&jsonrpc={Uri.EscapeDataString(jsonPayload)}",
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
			var json = JsonpRegex().Match(clientSideResponse.Content).Groups["json"].Value;

			using var jsonDoc = JsonDocument.Parse(json);

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var addressElement in jsonDoc.RootElement.GetProperty("result").EnumerateArray())
			{
				var udprn = addressElement.GetProperty("udprn").GetString()!;
				var custodian = addressElement.GetProperty("custodian").GetInt32();

				string[] addressParts =
				[
					addressElement.GetProperty("line1").GetString()!,
					addressElement.GetProperty("line2").GetString()!,
					addressElement.GetProperty("line3").GetString()!,
					addressElement.GetProperty("town").GetString()!,
					addressElement.GetProperty("county").GetString()!,
				];

				// Uid format: "udprn;custodian" - both are needed to submit the form in GetBinDays
				var address = new Address
				{
					Property = string.Join(", ", addressParts.Where(p => !string.IsNullOrWhiteSpace(p))),
					Postcode = postcode,
					Uid = $"{udprn};{custodian}",
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
		// Prepare client-side request for the initial page load
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://www.stockton.gov.uk/article/1390/Bin-collection-days-and-garden-waste",
				Method = "GET",
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare form submission with the selected address
		else if (clientSideResponse.RequestId == 1)
		{
			var sessionFields = SessionFieldRegex().Matches(clientSideResponse.Content)!;
			var sessionFieldValues = sessionFields.ToDictionary(
				x => x.Groups["name"].Value,
				x => x.Groups["value"].Value
			);

			var pageSessionId = sessionFieldValues["PAGESESSIONID"];
			var sessionId = sessionFieldValues["SESSIONID"];
			var nonce = sessionFieldValues["NONCE"];

			// Uid format: "udprn;custodian"
			var uidParts = address.Uid!.Split(';', 2);

			var formData = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ $"{_formPrefix}_PAGESESSIONID", pageSessionId },
				{ $"{_formPrefix}_SESSIONID", sessionId },
				{ $"{_formPrefix}_NONCE", nonce },
				{ $"{_formPrefix}_UPRN", uidParts[0] },
				{ $"{_formPrefix}_CUSTODIAN", uidParts[1] },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"https://www.stockton.gov.uk/apiserver/formsservice/http/processsubmission?pageSessionId={pageSessionId}&fsid={sessionId}&fsn={nonce}",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
				},
				Body = formData,
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
		// Follow the verify cookie redirect
		else if (clientSideResponse.RequestId == 2)
		{
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = clientSideResponse.Headers["location"],
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "cookie", cookies },
				},
				Options = new ClientSideOptions
				{
					FollowRedirects = false,
					Metadata =
					{
						{ "cookie", cookies },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Follow the redirect to the page containing the bin days
		else if (clientSideResponse.RequestId == 3)
		{
			var cookies = clientSideResponse.Options.Metadata["cookie"];

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 4,
				Url = clientSideResponse.Headers["location"],
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
		else if (clientSideResponse.RequestId == 4)
		{
			var rawBinDays = BinDaysRegex().Matches(clientSideResponse.Content)!
				.Concat(GardenBinDaysRegex().Matches(clientSideResponse.Content)!);

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var rawBinDay in rawBinDays)
			{
				var service = rawBinDay.Groups["service"].Value;
				var collectionDate = rawBinDay.Groups["date"].Value;

				var binDay = new BinDay
				{
					Date = DateUtilities.ParseDateExact(collectionDate, "ddd d MMMM yyyy"),
					Address = address,
					Bins = ProcessingUtilities.GetMatchingBins(_binTypes, service),
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
