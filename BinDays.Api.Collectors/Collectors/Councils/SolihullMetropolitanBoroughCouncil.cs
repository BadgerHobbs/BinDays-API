namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Solihull Metropolitan Borough Council.
/// </summary>
internal sealed partial class SolihullMetropolitanBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Solihull Metropolitan Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://digital.solihull.gov.uk/BinCollectionCalendar/");

	/// <inheritdoc/>
	public override string GovUkId => "solihull";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes = [
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Green Wheelie Bin" ],
		},
		new()
		{
			Name = "Glass Recycling",
			Colour = BinColour.Black,
			Keys = [ "Black Box" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Brown,
			Keys = [ "Brown Wheelie Bin" ],
		},
		new()
		{
			Name = "Household Waste",
			Colour = BinColour.Black,
			Keys = [ "Black Wheelie Bin" ],
		},
	];

	/// <summary>
	/// Regex for the viewstate token values from input fields.
	/// </summary>
	[GeneratedRegex(@"<input[^>]*?(?:name|id)=[""']__VIEWSTATE[""'][^>]*?value=[""'](?<viewStateValue>[^""']*)[""'][^>]*?/?>")]
	private static partial Regex ViewStateTokenRegex();

	/// <summary>
	/// Regex for the event validation values from input fields.
	/// </summary>
	[GeneratedRegex(@"<input[^>]*?(?:name|id)=[""']__EVENTVALIDATION[""'][^>]*?value=[""'](?<viewStateValue>[^""']*)[""'][^>]*?/?>")]
	private static partial Regex EventValidationRegex();

	/// <summary>
	/// Regex for the addresses from the options elements.
	/// </summary>
	[GeneratedRegex(@"<option\s+value=""(?<uid>\d+)""[^>]*>\s*(?<address>.*?)\s*</option>")]
	private static partial Regex AddressesRegex();

	/// <summary>
	/// Regex for the bin days from the data table elements.
	/// </summary>
	[GeneratedRegex(@"(?s)<div class=""card-title"">\s*<h4>(?<binType>[^<]+)</h4>.*?<i class='far fa-hand-point-right fa-lg'></i>\s*Your next collection will be on\s*<strong>(?<nextCollectionDate>[^<]+)\s*</strong>")]
	private static partial Regex BinDaysRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting token
		if (clientSideResponse == null)
		{
			// Prepare client-side request
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://digital.solihull.gov.uk/BinCollectionCalendar/",
				Method = "GET",
				Headers = new() {
					{"user-agent", Constants.UserAgent},
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for getting addresses
		else if (clientSideResponse.RequestId == 1)
		{
			// Get viewstate and event validation from response
			var viewState = ViewStateTokenRegex().Match(clientSideResponse.Content).Groups[1].Value;
			var eventValidation = EventValidationRegex().Match(clientSideResponse.Content).Groups[1].Value;

			// Prepare client-side request
			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{"__VIEWSTATE", viewState},
				{"__EVENTVALIDATION", eventValidation},
				{"txtPostCode", postcode},
				{"butFindAddress", "Find+Address"},
			});

			var requestHeaders = new Dictionary<string, string> {
				{"user-agent", Constants.UserAgent},
				{"content-type", "application/x-www-form-urlencoded"},
			};

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = "https://digital.solihull.gov.uk/BinCollectionCalendar/",
				Method = "POST",
				Headers = requestHeaders,
				Body = requestBody,
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 2)
		{
			// Get addresses from response
			var rawAddresses = AddressesRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				var property = rawAddress.Groups["address"].Value;
				var uprn = rawAddress.Groups["uid"].Value;

				// Skip uprn if zero
				if (uprn == "0")
				{
					continue;
				}

				var address = new Address
				{
					Property = property,
					Postcode = postcode,
					Uid = uprn,
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
		// Prepare client-side request for getting token
		if (clientSideResponse == null)
		{
			// Prepare client-side request
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://digital.solihull.gov.uk/BinCollectionCalendar/",
				Method = "GET",
				Headers = new() {
					{"user-agent", Constants.UserAgent},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting bin days
		else if (clientSideResponse.RequestId == 1)
		{
			// Get viewstate and event validation from response
			_ = ViewStateTokenRegex().Match(clientSideResponse.Content).Groups[1].Value;
			_ = EventValidationRegex().Match(clientSideResponse.Content).Groups[1].Value;

			// Prepare client-side request
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"https://digital.solihull.gov.uk/BinCollectionCalendar/Calendar.aspx?UPRN={address.Uid}",
				Method = "GET",
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getBinDaysResponse;
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 2)
		{
			// Get bin days from response
			var rawBinDays = BinDaysRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var service = rawBinDay.Groups["binType"].Value;
				var collectionDate = rawBinDay.Groups["nextCollectionDate"].Value;

				// Parse the collection date (e.g. Monday, 12 May 2025)
				var date = DateOnly.ParseExact(
					collectionDate,
					"dddd, d MMMM yyyy",
					CultureInfo.InvariantCulture,
					DateTimeStyles.None
				);

				// Get matching bin types from the service using the keys
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
