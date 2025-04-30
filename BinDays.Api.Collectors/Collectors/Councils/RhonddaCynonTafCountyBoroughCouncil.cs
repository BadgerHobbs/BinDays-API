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
	using System.Text.RegularExpressions;

	/// <summary>
	/// Collector implementation for Rhondda Cynon Taf County Borough Council.
	/// </summary>
	internal sealed partial class RhonddaCynonTafCountyBoroughCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "Rhondda Cynon Taf County Borough Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://www.rctcbc.gov.uk/EN/Resident/RecyclingandWaste/BinCollectionDays.aspx");

		/// <inheritdoc/>
		public override string GovUkId => "rhondda-cynon-taff";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly ReadOnlyCollection<Bin> binTypes = new List<Bin>()
		{
			new()
			{
				Name = "Recycling",
				Colour = "White",
				Keys = new List<string>() { "recycling" }.AsReadOnly(),
			},
			new()
			{
				Name = "Food Waste",
				Colour = "Light Green",
				Keys = new List<string>() { "food waste" }.AsReadOnly(),
			},
			new()
			{
				Name = "Green Waste",
				Colour = "Green",
				Keys = new List<string>() { "green waste", "garden waste" }.AsReadOnly(),
			},
			new()
			{
				Name = "General Waste",
				Colour = "Black",
				Keys = new List<string>() { "black bags" }.AsReadOnly(),
			},
		}.AsReadOnly();

		/// <summary>
		/// Regex for the addresses from the options elements.
		/// </summary>
		[GeneratedRegex(@"<option\s+value=""(?<uid>[^""]+)""[^>]*>\s*(?<address>.*?)\s*</option>")]
		private static partial Regex AddressRegex();

		/// <summary>
		/// Regex for extracting bin type and date string from the next collection table rows.
		/// </summary>
		[GeneratedRegex(@"<tr>\s*<td[^>]*><a[^>]*>(?<bintype>[^<]+)</a></td><td>(?<datestring>[^<]+)</td></tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
		private static partial Regex NextCollectionRowRegex();

		/// <summary>
		/// Regex for removing the st|nd|rd|th from the date part
		/// </summary>
		[GeneratedRegex(@"(?<=\d)(st|nd|rd|th)")]
		private static partial Regex CollectionDateRegex();

		/// <summary>
		/// Regex for removing double spaces
		/// </summary>
		[GeneratedRegex(@"\s{2,}")]
		private static partial Regex DoubleSpaceRegex();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting addresses
			if (clientSideResponse == null)
			{
				var requestUrl = $"https://www.rctcbc.gov.uk/EN/Resident/RecyclingandWaste/RecyclingandWasteCollectionDays.aspx?&Postcode={Uri.EscapeDataString(postcode)}";

				var requestHeaders = new Dictionary<string, string>() {
					{"user-agent", Constants.UserAgent},
				};

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = requestUrl,
					Method = "GET",
					Headers = requestHeaders,
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
				// Get addresses from response
				var rawAddresses = AddressRegex().Matches(clientSideResponse.Content)!;

				// Iterate through each address, and create a new address object
				var addresses = new List<Address>();
				foreach (Match rawAddress in rawAddresses)
				{
					var uid = rawAddress.Groups["uid"].Value;
					var fullAddress = rawAddress.Groups["address"].Value.Trim();

					var address = new Address()
					{
						Property = fullAddress,
						Street = string.Empty,
						Town = string.Empty,
						Postcode = postcode,
						Uid = uid,
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
				// Use live URL as per legacy implementation, not base website URL
				var requestUrl = $"https://live-rctcbc.cloud.contensis.com/EN/Resident/RecyclingandWaste/RecyclingandWasteCollectionDays.aspx?uprn={Uri.EscapeDataString(address.Uid!)}";

				var requestHeaders = new Dictionary<string, string>() {
					{"user-agent", Constants.UserAgent},
				};

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = requestUrl,
					Method = "GET",
					Headers = requestHeaders,
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
				var binDays = new List<BinDay>();
				var collectionRows = NextCollectionRowRegex().Matches(clientSideResponse.Content);

				foreach (Match row in collectionRows)
				{
					var binTypeText = row.Groups["bintype"].Value.Trim().ToLowerInvariant();
					var dateString = row.Groups["datestring"].Value.Trim();

					// Extract the date part (e.g., "Tuesday 29th of April 2025")
					// Assumes format like "x days' time, Day d[th] of Month yyyy" or just "Day d[th] of Month yyyy"
					var datePart = dateString.Split(',').Last().Trim();

					// Strip the st|nd|rd|th from the date part
					datePart = CollectionDateRegex().Replace(datePart, "");

					// Remove double spaces
					datePart = DoubleSpaceRegex().Replace(datePart, " ");

					// Parse the date
					var collectionDate = DateOnly.ParseExact(
						datePart,
						"dddd d 'of' MMMM yyyy",
						CultureInfo.InvariantCulture,
						DateTimeStyles.None
					);

					// Find matching bin types
					var matchedBins = this.binTypes
						.Where(bin => bin.Keys.Any(key => binTypeText.Contains(key.ToLowerInvariant())))
						.ToList();

					// If bins are matched, create or update BinDay entry
					if (matchedBins.Count > 0)
					{
						var binDay = new BinDay()
						{
							Date = collectionDate,
							Address = address,
							Bins = matchedBins.AsReadOnly()
						};
						binDays.Add(binDay);
					}
				}

				// Filter out bin days in the past
				binDays = [.. ProcessingUtilities.GetFutureBinDays(binDays)];

				// Merge bin days that fall on the same date
				binDays = [.. ProcessingUtilities.MergeBinDays(binDays)];

				var getBinDaysResponse = new GetBinDaysResponse()
				{
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
