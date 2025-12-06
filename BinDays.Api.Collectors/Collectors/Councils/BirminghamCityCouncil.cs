namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Collectors.Vendors;
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Globalization;
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
		private readonly IReadOnlyCollection<Bin> _binTypes = new List<Bin>()
		{
			new()
			{
				Name = "Rubbish",
				Colour = BinColour.Grey,
				Keys = new List<string>() { "Household Collection" }.AsReadOnly(),
			},
			new()
			{
				Name = "Recycling",
				Colour = BinColour.Blue,
				Keys = new List<string>() { "Recycling Collection" }.AsReadOnly(),
			},
			new()
			{
				Name = "Garden Waste",
				Colour = BinColour.Green,
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
		[GeneratedRegex(@"<tbody>\s*<tr>\s*<t.>(?<service>.*?)</t.>\s*<td>(?<date>.*?)</td>\s*</tr>\s*</tbody>")]
		private static partial Regex BinDaysRegex();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting token
			if (clientSideResponse == null)
			{
				// Prepare client-side request
				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = "https://www.birmingham.gov.uk/xfp/form/619",
					Method = "GET",
					Headers = new() {
						{"user-agent", Constants.UserAgent},
					},
				};

				var getAddressesResponse = new GetAddressesResponse
				{
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
				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
				{
					{"__token", token},
					{"page", "491"},
					{"locale", "en_GB"},
					{"injectedParams", "{'formID': '619'}"},
					{"q1f8ccce1d1e2f58649b4069712be6879a839233f_0_0", postcode},
					{"callback", "{ 'action': 'ic', 'element': 'q1f8ccce1d1e2f58649b4069712be6879a839233f', 'data': 0, 'tableRow': -1 }"},
				});

				var requestHeaders = new Dictionary<string, string> {
					{"user-agent", Constants.UserAgent},
					{"content-type", "application/x-www-form-urlencoded"},
				};

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 2,
					Url = "https://www.birmingham.gov.uk/xfp/form/619",
					Method = "POST",
					Headers = requestHeaders,
					Body = requestBody,
				};

				var getAddressesResponse = new GetAddressesResponse
				{
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

					var address = new Address
					{
						Property = rawAddress.Groups["address"].Value,
						Postcode = postcode,
						Uid = uid,
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
			// Prepare client-side request for getting token
			if (clientSideResponse == null)
			{
				// Prepare client-side request
				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = "https://www.birmingham.gov.uk/xfp/form/619",
					Method = "GET",
					Headers = new() {
						{"user-agent", Constants.UserAgent},
					},
				};

				var getBinDaysResponse = new GetBinDaysResponse
				{
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
				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
				{
					{"__token", token},
					{"page", "491"},
					{"locale", "en_GB"},
					{"injectedParams", "{'formID': '619'}"},
					{"q1f8ccce1d1e2f58649b4069712be6879a839233f_0_0", address.Postcode!},
					{"q1f8ccce1d1e2f58649b4069712be6879a839233f_1_0", address.Uid!},
					{"next", "Next"},
				});

				var requestHeaders = new Dictionary<string, string> {
					{"user-agent", Constants.UserAgent},
					{"content-type", "application/x-www-form-urlencoded"},
				};

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 2,
					Url = "https://www.birmingham.gov.uk/xfp/form/619",
					Method = "POST",
					Headers = requestHeaders,
					Body = requestBody,
				};

				var getBinDaysResponse = new GetBinDaysResponse
				{
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

					var date = DateOnly.ParseExact(
						collectionDate,
						"ddd dd/MM/yyyy",
						CultureInfo.InvariantCulture,
						DateTimeStyles.None
					);

					// Get matching bin types from the service using the keys
					var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, service);

					var binDay = new BinDay
					{
						Date = date,
						Address = address,
						Bins = matchedBinTypes,
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
