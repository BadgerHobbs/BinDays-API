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
		private readonly ReadOnlyCollection<Bin> binTypes = new List<Bin>()
		{
			new()
			{
				Name = "Recycling",
				Colour = "Blue",
				Keys = new List<string>() { "Recycling" }.AsReadOnly(),
			},
			new()
			{
				Name = "Garden Waste",
				Colour = "Green",
				Keys = new List<string>() { "Green" }.AsReadOnly(),
			},
			new()
			{
				Name = "Household Waste",
				Colour = "Purple",
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
				// Parse response content as JSON array
				using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);

				// Iterate through each address json, and create a new address object
				var addresses = new List<Address>();
				foreach (var addressElement in jsonDoc.RootElement.EnumerateArray())
				{
					string? property = addressElement.GetProperty("addressLines").GetString();
					string? uprn = addressElement.GetProperty("uprn").GetString();

					var address = new Address()
					{
						Property = property?.Trim(),
						Street = null,
						Town = null,
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
				var requestUrl = $"https://liverpool.gov.uk/Bins/BinDatesTable?UPRN={address.Uid}&HideGreenBin=False";

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

						// Strip the st|nd|rd|th from the date string
						dateString = CollectionDateRegex().Replace(dateString, "");

						// Handle a date of 'Today'
						if (dateString == "Today")
						{
							dateString = DateTime.Now.ToString("dddd, d MMMM");
						}
						// Handle a date of "Tomorrow"
						else if (dateString == "Tomorrow")
						{
							dateString = DateTime.Now.AddDays(1).ToString("dddd, d MMMM");
						}

						// Parse the date
						var date = DateOnly.ParseExact(
							dateString,
							"dddd, d MMMM",
							CultureInfo.InvariantCulture,
							DateTimeStyles.None
						);

						// Get matching bin types from the type using the keys
						var matchedBinTypes = binTypes.Where(x => x.Keys.Any(y => binType.Contains(y)));

						var binDay = new BinDay()
						{
							Date = date,
							Address = address,
							Bins = matchedBinTypes.ToList().AsReadOnly()
						};

						binDays.Add(binDay);
					}
				}

				// Filter out bin days in the past
				binDays = [.. ProcessingUtilities.GetFutureBinDays(binDays)];

				// Merge the bin days
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
