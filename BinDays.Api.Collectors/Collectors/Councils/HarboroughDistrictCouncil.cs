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
/// Collector implementation for Harborough District Council.
/// </summary>
internal sealed partial class HarboroughDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Harborough District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.harborough.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "harborough";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes = [
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Non-recyclable waste" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Recycling collection" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Garden waste" ],
		},
	];

	private const string _baseUrl = "http://harborough.fccenvironment.co.uk/";
	private const string _forwardedProtoHeaderValue = "https";

	/// <summary>
	/// Regex for the bin days list items.
	/// </summary>
	[GeneratedRegex(@"<li>\s*(?<service>[^<]+?)\s*<span[^>]*>\s*(?<date>[^<]+)\s*</span>\s*</li>", RegexOptions.IgnoreCase)]
	private static partial Regex BinDaysRegex();

	/// <summary>
	/// Regex for the next scheduled bin collection block.
	/// </summary>
	[GeneratedRegex(@"block-your-next-scheduled-bin-collection-days"".*?(?<content><ul>.*?</ul>)", RegexOptions.Singleline)]
	private static partial Regex BinDaysSectionRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var requestBody = $$"""
			{
			  "Postcode": "{{postcode}}"
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}getAddress",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", "application/json" },
					{ "x-forwarded-proto", _forwardedProtoHeaderValue },
				},
				Body = requestBody,
				Options = new ClientSideOptions
				{
					FollowRedirects = false,
				},
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
			using var jsonDocument = JsonDocument.Parse(clientSideResponse.Content);
			var addressElements = jsonDocument.RootElement.GetProperty("datas").EnumerateArray();

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var addressElement in addressElements)
			{
				var uprn = addressElement.GetProperty("AccountSiteUprn").GetString()!;

				if (string.IsNullOrWhiteSpace(uprn))
				{
					continue;
				}

				var property = addressElement.GetProperty("SiteShortAddress").GetString()!.Trim();
				var addressLabel = addressElement.GetProperty("SiteShortAddressLabel").GetString()!.Trim();

				// Uid format: "{uprn};{addressLabel}"
				var address = new Address
				{
					Property = property,
					Postcode = postcode,
					Uid = $"{uprn};{addressLabel}",
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
			// Uid format: "{uprn};{addressLabel}"
			var addressParts = address.Uid!.Split(';', 2);
			var uprn = addressParts[0];
			var addressLabel = addressParts[1];

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new Dictionary<string, string>
			{
				{ "Uprn", uprn },
				{ "hiddenAddressLabel", addressLabel },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}detail-address",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", "application/x-www-form-urlencoded; charset=UTF-8" },
					{ "x-forwarded-proto", _forwardedProtoHeaderValue },
				},
				Body = requestBody,
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
		// Process bin days from response
		else if (clientSideResponse.RequestId == 1)
		{
			var binDaysContent = BinDaysSectionRegex().Match(clientSideResponse.Content).Groups["content"].Value;
			var rawBinDays = BinDaysRegex().Matches(binDaysContent)!;

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var service = rawBinDay.Groups["service"].Value.Trim();
				var collectionDate = rawBinDay.Groups["date"].Value.Trim();

				var date = DateOnly.ParseExact(
					collectionDate,
					"d MMMM yyyy",
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
