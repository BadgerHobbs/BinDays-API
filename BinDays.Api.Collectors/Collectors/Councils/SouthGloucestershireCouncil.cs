namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Collectors.Vendors;
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Text.Json;
	using System.Text.Json.Nodes;

	/// <summary>
	/// Collector implementation for South Gloucestershire Council.
	/// </summary>
	internal sealed partial class SouthGloucestershireCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "South Gloucestershire Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://apps.southglos.gov.uk/forms/waste-and-recycling-collection-dates");

		/// <inheritdoc/>
		public override string GovUkId => "south-gloucestershire";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly IReadOnlyCollection<Bin> _binTypes = new List<Bin>()
		{
			new()
			{
				Name = "Black bin",
				Colour = BinColour.Black,
				Keys = new List<string>() { "Refuse" }.AsReadOnly(),
			},
			new()
			{
				Name = "Food waste",
				Colour = BinColour.Black,
				Keys = new List<string>() { "Food" }.AsReadOnly(),
				Type = BinType.Caddy,
			},
			new()
			{
				Name = "Recycling",
				Colour = BinColour.Green,
				Keys = new List<string>() { "Recycling" }.AsReadOnly(),
				Type = BinType.Box,
			},
			new()
			{
				Name = "Garden waste",
				Colour = BinColour.Green,
				Keys = new List<string>() { "Garden" }.AsReadOnly(),
				Type = BinType.Bin,
			},
		}.AsReadOnly();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting addresses
			if (clientSideResponse == null)
			{
				var requestUrl = $"https://webapps.southglos.gov.uk/Webservices/SGC.RefuseCollectionService/RefuseCollectionService.svc/getAddresses/{postcode}";

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = requestUrl,
					Method = "GET",
				};

				var getAddressesResponse = new GetAddressesResponse
				{
					NextClientSideRequest = clientSideRequest
				};

				return getAddressesResponse;
			}
			// Process addresses from response
			else if (clientSideResponse.RequestId == 1)
			{
				// Parse response content as JSON array
				var rawAddresses = JsonSerializer.Deserialize<JsonArray>(clientSideResponse.Content)!;

				// Iterate through each address json, and create a new address object
				var addresses = new List<Address>();
				foreach (var rawAddress in rawAddresses)
				{
					var address = new Address
					{
						Property = rawAddress!["Property"]!.GetValue<string>(),
						Street = rawAddress!["Street"]!.GetValue<string>(),
						Town = rawAddress!["Town"]!.GetValue<string>(),
						Postcode = postcode,
						Uid = rawAddress!["Uprn"]!.GetValue<string>(),
					};

					addresses.Add(address);
				}

				var getAddressesResponse = new GetAddressesResponse
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
				var requestUrl = $"https://api.southglos.gov.uk/wastecomp/GetCollectionDetails?uprn={address.Uid}";

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = requestUrl,
					Method = "GET",
				};

				var getBinDaysResponse = new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest
				};

				return getBinDaysResponse;
			}
			// Process bin days from response
			else if (clientSideResponse.RequestId == 1)
			{
				// Parse response content as JSON object
				var responseJson = JsonSerializer.Deserialize<JsonObject>(clientSideResponse.Content)!;
				var rawBinDayCollections = responseJson["value"]!.AsArray();

				// Iterate through each collection type result
				var binDays = new List<BinDay>();
				foreach (var rawBinDayCollection in rawBinDayCollections)
				{
					var serviceName = rawBinDayCollection!["hso_servicename"]!.GetValue<string>();
					var nextCollection = rawBinDayCollection["hso_nextcollection"]!.GetValue<string>();

					// Find matching bin types based on the service name containing a key (case-insensitive)
					var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, serviceName);

					// Parse the date string (e.g. "2026-01-08T07:00:00+00:00")
					var date = DateTimeOffset.Parse(nextCollection, CultureInfo.InvariantCulture).Date;

					var binDay = new BinDay
					{
						Date = DateOnly.FromDateTime(date),
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
	}
}
