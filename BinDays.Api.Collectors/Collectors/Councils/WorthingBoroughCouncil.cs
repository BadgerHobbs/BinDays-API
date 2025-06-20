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
	/// Collector implementation for Worthing Borough Council.
	/// </summary>
	internal sealed partial class WorthingBoroughCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "Worthing Borough Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://www.adur-worthing.gov.uk/bin-day/");

		/// <inheritdoc/>
		public override string GovUkId => "worthing";

		/// <summary>
		/// Regex for the addresses from the options elements.
		/// </summary>
		[GeneratedRegex(@"<option\s+value=""(?<uid>\d+)""[^>]*>\s*(?<address>.*?)\s*</option>")]
		private static partial Regex AddressRegex();

		/// <summary>
		/// Regex for the bin days from the table rows.
		/// </summary>
		[GeneratedRegex(@"(?s)<div class=""bin-collection-listing-row.*?<h2.*?>(?<collection>.*?)</h2>.*?<p><strong>Next collection.*?</strong>\s*(?<date>.*?)</p>")]
		private static partial Regex BinDaysRegex();

		/// <summary>
		/// Regex for removing the st|nd|rd|th from the date part
		/// </summary>
		[GeneratedRegex(@"(?<=\d)(st|nd|rd|th)")]
		private static partial Regex CollectionDateRegex();

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
				Name = "Rubbish",
				Colour = "Grey",
				Keys = new List<string>() { "General rubbish" }.AsReadOnly(),
			},
			new()
			{
				Name = "Garden",
				Colour = "Green",
				Keys = new List<string>() { "Garden" }.AsReadOnly(),
			},
		}.AsReadOnly();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting addresses
			if (clientSideResponse == null)
			{
				var requestUrl = $"https://www.adur-worthing.gov.uk/bin-day/?brlu-address-postcode={postcode}&return-url=%2Fbin-day%2F&action=search";

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = requestUrl,
					Method = "GET",
					Headers = new Dictionary<string, string>() {
						{"user-agent", Constants.UserAgent},
					},
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
				// Get addresses from response using regex
				var rawAddresses = AddressRegex().Matches(clientSideResponse.Content);

				var addresses = new List<Address>();
				foreach (Match rawAddress in rawAddresses)
				{
					var uid = rawAddress.Groups["uid"].Value;
					var addressText = rawAddress.Groups["address"].Value.Trim();

					var address = new Address()
					{
						Property = addressText,
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
				var requestUrl = $"https://www.adur-worthing.gov.uk/bin-day/?brlu-selected-address={address.Uid}&return-url=/bin-day/";

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = requestUrl,
					Method = "GET",
					Headers = new Dictionary<string, string>() {
						{"user-agent", Constants.UserAgent},
					},
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
				// Get bin days from response
				var rawBinDays = BinDaysRegex().Matches(clientSideResponse.Content);

				// Iterate through each bin day, and create a new bin day object
				var binDays = new List<BinDay>();
				foreach (Match rawBinDay in rawBinDays)
				{
					var collection = rawBinDay.Groups["collection"].Value;

					// Remove the st|nd|rd|th from the date part (e.g. '16th')
					var collectionDates = CollectionDateRegex()
						.Replace(rawBinDay.Groups["date"].Value, "")
						.Split(',');

					foreach (var collectionDate in collectionDates)
					{
						// Parse the date (e.g. 'Friday 16 May')
						var date = DateOnly.ParseExact(
							collectionDate.Trim(),
							"dddd d MMMM",
							CultureInfo.InvariantCulture,
							DateTimeStyles.None
						);

						// Get matching bin types from the collection using the keys
						var matchedBinTypes = binTypes.Where(x => x.Keys.Any(y => collection.Contains(y)));

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
