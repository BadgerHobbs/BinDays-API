namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Erewash Borough Council.
/// </summary>
internal sealed partial class ErewashBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Erewash Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.erewash.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "erewash";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Domestic Waste Collection Service" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Recycling Collection Service" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden Waste Collection Service" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Green,
			Keys = [ "Food Collection Service" ],
			Type = BinType.Caddy,
		},
	];

	/// <summary>
	/// Regex for the addresses from the address select options.
	/// </summary>
	[GeneratedRegex(@"<option value=""(?<uid>\d+)"">(?<address>[^<]+)</option>")]
	private static partial Regex AddressRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "postcode", postcode },
				{ "form_id", "bbd_whitespace_bbd_whitespace_address_search" },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://www.erewash.gov.uk/bins-and-recycling/when-my-bin-day",
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
			var rawAddresses = AddressRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				var address = new Address
				{
					Property = WebUtility.HtmlDecode(rawAddress.Groups["address"].Value).Trim(),
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
				Url = $"https://www.erewash.gov.uk/bbd-whitespace/one-year-collection-dates-without-christmas?uprn={address.Uid}",
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

			// The collection dates are grouped by month in the Drupal settings command
			var rawMonths = jsonDoc.RootElement[0]
				.GetProperty("settings")
				.GetProperty("collection_dates");

			// Iterate through each collection, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var rawMonth in rawMonths.EnumerateObject())
			{
				foreach (var rawCollection in rawMonth.Value.EnumerateArray())
				{
					var service = rawCollection.GetProperty("service").GetString()!;
					var date = DateUtilities.ParseDateExact(rawCollection.GetProperty("date").GetString()!, "yyyy-MM-dd");

					var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, service);

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
}
