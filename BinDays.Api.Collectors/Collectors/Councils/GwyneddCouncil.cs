namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Exceptions;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Gwynedd Council.
/// </summary>
internal sealed partial class GwyneddCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Gwynedd Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.gwynedd.llyw.cymru/en-gb/bins-recycling/when-is-my-waste-collected");

	/// <inheritdoc/>
	public override string GovUkId => "gwynedd";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Keys = [ "Green bin" ],
		},
		new()
		{
			Name = "Paper, Card, Plastic & Cans Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Blue box / food waste" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Brown,
			Keys = [ "Blue box / food waste" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Brown bin (garden waste)" ],
		},
		new()
		{
			Name = "Nappy Waste",
			Colour = BinColour.Yellow,
			Keys = [ "Nappy" ],
			Type = BinType.Bag,
		},
	];

	/// <summary>
	/// Regex for the addresses from the address search response.
	/// </summary>
	[GeneratedRegex(@"<li><a href=""/Daearyddol/en/ChwilioCyfeiriad/Dewis/LleDwinByw/(?<uid>\d+)"">(?<address>[^<]+)</a></li>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for detecting whether another page of address results follows.
	/// </summary>
	[GeneratedRegex(@"<a href=""[^""]+"" rel=""next"">")]
	private static partial Regex NextPageRegex();

	/// <summary>
	/// Regex for the bin days from the "Where I live" response.
	/// </summary>
	[GeneratedRegex(@"<li>\s*(?<service>[^:<]+):\s*(?<date>[A-Za-z]+ \d{2}/\d{2}/\d{4})")]
	private static partial Regex BinDaysRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://diogel.gwynedd.llyw.cymru/Daearyddol/en/ChwilioCyfeiriad/Index/LleDwinByw?codPost={postcode}",
				Method = "GET",
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Process addresses from response, following pagination as required
		else if (clientSideResponse.RequestId == 1)
		{
			var rawAddresses = AddressRegex().Matches(clientSideResponse.Content)!;

			var addresses = new List<Address>();

			// Restore addresses accumulated from previous pages
			if (clientSideResponse.Options.Metadata.TryGetValue("addresses", out var accumulatedAddresses))
			{
				// Iterate through each accumulated address, and restore it
				foreach (var entry in accumulatedAddresses.Split('\n', StringSplitOptions.RemoveEmptyEntries))
				{
					var parts = entry.Split('|', 2);

					addresses.Add(new Address
					{
						Property = parts[0],
						Postcode = postcode,
						Uid = parts[1],
					});
				}
			}

			// Iterate through each address, and create a new address object
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

			// Request the next page of results if the response indicates one is available
			if (NextPageRegex().IsMatch(clientSideResponse.Content))
			{
				var currentPage = clientSideResponse.Options.Metadata.TryGetValue("page", out var pageValue)
					? int.Parse(pageValue, CultureInfo.InvariantCulture)
					: 1;

				// Iterate through each address, and serialize it to carry forward to the next page
				var serializedAddresses = new List<string>();
				foreach (var address in addresses)
				{
					serializedAddresses.Add($"{address.Property}|{address.Uid}");
				}

				var nextPageClientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = $"https://diogel.gwynedd.llyw.cymru/Daearyddol/en/ChwilioCyfeiriad/Index/LleDwinByw?codPost={postcode}&Tudalen={currentPage + 1}",
					Method = "GET",
					Options = new ClientSideOptions
					{
						Metadata = new()
						{
							{ "page", (currentPage + 1).ToString(CultureInfo.InvariantCulture) },
							{ "addresses", string.Join("\n", serializedAddresses) },
						},
					},
				};

				var nextPageResponse = new GetAddressesResponse
				{
					NextClientSideRequest = nextPageClientSideRequest,
				};

				return nextPageResponse;
			}

			if (addresses.Count == 0)
			{
				throw new AddressesNotFoundException(GovUkId, postcode);
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
				Url = $"https://diogel.gwynedd.llyw.cymru/Daearyddol/en/LleDwinByw/Index/{address.Uid}",
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

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var service = rawBinDay.Groups["service"].Value.Trim();
				var collectionDate = rawBinDay.Groups["date"].Value.Trim();

				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, service);

				if (matchedBinTypes.Count == 0)
				{
					continue;
				}

				var binDay = new BinDay
				{
					Date = DateUtilities.ParseDateExact(collectionDate, "dddd dd/MM/yyyy"),
					Address = address,
					Bins = matchedBinTypes,
				};

				binDays.Add(binDay);
			}

			if (binDays.Count == 0)
			{
				throw new BinDaysNotFoundException(GovUkId, address.Postcode!, address.Uid!);
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
