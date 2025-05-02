namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Collector implementation for Birmingham City Council.
	/// </summary>
	internal sealed partial class BirminghamCityCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "Birmingham City Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://www.birmingham.gov.uk/xfp/form/619");

		/// <inheritdoc/>
		public override string GovUkId => "birmingham";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly ReadOnlyCollection<Bin> binTypes = new List<Bin>()
		{
			new()
			{
				Name = "Rubbish",
				Colour = "Grey",
				Keys = new List<string>() { "Household Collection" }.AsReadOnly(),
			},
			new()
			{
				Name = "Recycling",
				Colour = "Blue",
				Keys = new List<string>() { "Recycling Collection" }.AsReadOnly(),
			},
			new()
			{
				Name = "Garden Waste",
				Colour = "Green",
				Keys = new List<string>() { "Green Recycling" }.AsReadOnly(),
			},
		}.AsReadOnly();

		/// <summary>
		/// Regex for the session token value from an input field.
		/// </summary>
		[GeneratedRegex(@"name=""__token"" value=""([^""]+)""")]
		private static partial Regex TokenRegex();

		/// <summary>
		/// Regex for the addresses from the options elements.
		/// </summary>
		[GeneratedRegex(@"<option\s+value=""(?<uid>\d+)""[^>]*>\s*(?<address>.*?)\s*</option>")]
		private static partial Regex AddressRegex();

		/// <summary>
		/// Regex for the bin days from the data table elements.
		/// </summary>
		[GeneratedRegex(@"<tr>\s*<td>(?<service>.*?)</td>\s*<td>(?<date>.*?)</td>\s*</tr>")]
		private static partial Regex BinDaysRegex();

		/// <summary>
		/// Regex for the collection date format.
		/// </summary>
		[GeneratedRegex(@"\((?<day>\d+)(?:st|nd|rd|th)\)")]
		private static partial Regex CollectionDateRegex();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting token
			if (clientSideResponse == null)
			{
				// Prepare client-side request
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = "https://www.birmingham.gov.uk/xfp/form/619",
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
			// Prepare client-side request for getting addresses
			else if (clientSideResponse.RequestId == 1)
			{
				// Get token from response
				var token = TokenRegex().Match(clientSideResponse.Content).Groups[1].Value;

				// Prepare client-side request
				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new Dictionary<string, string>()
				{
					{"__token", token},
					{"page", "491"},
					{"locale", "en_GB"},
					{"injectedParams", "{'formID': '619'}"},
					{"q1f8ccce1d1e2f58649b4069712be6879a839233f_0_0", postcode},
					{"callback", "{ 'action': 'ic', 'element': 'q1f8ccce1d1e2f58649b4069712be6879a839233f', 'data': 0, 'tableRow': -1 }"},
				});

				var requestHeaders = new Dictionary<string, string>() {
					{"user-agent", Constants.UserAgent},
					{"content-type", "application/x-www-form-urlencoded"},
				};

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 2,
					Url = "https://www.birmingham.gov.uk/xfp/form/619",
					Method = "POST",
					Headers = requestHeaders,
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
			else if (clientSideResponse.RequestId == 2)
			{
				// Get addresses from response
				var rawAddresses = AddressRegex().Matches(clientSideResponse.Content)!;

				// Iterate through each address, and create a new address object
				var addresses = new List<Address>();
				foreach (Match rawAddress in rawAddresses)
				{
					var uid = rawAddress.Groups["uid"].Value;

					// Exclude placeholder/invalid options
					if (uid == "-1" || uid == "111111")
					{
						continue;
					}

					var address = new Address()
					{
						Property = rawAddress.Groups["address"].Value,
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
			// Prepare client-side request for getting token
			if (clientSideResponse == null)
			{
				// Prepare client-side request
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = "https://www.birmingham.gov.uk/xfp/form/619",
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
			// Prepare client-side request for getting bin days
			else if (clientSideResponse.RequestId == 1)
			{
				// Get token from response
				var token = TokenRegex().Match(clientSideResponse.Content).Groups[1].Value;

				// Prepare client-side request
				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new Dictionary<string, string>()
				{
					{"__token", token},
					{"page", "491"},
					{"locale", "en_GB"},
					{"injectedParams", "{'formID': '619'}"},
					{"q1f8ccce1d1e2f58649b4069712be6879a839233f_0_0", address.Postcode!},
					{"q1f8ccce1d1e2f58649b4069712be6879a839233f_1_0", address.Uid!},
					{"next", "Next"},
				});

				var requestHeaders = new Dictionary<string, string>() {
					{"user-agent", Constants.UserAgent},
					{"content-type", "application/x-www-form-urlencoded"},
				};

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 2,
					Url = "https://www.birmingham.gov.uk/xfp/form/619",
					Method = "POST",
					Headers = requestHeaders,
					Body = requestBody,
				};

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					BinDays = null,
					NextClientSideRequest = clientSideRequest
				};

				return getBinDaysResponse;
			}
			// Process bin days from response
			else if (clientSideResponse.RequestId == 2)
			{
				// Get bin days from response
				var rawBinDays = BinDaysRegex().Matches(clientSideResponse.Content)!;

				// Iterate through each bin day, and create a new bin day object
				var binDays = new List<BinDay>();
				foreach (Match rawBinDay in rawBinDays)
				{
					var service = rawBinDay.Groups["service"].Value;
					var collectionDate = rawBinDay.Groups["date"].Value;

					// Get the day number from the collection date
					var dayNumber = int.Parse(CollectionDateRegex().Match(collectionDate).Groups["day"].Value);

					var date = new DateOnly(
						DateTime.Now.Year,
						DateTime.Now.Month,
						dayNumber
					);

					// If the day number is less then today, it is next month
					if (dayNumber < DateTime.Now.Day)
					{
						date = date.AddMonths(1);
					}

					// Get matching bin types from the service using the keys
					var matchedBinTypes = binTypes.Where(x => x.Keys.Any(y => service.Contains(y)));

					var binDay = new BinDay()
					{
						Date = date,
						Address = address,
						Bins = matchedBinTypes.ToList().AsReadOnly()
					};

					binDays.Add(binDay);
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
