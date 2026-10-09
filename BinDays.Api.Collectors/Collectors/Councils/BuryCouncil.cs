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
/// Collector implementation for Bury Council.
/// </summary>
internal sealed partial class BuryCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Bury Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.bury.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "bury";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Grey,
			Keys = [ "grey" ],
		},
		new()
		{
			Name = "Garden & Food Waste",
			Colour = BinColour.Brown,
			Keys = [ "brown" ],
		},
		new()
		{
			Name = "Paper & Cardboard Recycling",
			Colour = BinColour.Green,
			Keys = [ "green" ],
		},
		new()
		{
			Name = "Glass, Plastic, Cans & Foil Recycling",
			Colour = BinColour.Blue,
			Keys = [ "blue" ],
		},
	];

	/// <summary>
	/// Regex for removing the st|nd|rd|th from the date part.
	/// </summary>
	[GeneratedRegex(@"(?<=\d)(st|nd|rd|th)")]
	private static partial Regex OrdinalSuffixRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://www.bury.gov.uk/app-services/getProperties?postcode={postcode}",
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
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var rawAddresses = jsonDoc.RootElement.GetProperty("response").EnumerateArray();

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var rawAddress in rawAddresses)
			{
				var addressParts = new[]
				{
					rawAddress.GetProperty("addressLine1").GetString(),
					rawAddress.GetProperty("city").GetString(),
				};

				var address = new Address
				{
					Property = string.Join(", ", addressParts.Where(p => !string.IsNullOrWhiteSpace(p))),
					Postcode = postcode,
					Uid = rawAddress.GetProperty("id").GetString()!,
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
				Url = $"https://www.bury.gov.uk/app-services/getPropertyById?id={address.Uid}",
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
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var rawMonths = jsonDoc.RootElement.GetProperty("response").GetProperty("calendar").EnumerateArray();

			// Iterate through each month of the calendar, and create a new bin day object for each of its dates
			var binDays = new List<BinDay>();
			foreach (var rawMonth in rawMonths)
			{
				var month = rawMonth.GetProperty("date").GetString()!;

				// Iterate through each bin day, and create a new bin day object
				foreach (var rawBinDay in rawMonth.GetProperty("dates").EnumerateArray())
				{
					// Combine the day and month (e.g. "Thursday 1st" and "October 2026")
					var collectionDate = OrdinalSuffixRegex().Replace(rawBinDay.GetProperty("date").GetString()!, "");
					var date = DateUtilities.ParseDateExact($"{collectionDate} {month}", "dddd d MMMM yyyy");

					// Iterate through each bin collected on the date, and create a new bin day object
					foreach (var rawBin in rawBinDay.GetProperty("bins").EnumerateArray())
					{
						var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, rawBin.GetString()!);

						var binDay = new BinDay
						{
							Date = date,
							Address = address,
							Bins = matchedBinTypes,
						};

						binDays.Add(binDay);
					}
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
