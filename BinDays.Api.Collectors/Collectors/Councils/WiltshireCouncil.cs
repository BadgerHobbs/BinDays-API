namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Collectors.Vendors;
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Globalization;
	using System.Text.Json;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Collector implementation for Wiltshire Council.
	/// </summary>
	internal sealed partial class WiltshireCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "Wiltshire Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://ilforms.wiltshire.gov.uk/WasteCollectionDays/");

		/// <inheritdoc/>
		public override string GovUkId => "wiltshire";

		/// <summary>
		/// Regex to extract the JSON model data from the script tag in the HTML response.
		/// It captures the JSON object assigned to the 'modelData' variable.
		/// </summary>
		[GeneratedRegex("modelData = (\\{.*?\\});", RegexOptions.Singleline)]
		private static partial Regex ModelDataRegex();

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly IReadOnlyCollection<Bin> _binTypes = new List<Bin>()
		{
			new()
			{
				Name = "Household Waste",
				Colour = BinColour.Black,
				Keys = new List<string>() { "Household waste" }.AsReadOnly(),
			},
			new()
			{
				Name = "Mixed Dry Recycling",
				Colour = BinColour.Blue,
				Keys = new List<string>() { "Mixed dry recycling" }.AsReadOnly(),
			},
			new()
			{
				Name = "Garden Waste",
				Colour = BinColour.Green,
				Keys = new List<string>() { "Garden" }.AsReadOnly(),
			},
			new()
			{
				Name = "Glass Recycling",
				Colour = BinColour.Black,
				Keys = new List<string>() { "black box" }.AsReadOnly(),
				Type = BinType.Box,
			},
		}.AsReadOnly();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting addresses
			if (clientSideResponse == null)
			{
				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
				{
					{"Postcode", postcode},
					{"Uprn", string.Empty},
					{"Month", DateTime.Now.Month.ToString()},
					{"Year", DateTime.Now.Year.ToString()},
				});

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = "https://ilforms.wiltshire.gov.uk/WasteCollectionDays/AddressList",
					Method = "POST",
					Headers = new() {
						{"content-type", "application/x-www-form-urlencoded; charset=UTF-8"},
					},
					Body = requestBody,
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
				var addresses = new List<Address>();

				// Parse response content as JSON object
				using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
				var rawAddresses = jsonDoc.RootElement.GetProperty("Model").GetProperty("PostcodeAddresses");

				// Iterate through each address json, and create a new address object
				foreach (var addressElement in rawAddresses.EnumerateArray())
				{
					string? property = addressElement.GetProperty("PropertyNameAndNumber").GetString();
					string? street = addressElement.GetProperty("Street").GetString();
					string? town = addressElement.GetProperty("Town").GetString();
					string? uprn = addressElement.GetProperty("UPRN").GetString();

					var address = new Address
					{
						Property = property?.Trim(),
						Street = street?.Trim(),
						Town = town?.Trim(),
						Postcode = postcode,
						Uid = uprn,
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
			// Prepare client-side request for getting current month's bin days
			if (clientSideResponse == null)
			{
				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
				{
					{"Postcode", address.Postcode!},
					{"Uprn", address.Uid!},
					{"Month", DateTime.Now.Month.ToString()},
					{"Year", DateTime.Now.Year.ToString()},
				});

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = "https://ilforms.wiltshire.gov.uk/WasteCollectionDays/CollectionList",
					Method = "POST",
					Headers = new() {
						{"content-type", "application/x-www-form-urlencoded; charset=UTF-8"},
					},
					Body = requestBody,
				};

				var getBinDaysResponse = new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest
				};

				return getBinDaysResponse;
			}
			// Process current month's response. If no future data, request next month.
			else if (clientSideResponse.RequestId == 1)
			{
				// Process the response content from the current month's response
				var currentMonthBinDays = ParseBinDays(clientSideResponse.Content, address);

				// If future collections were found in the current month, return them
				if (currentMonthBinDays.Count > 0)
				{
					var getBinDaysResponse = new GetBinDaysResponse
					{
						BinDays = currentMonthBinDays,
					};
					return getBinDaysResponse;
				}
				// If no future collections found, prepare request for the next month
				else
				{
					var nextMonthDate = DateTime.Now.AddMonths(1);
					var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
					{
						{"Postcode", address.Postcode!},
						{"Uprn", address.Uid!},
						{"Month", nextMonthDate.Month.ToString()},
						{"Year", nextMonthDate.Year.ToString()},
					});

					var clientSideRequest = new ClientSideRequest
					{
						RequestId = 2,
						Url = "https://ilforms.wiltshire.gov.uk/WasteCollectionDays/CollectionList",
						Method = "POST",
						Headers = new() {
							{"content-type", "application/x-www-form-urlencoded; charset=UTF-8"},
						},
						Body = requestBody,
					};

					var getBinDaysResponse = new GetBinDaysResponse
					{
						NextClientSideRequest = clientSideRequest
					};
					return getBinDaysResponse;
				}
			}
			// Process next month's response
			else if (clientSideResponse.RequestId == 2)
			{
				// Process the response content from the next month's response
				var nextMonthBinDays = ParseBinDays(clientSideResponse.Content, address);

				var getBinDaysResponse = new GetBinDaysResponse
				{
					BinDays = nextMonthBinDays,
				};

				return getBinDaysResponse;
			}

			// Throw exception for invalid request
			throw new InvalidOperationException("Invalid client-side request.");
		}

		/// <summary>
		/// Parses bin day information from the JSON model in the provided response content.
		/// </summary>
		/// <param name="responseContent">The string containing the response data, including the model JSON.</param>
		/// <param name="address">The address associated with these bin days.</param>
		/// <returns>A read-only collection of future BinDay objects parsed from the JSON.</returns>
		private ReadOnlyCollection<BinDay> ParseBinDays(string responseContent, Address address)
		{
			var binDays = new List<BinDay>();

			// Extract the JSON string from the response content using regex
			var jsonMatch = ModelDataRegex().Match(responseContent);
			var jsonContent = jsonMatch.Groups[1].Value;

			// Parse the JSON and get the collection dates array
			using var jsonDoc = JsonDocument.Parse(jsonContent);
			var collectionDates = jsonDoc.RootElement.GetProperty("MonthCollectionDates");

			foreach (var collection in collectionDates.EnumerateArray())
			{
				var rawBinType = collection.GetProperty("RoundTypeName").GetString()!;
				var rawBinDayDate = collection.GetProperty("DateString").GetString()!;

				// Parsing the date string (e.g. "7/3/2025 12:00:00 AM")
				var date = DateOnly.FromDateTime(
					DateTime.Parse(rawBinDayDate, CultureInfo.InvariantCulture)
				);

				// Find matching bin types based on the description (case-insensitive)
				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, rawBinType);

				foreach (var binType in matchedBins)
				{
					var binDay = new BinDay
					{
						Date = date,
						Address = address,
						Bins = new List<Bin>() { binType }.AsReadOnly()
					};
					binDays.Add(binDay);
				}
			}

			return ProcessingUtilities.ProcessBinDays(binDays);
		}
	}
}
