namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Exceptions;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Renfrewshire Council.
/// </summary>
internal sealed partial class RenfrewshireCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Renfrewshire Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.renfrewshire.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "renfrewshire";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Grey,
			Keys = [ "Grey" ],
		},
		new()
		{
			Name = "Paper and Card Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Blue" ],
		},
		new()
		{
			Name = "Plastics, Cans and Glass Recycling",
			Colour = BinColour.Green,
			Keys = [ "Green" ],
		},
		new()
		{
			Name = "Food and Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Brown" ],
		},
	];

	/// <summary>
	/// Regex for the addresses from the response data.
	/// </summary>
	[GeneratedRegex(@"<option\s+value=""(?<uid>[^""]*)""[^>]*>(?<address>[^<]+)</option>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for the collections JSON from the response data.
	/// </summary>
	[GeneratedRegex(@"(?s)<script type=""application/json"" id=""collections-data"">\s*(?<collectionsData>\{.*?\})\s*</script>")]
	private static partial Regex CollectionsDataRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://www.renfrewshire.gov.uk/bins-and-recycling/bin-collection/bin-collection-calendar/check-your-bin-collection-day/find?postcode={postcode}",
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
			var rawAddresses = AddressRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				var uid = rawAddress.Groups["uid"].Value.Trim();

				if (string.IsNullOrWhiteSpace(uid))
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
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://www.renfrewshire.gov.uk/bins-and-recycling/bin-collection/bin-collection-calendar/check-your-bin-collection-day/view/{address.Uid!}",
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
			var rawCollectionsData = CollectionsDataRegex().Match(clientSideResponse.Content)!;

			// The collections data is only rendered when the property has upcoming collections
			if (!rawCollectionsData.Success)
			{
				throw new BinDaysNotFoundException(GovUkId, address.Postcode!, address.Uid!);
			}

			using var jsonDocument = JsonDocument.Parse(rawCollectionsData.Groups["collectionsData"].Value);

			// Iterate through each collection date, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var rawCollectionDate in jsonDocument.RootElement.EnumerateObject())
			{
				var matchedBins = new List<Bin>();

				// Iterate through each service on this date, and get matching bins
				foreach (var rawService in rawCollectionDate.Value.EnumerateObject())
				{
					if (rawService.Value.ValueKind == JsonValueKind.Null)
					{
						continue;
					}

					var service = rawService.Value.GetProperty("ShortName").GetString()!;
					matchedBins.AddRange(ProcessingUtilities.GetMatchingBins(_binTypes, service));
				}

				if (matchedBins.Count == 0)
				{
					continue;
				}

				var binDay = new BinDay
				{
					Date = DateUtilities.ParseDateExact(rawCollectionDate.Name, "yyyy-MM-dd"),
					Address = address,
					Bins = [.. matchedBins],
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
}
