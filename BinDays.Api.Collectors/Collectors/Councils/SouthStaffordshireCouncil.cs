namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for South Staffordshire Council.
/// </summary>
internal sealed partial class SouthStaffordshireCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "South Staffordshire Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.sstaffs.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "south-staffordshire";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Grey,
			Keys = [ "General Waste" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Recycling" ],
		},
		new()
		{
			Name = "Paper & Card Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Recycling" ],
			Type = BinType.Sack,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Garden Waste" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Brown,
			Keys = [ "Food Waste" ],
			Type = BinType.Caddy,
		},
	];

	/// <summary>
	/// Regex for the addresses from the address options.
	/// </summary>
	[GeneratedRegex(@"<option value=""(?<uid>[^""]+)""\s*>(?<address>[^<]+)</option>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for the next collection date and the bins collected on it.
	/// </summary>
	[GeneratedRegex(@"collection-date""><strong>(?<date>[^<]+)</strong>.*?collection-type""><strong>(?<bins>[^<]+)</strong>", RegexOptions.Singleline)]
	private static partial Regex NextBinDayRegex();

	/// <summary>
	/// Regex for the following collection dates and the bins collected on them from the collections table.
	/// </summary>
	[GeneratedRegex(@"<img class=""bin-icon""[^>]*>\s*(?<bins>[^<]+?)\s*</td>\s*<td[^>]*>\s*(?<date>[^<]+?)\s*</td>")]
	private static partial Regex BinDaysRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "postcode", postcode },
				{ "form_id", "bbd_bin_lookup_form" },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://www.sstaffs.gov.uk/viewyourcollectioncalendar?ajax_form=1&_wrapper_format=drupal_ajax",
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
		else if (clientSideResponse.RequestId == 1)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);

			// Iterate through each inserted HTML fragment of the Drupal AJAX response, and create a new address object for each option
			var addresses = new List<Address>();
			foreach (var command in jsonDoc.RootElement.EnumerateArray())
			{
				if (command.GetProperty("command").GetString() != "insert" || command.GetProperty("data").ValueKind != JsonValueKind.String)
				{
					continue;
				}

				foreach (Match rawAddress in AddressRegex().Matches(command.GetProperty("data").GetString()!)!)
				{
					var address = new Address
					{
						Property = rawAddress.Groups["address"].Value.Trim(),
						Postcode = postcode,
						Uid = rawAddress.Groups["uid"].Value,
					};

					addresses.Add(address);
				}
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
				Url = $"https://www.sstaffs.gov.uk/where-i-live?objectId={address.Uid}",
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
			// The next collection is shown on its own, followed by a table of the collections after it
			var rawBinDays = new List<Match>
			{
				NextBinDayRegex().Match(clientSideResponse.Content),
			};
			rawBinDays.AddRange(BinDaysRegex().Matches(clientSideResponse.Content)!);

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var rawBinDay in rawBinDays)
			{
				// Properties served by the van collection have no collection dates
				if (!rawBinDay.Success)
				{
					continue;
				}

				// Parse the date (e.g. "Friday, 23 October 2026")
				var date = DateUtilities.ParseDateExact(rawBinDay.Groups["date"].Value, "dddd, dd MMMM yyyy");

				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, rawBinDay.Groups["bins"].Value);

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
