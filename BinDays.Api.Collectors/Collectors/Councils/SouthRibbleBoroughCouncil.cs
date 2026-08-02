namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for South Ribble Borough Council.
/// </summary>
internal sealed partial class SouthRibbleBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "South Ribble Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://southribble.gov.uk/bincollectiondays");

	/// <inheritdoc/>
	public override string GovUkId => "south-ribble";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Grey,
			Keys = [ "Refuse Collection Service" ],
		},
		new()
		{
			Name = "Plastic, Cans and Glass Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Recycling Collection Service" ],
		},
		// The council returns one recycling service for both co-collected containers.
		new()
		{
			Name = "Paper and Card Recycling",
			Colour = BinColour.Green,
			Keys = [ "Recycling Collection Service" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden Waste Collection" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Green,
			Keys = [ "Food Waste Collection" ],
			Type = BinType.Caddy,
		},
	];

	/// <summary>
	/// The South Ribble bin collection form URL.
	/// </summary>
	private const string _formUrl = "https://forms.chorleysouthribble.gov.uk/xfp/form/70";

	/// <summary>
	/// The fixed form page value required by the council endpoint.
	/// </summary>
	private const string _page = "196";

	/// <summary>
	/// The integrated address lookup element name.
	/// </summary>
	private const string _addressElementName = "qc576c657112a8277ba6f954ebc0490c946168363";

	/// <summary>
	/// Regex for extracting addresses from option elements.
	/// </summary>
	[GeneratedRegex(@"<option\s+value=""(?<uid>[^""]*)""[^>]*>\s*(?<address>[^<]+)\s*</option>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for extracting bin services and dates from the collection table.
	/// </summary>
	[GeneratedRegex(@"<tr>\s*<td>(?<service>[^<]+)</td>\s*<td>(?<date>\d{2}/\d{2}/\d{2})</td>\s*</tr>")]
	private static partial Regex BinDaysRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for address lookup
		if (clientSideResponse == null)
		{
			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "page", _page },
				{ $"{_addressElementName}_0_0", postcode },
			});

			var clientSideRequest = CreatePostRequest(requestBody);

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
				var uid = rawAddress.Groups["uid"].Value;

				if (string.IsNullOrWhiteSpace(uid) || uid == "111111")
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

		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <inheritdoc/>
	public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting bin days
		if (clientSideResponse == null)
		{
			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "page", _page },
				{ $"{_addressElementName}_0_0", address.Postcode! },
				{ $"{_addressElementName}_1_0", address.Uid! },
				{ "next", "Next" },
			});

			var clientSideRequest = CreatePostRequest(requestBody);

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

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var service = rawBinDay.Groups["service"].Value.Trim();
				var collectionDate = rawBinDay.Groups["date"].Value.Trim();

				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, service);

				var binDay = new BinDay
				{
					Date = DateUtilities.ParseDateExact(collectionDate, "dd/MM/yy"),
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

		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <summary>
	/// Creates a form POST request for the South Ribble collection form.
	/// </summary>
	private static ClientSideRequest CreatePostRequest(string requestBody)
	{
		var clientSideRequest = new ClientSideRequest
		{
			RequestId = 1,
			Url = _formUrl,
			Method = "POST",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
				{ "content-type", Constants.FormUrlEncoded },
			},
			Body = requestBody,
		};

		return clientSideRequest;
	}
}
