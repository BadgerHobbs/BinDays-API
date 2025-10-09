namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Globalization;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Collector implementation for Oxford City Council.
	/// </summary>
	internal sealed partial class OxfordCityCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "Oxford City Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://www.oxford.gov.uk/mybinday");

		/// <inheritdoc/>
		public override string GovUkId => "oxford";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly ReadOnlyCollection<Bin> binTypes = new List<Bin>()
		{
			new()
			{
				Name = "Rubbish",
				Colour = BinColor.Green,
				Keys = new List<string>() { "residual" }.AsReadOnly(),
			},
			new()
			{
				Name = "Mixed Recycling",
				Colour = BinColor.Blue,
				Keys = new List<string>() { "recycling" }.AsReadOnly(),
			},
			new()
			{
				Name = "Garden Waste",
				Colour = BinColor.Brown,
				Keys = new List<string>() { "garden" }.AsReadOnly(),
			},
			new()
			{
				Name = "Food Waste",
				Colour = BinColor.Green,
				Keys = new List<string>() { "food" }.AsReadOnly(),
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
		/// Regex for the bin days from the page elements.
		/// </summary>
		[GeneratedRegex(@"<p>\s*<img alt=""(?<binType>[^""]+)""[^>]*>\s*(?:Your next [^:]+ collections:\s*(?<collectionDates>[^<]+)|(?<collectionDates>No [^<]+? Collections at this Property))<\/p>")]
		private static partial Regex BinDaysRegex();

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
					Url = "https://www.oxford.gov.uk/xfp/form/142",
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
					{"page", "12"},
					{"locale", "en_GB"},
					{"injectedParams", "{'formID': '142'}"},
					{"q6ad4e3bf432c83230a0347a6eea6c805c672efeb_0_0", postcode},
					{"callback", "{ 'action': 'ic', 'element': 'q6ad4e3bf432c83230a0347a6eea6c805c672efeb', 'data': 0, 'tableRow': -1 }"},
				});

				var requestHeaders = new Dictionary<string, string>() {
					{"user-agent", Constants.UserAgent},
					{"content-type", "application/x-www-form-urlencoded"},
				};

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 2,
					Url = "https://www.oxford.gov.uk/xfp/form/142",
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
					Url = "https://www.oxford.gov.uk/xfp/form/142",
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
					{"page", "12"},
					{"locale", "en_GB"},
					{"q6ad4e3bf432c83230a0347a6eea6c805c672efeb_0_0", address.Postcode!},
					{"q6ad4e3bf432c83230a0347a6eea6c805c672efeb_1_0", address.Uid!},
					{"next", "Next"},
				});

				var requestHeaders = new Dictionary<string, string>() {
					{"user-agent", Constants.UserAgent},
					{"content-type", "application/x-www-form-urlencoded"},
				};

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 2,
					Url = "https://www.oxford.gov.uk/xfp/form/142",
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
					var binType = rawBinDay.Groups["binType"].Value;
					var collectionDates = rawBinDay.Groups["collectionDates"].Value
						.Split(",")
						.Select(x => x.Trim());

					// Skip if there are no collection dates (e.g. 'No Garden Collections at this Property')
					if (collectionDates.Any(x => x.StartsWith("No ")))
					{
						continue;
					}

					foreach (var collectionDate in collectionDates)
					{
						// Parse the date (e.g. 'Thursday 19 June 2025' or 'Thursday 04 Sep 2025')
						var date = DateOnly.ParseExact(
							collectionDate,
							["dddd dd MMMM yyyy", "dddd dd MMM yyyy"],
							CultureInfo.InvariantCulture,
							DateTimeStyles.None
						);

						// Get matching bin types from the type using the keys
						var matchedBinTypes = binTypes.Where(x =>
							x.Keys.Any(y =>
								binType.Contains(y, StringComparison.CurrentCultureIgnoreCase)
							)
						);

						var binDay = new BinDay()
						{
							Date = date,
							Address = address,
							Bins = matchedBinTypes.ToList().AsReadOnly()
						};

						binDays.Add(binDay);
					}
				}

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					BinDays = ProcessingUtilities.ProcessBinDays(binDays),
					NextClientSideRequest = null
				};

				return getBinDaysResponse;
			}

			// Throw exception for invalid request
			throw new InvalidOperationException("Invalid client-side request.");
		}
	}
}
