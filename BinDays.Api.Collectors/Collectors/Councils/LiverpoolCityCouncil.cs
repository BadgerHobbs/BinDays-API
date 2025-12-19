namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Collectors.Vendors;
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Text.Json;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Collector implementation for Liverpool City Council.
	/// </summary>
	internal sealed partial class LiverpoolCityCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "Liverpool City Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://www.template.gov.uk/");

		/// <inheritdoc/>
		public override string GovUkId => "liverpool";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly IReadOnlyCollection<Bin> _binTypes = new List<Bin>()
		{
			new()
			{
				Name = "Recycling",
				Colour = BinColour.Blue,
				Keys = new List<string>() { "Recycling" }.AsReadOnly(),
			},
			new()
			{
				Name = "Garden Waste",
				Colour = BinColour.Green,
				Keys = new List<string>() { "Green" }.AsReadOnly(),
			},
			new()
			{
				Name = "Household Waste",
				Colour = BinColour.Purple,
				Keys = new List<string>() { "Refuse" }.AsReadOnly(),
			},
		}.AsReadOnly();

		/// <summary>
		/// Regex for the bin collections from the data table elements.
		/// </summary>
		[GeneratedRegex(@"<tr\s*>\s*<th\s+scope\s*=\s*""row""\s*>\s*<span[^>]*>\s*(?<binType>[^<]+?)\s*</span>\s*</th>\s*(?<datesHtml>(?:<td[^>]*>\s*<div[^>]*>\s*.*?s*</div>\s*</td>\s*)+)\s*</tr>")]
		private static partial Regex BinCollectionsRegex();

		/// <summary>
		/// Regex for the bin dates from the collection string.
		/// </summary>
		[GeneratedRegex(@"<div class=""bindate"">(?<date>.*?)</div>")]
		private static partial Regex BinDatesRegex();

		/// <summary>
		/// Regex for removing the st|nd|rd|th from the date part
		/// </summary>
		[GeneratedRegex(@"(?<=\d)(st|nd|rd|th)")]
		private static partial Regex CollectionDateRegex();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting addresses
			if (clientSideResponse == null)
			{
				var requestUrl = $"https://liverpool.gov.uk/Address/getAddressesByPostcode/?postcode={postcode}";

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
				using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);

				// Iterate through each address json, and create a new address object
				var addresses = new List<Address>();
				foreach (var addressElement in jsonDoc.RootElement.EnumerateArray())
				{
					string? property = addressElement.GetProperty("addressLines").GetString();
					string? uprn = addressElement.GetProperty("uprn").GetString();

					var address = new Address
					{
						Property = property?.Trim(),
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
			// Prepare client-side request for getting bin days
			if (clientSideResponse == null)
			{
				var requestUrl = $"https://liverpool.gov.uk/Bins/BinDatesTable?UPRN={address.Uid}&HideGreenBin=False";

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
				// Get bin collections from response
				var rawBinCollections = BinCollectionsRegex().Matches(clientSideResponse.Content);

				// Iterate through each bin collection, and create a new bin day object
				var binDays = new List<BinDay>();
				foreach (Match rawBinCollection in rawBinCollections)
				{
					var binType = rawBinCollection.Groups["binType"].Value;
					var datesHtml = rawBinCollection.Groups["datesHtml"].Value;

					// Get bin dates from the collection
					var rawBinDates = BinDatesRegex().Matches(datesHtml);

					// Iterate through each bin date, and create a new bin day object
					foreach (Match rawBinDate in rawBinDates)
					{
						var dateString = rawBinDate.Groups["date"].Value;

						// Strip the st|nd|rd|th and remove day of the week
						dateString = CollectionDateRegex().Replace(dateString, "").Split(",").Last().Trim();

						// Handle a date of 'Today'
						if (dateString == "Today")
						{
							dateString = DateTime.Now.ToString("d MMMM");
						}
						// Handle a date of "Tomorrow"
						else if (dateString == "Tomorrow")
						{
							dateString = DateTime.Now.AddDays(1).ToString("d MMMM");
						}

						// Parse the date
						var date = dateString.ParseDateInferringYear("d MMMM");

						// Get matching bin types from the type using the keys
						var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, binType);

						var binDay = new BinDay
						{
							Date = date,
							Address = address,
							Bins = matchedBinTypes,
						};

						binDays.Add(binDay);
					}
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
