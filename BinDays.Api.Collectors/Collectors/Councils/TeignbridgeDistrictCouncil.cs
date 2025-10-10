namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Globalization;
	using System.Text.Json;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Collector implementation for Teignbridge District Council.
	/// </summary>
	internal sealed partial class TeignbridgeDistrictCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "Teignbridge District Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://www.teignbridge.gov.uk/recycling-and-waste/bin-and-box-collections/when-are-my-bins-and-boxes-collected/");

		/// <inheritdoc/>
		public override string GovUkId => "teignbridge";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly ReadOnlyCollection<Bin> _binTypes = new List<Bin>()
		{
			new()
			{
				Name = "Household Waste",
				Colour = BinColour.Black,
				Keys = new List<string>() { "refuse" }.AsReadOnly(),
				Type = BinType.Bin,

			},
			new()
			{
				Name = "Food Waste",
				Colour = BinColour.Blue,
				Keys = new List<string>() { "food waste" }.AsReadOnly(),
				Type = BinType.Container,
			},
			new()
			{
				Name = "Plastic and Metals",
				Colour = BinColour.Black,
				Keys = new List<string>() { "black box" }.AsReadOnly(),
				Type = BinType.Box,
			},
			new()
			{
				Name = "Cardboard and Glass Recycling",
				Colour = BinColour.Green,
				Keys = new List<string>() { "green box" }.AsReadOnly(),
				Type = BinType.Box,
			},
			new()
			{
				Name = "Paper Recycling",
				Colour = BinColour.Blue,
				Keys = new List<string>() { "paper" }.AsReadOnly(),
				Type = BinType.Sack,
			},
			new()
			{
				Name = "Garden Waste",
				Colour = BinColour.Purple,
				Keys = new List<string>() { "garden waste" }.AsReadOnly(),
			},
		}.AsReadOnly();

		/// <summary>
		/// Regex for the bin collections from the page elements.
		/// </summary>
		[GeneratedRegex(@"(?s)<h3 class=""binCollectionH3"">\s*(?<CollectionDate>\d{1,2}\s+\w+\s+\d{4})\s*<span class=""binDayDescriptor"">\w+</span>\s*</h3>\s*<div class=""binInfoContainer"">\s*(?:<div class=""binInfoLine"">.*?</span>\s*(?:</a>\s*)?(?<BinType>[^<]+?)</div>\s*)+\s*</div>")]
		private static partial Regex BinCollectionsRegex();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting addresses
			if (clientSideResponse == null)
			{
				var requestUrl = $"https://www.teignbridge.gov.uk/repositories/hidden-pages/address-finder?qtype=bins&term={postcode}";

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = requestUrl,
					Method = "GET",
				};

				var getAddressesResponse = new GetAddressesResponse()
				{
					NextClientSideRequest = clientSideRequest
				};

				return getAddressesResponse;
			}
			// Process addresses from response
			else if (clientSideResponse.RequestId == 1)
			{
				// Parse response content as JSON array
				using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);

				// Iterate through each address json, and create a new address object
				var addresses = new List<Address>();
				foreach (var addressElement in jsonDoc.RootElement.EnumerateArray())
				{
					string? property = addressElement.GetProperty("label").GetString();
					string? uprn = addressElement.GetProperty("UPRN").GetString();

					var address = new Address()
					{
						Property = property?.Trim(),
						Postcode = postcode,
						Uid = uprn,
					};

					addresses.Add(address);
				}

				var getAddressesResponse = new GetAddressesResponse()
				{
					Addresses = addresses.AsReadOnly(),
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
				var requestUrl = $"https://www.teignbridge.gov.uk/repositories/hidden-pages/bin-finder?uprn={address.Uid}";

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = requestUrl,
					Method = "GET",
				};

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					NextClientSideRequest = clientSideRequest
				};

				return getBinDaysResponse;
			}
			// Process bin days from response
			else if (clientSideResponse.RequestId == 1)
			{
				// Get bin collections from response
				var rawBinCollections = BinCollectionsRegex().Matches(clientSideResponse.Content);

				// Iterate through each bin collection, and create a new bin day object
				var binDays = new List<BinDay>();
				foreach (Match rawBinCollection in rawBinCollections)
				{
					var dateString = rawBinCollection.Groups["CollectionDate"].Value;

					// Parse the date (e.g. '12 June 2025')
					var date = DateOnly.ParseExact(
						dateString,
						"d MMMM yyyy",
						CultureInfo.InvariantCulture,
						DateTimeStyles.None
					);

					// Get the bin types from the collection
					var rawBinTypes = rawBinCollection.Groups["BinType"].Captures;

					// Get matching bin types from the type using the keys
					var matchedBinTypes = rawBinTypes
						.SelectMany(rawBinType => ProcessingUtilities.GetMatchingBins(_binTypes, rawBinType.Value))
						.Distinct()
						.ToList()
						.AsReadOnly();

					var binDay = new BinDay()
					{
						Date = date,
						Address = address,
						Bins = matchedBinTypes,
					};

					binDays.Add(binDay);
				}

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					BinDays = ProcessingUtilities.ProcessBinDays(binDays),
				};

				return getBinDaysResponse;
			}

			// Throw exception for invalid request
			throw new InvalidOperationException("Invalid client-side request.");
		}
	}
}
