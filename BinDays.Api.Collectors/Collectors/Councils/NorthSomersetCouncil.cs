namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for North Somerset Council.
/// </summary>
internal sealed partial class NorthSomersetCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "North Somerset Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.n-somerset.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "north-somerset";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Rubbish" ],
		},
		new()
		{
			Name = "Glass, Paper & Card Recycling",
			Colour = BinColour.Green,
			Keys = [ "Recycling" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Plastic Bottles, Pots & Trays Recycling",
			Colour = BinColour.Red,
			Keys = [ "Recycling" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Brown,
			Keys = [ "Food" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Garden waste" ],
		},
	];

	/// <summary>
	/// The URL of the collection schedule form, which both the address search and the address selection post to.
	/// </summary>
	private const string _collectionScheduleUrl = "https://forms.n-somerset.gov.uk/Waste/CollectionSchedule";

	/// <summary>
	/// Regex for the addresses from the data, excluding the postcode the council appends to each option.
	/// </summary>
	[GeneratedRegex(@"<option value=""(?<uid>\d+)"">(?<address>[^<]+), [^,<]+</option>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for the bin days from the data, capturing the next and following collection dates for each service.
	/// </summary>
	[GeneratedRegex(@"<tr>\s*<td>(?<service>[^<]+)</td>\s*<td>(?<nextDate>[^<]+)</td>\s*<td>(?<followingDate>[^<]+)</td>\s*</tr>")]
	private static partial Regex BinDaysRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var formData = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "Postcode", postcode },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _collectionScheduleUrl,
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
				},
				Body = formData,
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
					Property = rawAddress.Groups["address"].Value.Trim(),
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
		// Prepare client-side request for selecting the address
		if (clientSideResponse == null)
		{
			// PreviousPostcode must match Postcode, otherwise the form treats it as a new address search
			var formData = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "PreviousPostcode", address.Postcode! },
				{ "Postcode", address.Postcode! },
				{ "SelectedUprn", address.Uid! },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _collectionScheduleUrl,
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
		// Prepare client-side request for the collection schedule held against the session
		else if (clientSideResponse.RequestId == 1)
		{
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_collectionScheduleUrl}/ViewSchedule",
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
		else if (clientSideResponse.RequestId == 2)
		{
			var rawBinDays = BinDaysRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var service = rawBinDay.Groups["service"].Value.Trim();
				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, service);

				var collectionDates = new[]
				{
					rawBinDay.Groups["nextDate"].Value.Trim(),
					rawBinDay.Groups["followingDate"].Value.Trim(),
				};

				foreach (var collectionDate in collectionDates)
				{
					var binDay = new BinDay
					{
						Date = DateUtilities.ParseDateInferringYear(collectionDate, "dddd d MMMM"),
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
