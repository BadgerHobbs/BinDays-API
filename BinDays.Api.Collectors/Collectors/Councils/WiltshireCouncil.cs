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
		/// Regex to extract bin collection event details from HTML.
		/// It captures the bin type description from 'data-original-title' and the date string from 'data-original-datetext'.
		/// </summary>
		[GeneratedRegex("<a[^>]*?data-original-title=\"(.*?)\"[^>]*?data-original-datetext=\"(.*?)\"[^>]*?>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
		private static partial Regex CollectionEventRegex();

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly ReadOnlyCollection<Bin> binTypes = new List<Bin>()
		{
			new()
			{
				Name = "Household Waste",
				Colour = "Black",
				Keys = new List<string>() { "Household waste" }.AsReadOnly(),
			},
			new()
			{
				Name = "Mixed Dry Recycling",
				Colour = "Blue",
				Keys = new List<string>() { "Mixed dry recycling" }.AsReadOnly(),
			},
			new()
			{
				Name = "Garden Waste",
				Colour = "Green",
				Keys = new List<string>() { "Garden" }.AsReadOnly(),
			},
			new()
			{
				Name = "Glass Recycling",
				Colour = "Black",
				Keys = new List<string>() { "black box" }.AsReadOnly(),
				Type = "Box",
			},
		}.AsReadOnly();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting addresses
			if (clientSideResponse == null)
			{
				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new Dictionary<string, string>()
				{
					{"Postcode", postcode},
					{"Uprn", string.Empty},
					{"Month", DateTime.Now.Month.ToString()},
					{"Year", DateTime.Now.Year.ToString()},
				});

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = "https://ilforms.wiltshire.gov.uk/WasteCollectionDays/AddressList",
					Method = "POST",
					Headers = new Dictionary<string, string>() {
						{"content-type", "application/x-www-form-urlencoded; charset=UTF-8"},
					},
					Body = requestBody,
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
			// Prepare client-side request for getting current month's bin days
			if (clientSideResponse == null)
			{
				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new Dictionary<string, string>()
				{
					{"Postcode", address.Postcode!},
					{"Uprn", address.Uid!},
					{"Month", DateTime.Now.Month.ToString()},
					{"Year", DateTime.Now.Year.ToString()},
				});

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = "https://ilforms.wiltshire.gov.uk/WasteCollectionDays/CollectionList",
					Method = "POST",
					Headers = new Dictionary<string, string>() {
						{"content-type", "application/x-www-form-urlencoded; charset=UTF-8"},
					},
					Body = requestBody,
				};

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					BinDays = null,
					NextClientSideRequest = clientSideRequest
				};

				return getBinDaysResponse;
			}
			// Process current month's response. If no future data, request next month.
			else if (clientSideResponse.RequestId == 1)
			{
				// Process the HTML content from the current month's response
				var currentMonthBinDays = ParseBinDaysFromHtml(clientSideResponse.Content, address);

				// If future collections were found in the current month, return them
				if (currentMonthBinDays.Count > 0)
				{
					var getBinDaysResponse = new GetBinDaysResponse()
					{
						BinDays = currentMonthBinDays,
						NextClientSideRequest = null // No further requests needed
					};
					return getBinDaysResponse;
				}
				// If no future collections found, prepare request for the next month
				else
				{
					var nextMonthDate = DateTime.Now.AddMonths(1);
					var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new Dictionary<string, string>()
					{
						{"Postcode", address.Postcode!},
						{"Uprn", address.Uid!},
						{"Month", nextMonthDate.Month.ToString()},
						{"Year", nextMonthDate.Year.ToString()},
					});

					var clientSideRequest = new ClientSideRequest()
					{
						RequestId = 2, // Set ID for the next step
						Url = "https://ilforms.wiltshire.gov.uk/WasteCollectionDays/CollectionList",
						Method = "POST",
						Headers = new Dictionary<string, string>() {
							{"content-type", "application/x-www-form-urlencoded; charset=UTF-8"},
						},
						Body = requestBody,
					};

					var getBinDaysResponse = new GetBinDaysResponse()
					{
						BinDays = null,
						NextClientSideRequest = clientSideRequest
					};
					return getBinDaysResponse;
				}
			}
			// Process next month's response
			else if (clientSideResponse.RequestId == 2)
			{
				// Process the HTML content from the next month's response
				var nextMonthBinDays = ParseBinDaysFromHtml(clientSideResponse.Content, address);

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					BinDays = nextMonthBinDays,
					NextClientSideRequest = null
				};

				return getBinDaysResponse;
			}

			// Throw exception for invalid request
			throw new InvalidOperationException("Invalid client-side request.");
		}

		/// <summary>
		/// Parses bin day information from the provided HTML content, filtering for future dates.
		/// </summary>
		/// <param name="htmlContent">The HTML string containing collection data.</param>
		/// <param name="address">The address associated with these bin days.</param>
		/// <returns>A read-only collection of future BinDay objects parsed from the HTML.</returns>
		private ReadOnlyCollection<BinDay> ParseBinDaysFromHtml(string htmlContent, Address address)
		{
			var binDays = new List<BinDay>();
			var today = DateOnly.FromDateTime(DateTime.Today);

			// Find all collection event details using regex
			var matches = CollectionEventRegex().Matches(htmlContent);

			foreach (Match match in matches.Cast<Match>())
			{
				if (match.Groups.Count == 3)
				{
					var rawBinType = match.Groups[1].Value.Trim();
					var rawBinDayDate = match.Groups[2].Value.Trim();

					// Try parsing the date string (e.g., "Wednesday 16 April, 2025")
					if (!DateTime.TryParseExact(rawBinDayDate, "dddd d MMMM, yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDateTime))
					{
						continue; // Skip if date parsing fails
					}
					var collectionDate = DateOnly.FromDateTime(parsedDateTime);

					// Only process dates that are today or in the future
					if (collectionDate < today)
					{
						continue;
					}

					// Find matching bin types based on the description (case-insensitive)
					var matchedBins = binTypes.Where(bin =>
						bin.Keys.Any(key => rawBinType.Contains(key, StringComparison.OrdinalIgnoreCase)));

					// Create a BinDay for each matched bin type
					foreach (var binType in matchedBins)
					{
						var binDay = new BinDay()
						{
							Date = collectionDate,
							Address = address,
							// Create a read-only list containing just this one bin
							Bins = new List<Bin>() { binType }.AsReadOnly()
						};
						binDays.Add(binDay);
					}
				}
			}

			// Filter out any bin days that might still be in the past (redundant due to check above, but safe)
			// Note: ProcessingUtilities.GetFutureBinDays expects IEnumerable<BinDay>
			var futureBinDays = ProcessingUtilities.GetFutureBinDays(binDays);

			// Merge bin days that occur on the same date
			// Note: ProcessingUtilities.MergeBinDays expects IEnumerable<BinDay>
			var mergedBinDays = ProcessingUtilities.MergeBinDays(futureBinDays);

			// Return the final list, ordered by date
			return mergedBinDays.OrderBy(bd => bd.Date).ToList().AsReadOnly();
		}
	}
}
