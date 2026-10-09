namespace BinDays.Api.Collectors.Collectors.Vendors;

using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Base collector implementation for councils using the shared Bromsgrove and Redditch bin collections app.
/// </summary>
internal abstract partial class BromsgroveRedditchCollectorBase : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public abstract string Name { get; }

	/// <inheritdoc/>
	public abstract Uri WebsiteUrl { get; }

	/// <summary>
	/// Gets the base URL of the council's bin collections app, including the trailing slash.
	/// </summary>
	protected abstract string BaseUrl { get; }

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Grey,
			Keys = [ "Grey Bin" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Green,
			Keys = [ "Green Bin" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Brown" ],
		},
	];

	/// <summary>
	/// Regex for the addresses from the options elements.
	/// </summary>
	[GeneratedRegex(@"<option value=""(?<uid>\d+)"">(?<address>[^<]+)</option>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for the bins and their collection dates from the collection containers.
	/// </summary>
	[GeneratedRegex(@"(?s)<div class=""heading"">\s*(?<service>[^<]+?)\s*</div>.*?<div class=""collection-details-container"">(?<dates>.*?)</div>")]
	private static partial Regex BinDaysRegex();

	/// <summary>
	/// Regex for the collection dates of a bin (e.g. "16 October 2026").
	/// </summary>
	[GeneratedRegex(@"(?<date>\d{2} [A-Za-z]+ \d{4})")]
	private static partial Regex CollectionDateRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "PostCode", postcode },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = BaseUrl,
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
			var seenUids = new HashSet<string>();
			foreach (Match rawAddress in rawAddresses)
			{
				var uid = rawAddress.Groups["uid"].Value;

				// The same property can be listed several times
				if (!seenUids.Add(uid))
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
				Url = $"{BaseUrl}BinCollections/Details?UPRN={address.Uid}",
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
			var rawBinDays = BinDaysRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each bin, and create a new bin day object for each of its collection dates
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var service = rawBinDay.Groups["service"].Value;
				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, service);

				// Iterate through each collection date, and create a new bin day object
				foreach (Match rawDate in CollectionDateRegex().Matches(rawBinDay.Groups["dates"].Value)!)
				{
					var binDay = new BinDay
					{
						Date = DateUtilities.ParseDateExact(rawDate.Groups["date"].Value, "dd MMMM yyyy"),
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
