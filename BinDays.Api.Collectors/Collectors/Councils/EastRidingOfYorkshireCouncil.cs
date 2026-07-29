namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

/// <summary>
/// Collector implementation for East Riding of Yorkshire Council.
/// </summary>
internal sealed class EastRidingOfYorkshireCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "East Riding of Yorkshire Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.eastriding.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "east-riding-of-yorkshire";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Keys = [ "GreenDate" ],
		},
		new()
		{
			Name = "Garden and Food Waste",
			Colour = BinColour.Brown,
			Keys = [ "BrownDate" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Blue,
			Keys = [ "BlueDate" ],
		},
	];

	/// <summary>
	/// A single property's entry from the CollectionsData API response.
	/// </summary>
	private sealed record CollectionEntry(string Uprn, string Address, string GreenDate, string BrownDate, string BlueDate);

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = BuildCollectionsDataUrl(postcode),
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
			// Uid format: uprn
			var addresses = ParseCollectionEntries(clientSideResponse.Content)
				.Select(entry => new Address
				{
					Property = entry.Address,
					Postcode = postcode,
					Uid = entry.Uprn,
				});

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
		// Prepare client-side request for getting current collection dates
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = BuildCollectionsDataUrl(address.Postcode!),
				Method = "GET",
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin days from the current collection dates for this UPRN
		else if (clientSideResponse.RequestId == 1)
		{
			// Uid is either just the uprn (current format), or the legacy
			// "uprn;greenDate;brownDate;blueDate" format (dates ignored, always refetched live).
			var uprn = address.Uid!.Split(';', 2)[0];

			var entry = ParseCollectionEntries(clientSideResponse.Content)
				.First(e => e.Uprn == uprn);

			var collectionEntries = new[]
			{
				new
				{
					Service = "GreenDate",
					CollectionDate = entry.GreenDate,
				},
				new
				{
					Service = "BrownDate",
					CollectionDate = entry.BrownDate,
				},
				new
				{
					Service = "BlueDate",
					CollectionDate = entry.BlueDate,
				},
			};

			// Iterate through each collection entry, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var collectionEntry in collectionEntries)
			{
				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, collectionEntry.Service);

				var binDay = new BinDay
				{
					Date = DateUtilities.ParseDateExact(collectionEntry.CollectionDate, "yyyy-MM-dd'T'HH:mm:ss"),
					Address = address,
					Bins = matchedBins,
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

	/// <summary>
	/// Builds the CollectionsData API URL for a given postcode.
	/// </summary>
	private static string BuildCollectionsDataUrl(string postcode) =>
		$"https://wasterecyclingapi.eastriding.gov.uk/api/RecyclingData/CollectionsData?APIKey=ekBWR8tSiv6qwMo31REEeTZ5FAiMNB&Licensee=BinCollectionWebTeam&Postcode={postcode}";

	/// <summary>
	/// Parses the CollectionsData API response, which may be either XML or JSON.
	/// </summary>
	private static List<CollectionEntry> ParseCollectionEntries(string content)
	{
		var trimmedContent = content.TrimStart();
		var entries = new List<CollectionEntry>();

		if (trimmedContent.StartsWith('<'))
		{
			var xml = XDocument.Parse(trimmedContent);
			var ns = xml.Root!.GetDefaultNamespace();

			foreach (var rawEntry in xml.Descendants(ns + "collectionDateOutput"))
			{
				entries.Add(new CollectionEntry(
					rawEntry.Element(ns + "UPRN")!.Value.Trim(),
					rawEntry.Element(ns + "Address")!.Value.Trim(),
					rawEntry.Element(ns + "GreenDate")!.Value.Trim(),
					rawEntry.Element(ns + "BrownDate")!.Value.Trim(),
					rawEntry.Element(ns + "BlueDate")!.Value.Trim()
				));
			}
		}
		else
		{
			using var jsonDocument = JsonDocument.Parse(trimmedContent);

			foreach (var rawEntry in jsonDocument.RootElement.GetProperty("dataReturned").EnumerateArray())
			{
				entries.Add(new CollectionEntry(
					rawEntry.GetProperty("UPRN").GetString()!.Trim(),
					rawEntry.GetProperty("Address").GetString()!.Trim(),
					rawEntry.GetProperty("GreenDate").GetString()!,
					rawEntry.GetProperty("BrownDate").GetString()!,
					rawEntry.GetProperty("BlueDate").GetString()!
				));
			}
		}

		return entries;
	}
}
