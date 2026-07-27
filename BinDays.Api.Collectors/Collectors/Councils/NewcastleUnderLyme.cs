namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Newcastle-under-Lyme.
/// </summary>
internal sealed partial class NewcastleUnderLyme : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Newcastle-under-Lyme";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.newcastle-staffs.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "newcastle-under-lyme";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Household Rubbish" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Recycling" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden Waste" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Grey,
			Keys = [ "Food Waste" ],
			Type = BinType.Caddy,
		},
	];

	/// <summary>
	/// Regex for extracting bin day rows from the HTML table.
	/// </summary>
	[GeneratedRegex(@"<tr>\s*<td>(?<date>[^<]+)</td>\s*<td>(?<services>.*?)</td>\s*</tr>", RegexOptions.Singleline)]
	private static partial Regex BinDayRowRegex();

	/// <summary>
	/// Regex for extracting service names from each bin day row.
	/// </summary>
	[GeneratedRegex(@"(?<service>[^<]+)<br\s*/?>", RegexOptions.Singleline)]
	private static partial Regex ServiceRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://www.newcastle-staffs.gov.uk/bartec-ajax/{postcode}",
				Method = "POST",
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

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var rawAddress in jsonDoc.RootElement.EnumerateArray())
			{
				var address = new Address
				{
					Property = rawAddress.GetProperty("label").GetString()!.Trim(),
					Postcode = postcode,
					Uid = rawAddress.GetProperty("uprn").GetString()!,
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
				Url = $"https://www.newcastle-staffs.gov.uk/homepage/97/check-your-bin-day?uprn={address.Uid!}",
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
			var rawBinDays = BinDayRowRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each bin day row, and create new bin day objects
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var dateString = rawBinDay.Groups["date"].Value.Trim();
				var date = DateUtilities.ParseDateInferringYear(dateString, "dddd d MMMM");

				var rawServices = ServiceRegex().Matches(rawBinDay.Groups["services"].Value)!;

				// Iterate through each service, and create a new bin day object
				foreach (Match rawService in rawServices)
				{
					var service = rawService.Groups["service"].Value.Trim();
					var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, service);

					var binDay = new BinDay
					{
						Date = date,
						Address = address,
						Bins = matchedBins,
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
