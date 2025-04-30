// This file was converted from the legacy dart implementation using AI.
// TODO: Manually review and improve this file.

namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Globalization;
	using System.Linq;
	using System.Text.Json;

	/// <summary>
	/// Collector implementation for South Gloucestershire Council.
	/// </summary>
	internal sealed partial class SouthGloucestershireCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "South Gloucestershire Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://beta.southglos.gov.uk/waste-and-recycling-collection-dates/");

		/// <inheritdoc/>
		public override string GovUkId => "south-gloucestershire";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly ReadOnlyCollection<Bin> binTypes = new List<Bin>()
		{
			new()
			{
				Name = "Recycling",
				Colour = "Green",
				Keys = new List<string>() { "C", "R" }.AsReadOnly(),
			},
			new()
			{
				Name = "Food Waste",
				Colour = "Grey",
				Keys = new List<string>() { "C", "R" }.AsReadOnly(),
			},
			new()
			{
				Name = "General Waste",
				Colour = "Black",
				Keys = new List<string>() { "R" }.AsReadOnly(),
			},
		}.AsReadOnly();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting addresses
			if (clientSideResponse == null)
			{
				var requestUrl = $"https://webapps.southglos.gov.uk/Webservices/SGC.RefuseCollectionService/RefuseCollectionService.svc/getAddresses/{Uri.EscapeDataString(postcode)}";

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = requestUrl,
					Method = "GET",
					Headers = [],
					Body = string.Empty,
				};

				var getAddressesResponse = new GetAddressesResponse()
				{
					Addresses = null,
					NextClientSideRequest = clientSideRequest
				};

				return getAddressesResponse;
			}
			// Process addresses from response
			else if (clientSideResponse.RequestId == 1)
			{
				var addresses = new List<Address>();

				// Parse response content as JSON array
				using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);

				// Iterate through each address json, and create a new address object
				foreach (var addressElement in jsonDoc.RootElement.EnumerateArray())
				{
					string? property = addressElement.GetProperty("Property").GetString();
					string? street = addressElement.GetProperty("Street").GetString();
					string? town = addressElement.GetProperty("Town").GetString();
					string? uprn = addressElement.GetProperty("Uprn").GetString();

					var address = new Address()
					{
						Property = property?.Trim(),
						Street = street?.Trim(),
						Town = town?.Trim(),
						Postcode = postcode,
						Uid = uprn,
					};

					addresses.Add(address);
				}

				var getAddressesResponse = new GetAddressesResponse()
				{
					Addresses = addresses.AsReadOnly(),
					NextClientSideRequest = null
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
				var requestUrl = $"https://webapps.southglos.gov.uk/Webservices/SGC.RefuseCollectionService/RefuseCollectionService.svc/getCollections/{address.Uid}";

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = requestUrl,
					Method = "GET",
					Headers = [],
					Body = string.Empty,
				};

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					BinDays = null,
					NextClientSideRequest = clientSideRequest
				};

				return getBinDaysResponse;
			}
			// Process bin days from response
			else if (clientSideResponse.RequestId == 1)
			{
				var binDays = new List<BinDay>(); // Initialize the list directly

				// Parse response content as JSON object (within an array)
				using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);

				// Get the first element which contains the collection data
				// Add null checks for robustness
				if (jsonDoc.RootElement.ValueKind != JsonValueKind.Array || jsonDoc.RootElement.GetArrayLength() == 0)
				{
					// Handle empty or unexpected response format
					return new GetBinDaysResponse()
					{
						BinDays = new List<BinDay>().AsReadOnly(), // Return empty list
						NextClientSideRequest = null
					};
				}
				var rawBinDaysObject = jsonDoc.RootElement[0];

				// Iterate through each property (collection type and date)
				foreach (var property in rawBinDaysObject.EnumerateObject())
				{
					var dateString = property.Value.GetString();

					// Try parsing the date string (e.g., "15/04/2025")
					if (string.IsNullOrEmpty(dateString) || !DateOnly.TryParseExact(dateString, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var collectionDate))
					{
						// Skip if date parsing fails or date string is null/empty
						continue;
					}

					// Get the collection code (e.g., "C1", "R2") and extract the type character
					var collectionCode = property.Name;
					if (string.IsNullOrEmpty(collectionCode)) continue; // Skip if property name is empty
					var typeChar = collectionCode[0].ToString();

					// Find matching bin types based on the type character in their keys
					var matchedBins = this.binTypes.Where(bin => bin.Keys.Contains(typeChar)).ToList(); // Materialize the list

					// If no matching bins found for this type character, skip
					if (!matchedBins.Any())
					{
						continue;
					}

					// Create a BinDay for this specific date and the matched bins
					var binDay = new BinDay()
					{
						Date = collectionDate,
						Address = address,
						// Ensure we create a new read-only list for this BinDay
						Bins = matchedBins.AsReadOnly()
					};

					binDays.Add(binDay);
				}

				// Filter out bin days in the past
				binDays = [.. ProcessingUtilities.GetFutureBinDays(binDays)];

				// Merge bin days that fall on the same date
				binDays = [.. ProcessingUtilities.MergeBinDays(binDays)];

				// Order the final list by date
				binDays = binDays.OrderBy(bd => bd.Date).ToList();

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					// Assign the processed, read-only list
					BinDays = binDays.AsReadOnly(),
					NextClientSideRequest = null
				};

				return getBinDaysResponse;
			}

			// Throw exception for invalid request
			throw new InvalidOperationException("Invalid client-side request.");
		}
	}
}
