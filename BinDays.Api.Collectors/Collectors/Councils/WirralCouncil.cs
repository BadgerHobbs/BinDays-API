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
	/// Collector implementation for Wirral Council.
	/// </summary>
	internal sealed partial class WirralCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "Wirral Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://www.wirral.gov.uk/bins-and-recycling/bin-collection-dates");

		/// <inheritdoc/>
		public override string GovUkId => "wirral";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly IReadOnlyCollection<Bin> _binTypes = new List<Bin>()
		{
			new()
			{
				Name = "Non-recyclable waste",
				Colour = BinColour.Green,
				Keys = ["Green bin"],
			},
			new()
			{
				Name = "Recycling",
				Colour = BinColour.Grey,
				Keys = ["Grey bin"],
			},
			new()
			{
				Name = "Garden Waste",
				Colour = BinColour.Brown,
				Keys = ["Brown bin"],
			},
		}.AsReadOnly();

		/// <summary>
		/// Regex for the viewstate token values from input fields.
		/// </summary>
		[GeneratedRegex(@"<input[^>]*?(?:name|id)=[""']__VIEWSTATE[""'][^>]*?value=[""'](?<viewStateValue>[^""']*)[""'][^>]*?/?>")]
		private static partial Regex ViewStateTokenRegex();

		/// <summary>
		/// Regex for the event validation values from input fields.
		/// </summary>
		[GeneratedRegex(@"<input[^>]*?(?:name|id)=[""']__EVENTVALIDATION[""'][^>]*?value=[""'](?<viewStateValue>[^""']*)[""'][^>]*?/?>")]
		private static partial Regex EventValidationRegex();

		/// <summary>
		/// Regex for the addresses from the options elements.
		/// </summary>
		[GeneratedRegex(@"<option value=""(?<uid>\d+)"">(?<address>[^<]+)</option>")]
		private static partial Regex AddressesRegex();

		/// <summary>
		/// Regex for the bin days from the data elements.
		/// </summary>
		[GeneratedRegex(@"(?s)<h2>(?<binName>[^<]+)</h2>.*?<p><strong>(?<date>[^<]+)</strong>")]
		private static partial Regex BinDaysRegex();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting token
			if (clientSideResponse == null)
			{
				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = "https://www.wirral.gov.uk/bincal_dev/",
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
				var viewState = ViewStateTokenRegex().Match(clientSideResponse.Content).Groups["viewStateValue"].Value;
				var eventValidation = EventValidationRegex().Match(clientSideResponse.Content).Groups["viewStateValue"].Value;

				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
				{
					{"__VIEWSTATE", viewState},
					{"__EVENTVALIDATION", eventValidation},
					{"ctl00$MainContent$Postcode", postcode},
					{"ctl00$MainContent$LookupPostcode", "Go"},
				});

				var requestHeaders = new Dictionary<string, string> {
					{"user-agent", Constants.UserAgent},
					{"content-type", "application/x-www-form-urlencoded"},
				};

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 2,
					Url = "https://www.wirral.gov.uk/bincal_dev/",
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
				var rawAddresses = AddressesRegex().Matches(clientSideResponse.Content)!;
				var addresses = new List<Address>();

				foreach (Match rawAddress in rawAddresses)
				{
					addresses.Add(new Address
					{
						Property = rawAddress.Groups["address"].Value.Trim(),
						Postcode = postcode,
						Uid = rawAddress.Groups["uid"].Value,
					});
				}

				var getAddressesResponse = new GetAddressesResponse
				{
					Addresses = addresses.AsReadOnly(),
				};

				return getAddressesResponse;
			}

			throw new InvalidOperationException("Invalid client-side request.");
		}

		/// <inheritdoc/>
		public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting token
			if (clientSideResponse == null)
			{
				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = "https://www.wirral.gov.uk/bincal_dev/",
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
			// Prepare client-side request to get the address selection page
			else if (clientSideResponse.RequestId == 1)
			{
				var viewState = ViewStateTokenRegex().Match(clientSideResponse.Content).Groups["viewStateValue"].Value;
				var eventValidation = EventValidationRegex().Match(clientSideResponse.Content).Groups["viewStateValue"].Value;

				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
				{
					{"__VIEWSTATE", viewState},
					{"__EVENTVALIDATION", eventValidation},
					{"ctl00$MainContent$Postcode", address.Postcode!},
					{"ctl00$MainContent$LookupPostcode", "Go"},
				});

				var requestHeaders = new Dictionary<string, string> {
					{"user-agent", Constants.UserAgent},
					{"content-type", "application/x-www-form-urlencoded"},
				};

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 2,
					Url = "https://www.wirral.gov.uk/bincal_dev/",
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
			// Prepare client-side request to get bin collection data
			else if (clientSideResponse.RequestId == 2)
			{
				var viewState = ViewStateTokenRegex().Match(clientSideResponse.Content).Groups["viewStateValue"].Value;
				var eventValidation = EventValidationRegex().Match(clientSideResponse.Content).Groups["viewStateValue"].Value;

				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
				{
					{"__VIEWSTATE", viewState},
					{"__EVENTVALIDATION", eventValidation},
					{"ctl00$MainContent$Postcode", address.Postcode!},
					{"ctl00$MainContent$addressDropDown", address.Uid!},
					{"ctl00$MainContent$FindRounds", "Find bin collections"},
				});

				var requestHeaders = new Dictionary<string, string> {
					{"user-agent", Constants.UserAgent},
					{"content-type", "application/x-www-form-urlencoded"},
				};

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 3,
					Url = "https://www.wirral.gov.uk/bincal_dev/",
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
			else if (clientSideResponse.RequestId == 3)
			{
				// Get bin days from response
				var rawBinDays = BinDaysRegex().Matches(clientSideResponse.Content)!;
				var binDays = new List<BinDay>();

				// Iterate through each bin day, and create a new bin day object
				foreach (Match rawBinDay in rawBinDays)
				{
					var binName = rawBinDay.Groups["binName"].Value;
					var date = rawBinDay.Groups["date"].Value.TrimEnd('.');

					// Parse the collection date
					var collectionDate = DateOnly.ParseExact(
						date,
						"dddd dd MMMM yyyy",
						CultureInfo.InvariantCulture
					);

					// Get matching bin types from the type using the keys
					var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, binName);

					binDays.Add(new BinDay
					{
						Date = collectionDate,
						Address = address,
						Bins = matchedBinTypes,
					});
				}

				var getBinDaysResponse = new GetBinDaysResponse
				{
					BinDays = ProcessingUtilities.ProcessBinDays(binDays),
				};

				return getBinDaysResponse;
			}

			throw new InvalidOperationException("Invalid client-side request.");
		}
	}
}
