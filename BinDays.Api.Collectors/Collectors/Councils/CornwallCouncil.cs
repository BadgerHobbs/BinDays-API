namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Collectors.Vendors;
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Net;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Collector implementation for Cornwall Council.
	/// </summary>
	internal sealed partial class CornwallCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "Cornwall Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://www.cornwall.gov.uk/my-area");

		/// <inheritdoc/>
		public override string GovUkId => "cornwall";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly IReadOnlyCollection<Bin> _binTypes = new List<Bin>()
		{
			new()
			{
				Name = "Food Waste",
				Colour = BinColour.Green,
				Keys = new List<string>() { "food" }.AsReadOnly(),
			},
			new()
			{
				Name = "Recycling",
				Colour = BinColour.Orange,
				Keys = new List<string>() { "recycling" }.AsReadOnly(),
				Type = BinType.Bag,
			},
			new()
			{
				Name = "Rubbish",
				Colour = BinColour.Black,
				Keys = new List<string>() { "rubbish" }.AsReadOnly(),
			},
			new()
			{
				Name = "Garden Waste",
				Colour = BinColour.Brown,
				Keys = new List<string>() { "garden" }.AsReadOnly(),
			},
		}.AsReadOnly();

		/// <summary>
		/// Regex for the addresses from the options elements.
		/// </summary>
		[GeneratedRegex(@"<option value=""(?<uid>\d+)"">(?<address>[\s\S]*?)<\/option>")]
		private static partial Regex AddressesRegex();

		/// <summary>
		/// Regex for the bin days from the data elements.
		/// </summary>
		[GeneratedRegex(@"(?s)<div id=""(?<binId>[^""]+)-nom"" class=""collection.*?<span>[^<]+</span>\s*<span>[^<]+</span>\s*<span>(?<date>[^<]+)</span>")]
		private static partial Regex BinDaysRegex();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting addresses
			if (clientSideResponse == null)
			{
				var requestUrl = $"https://www.cornwall.gov.uk/umbraco/surface/geo/MyAreaAddressList?postcode={postcode}";

				var requestHeaders = new Dictionary<string, string>
				{
					{ "X-Requested-With", "XMLHttpRequest" },
					{ "User-Agent", Constants.UserAgent },
				};

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = requestUrl,
					Method = "GET",
					Headers = requestHeaders,
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
				// Get addresses from response
				var rawAddresses = AddressesRegex().Matches(clientSideResponse.Content);

				var addresses = new List<Address>();
				foreach (Match rawAddress in rawAddresses)
				{
					var uid = rawAddress.Groups["uid"].Value;
					var addressText = rawAddress.Groups["address"].Value;

					// Decode HTML entities and take the first line of the address
					var decodedAddress = WebUtility.HtmlDecode(addressText);
					var property = decodedAddress.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();

					var address = new Address
					{
						Property = property,
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
			// Prepare client-side request for getting bin days
			if (clientSideResponse == null)
			{
				var requestUrl = $"https://www.cornwall.gov.uk/umbraco/surface/waste/MyCollectionDays?uprn={address.Uid}";

				var requestHeaders = new Dictionary<string, string>
				{
					{ "X-Requested-With", "XMLHttpRequest" },
					{ "User-Agent", Constants.UserAgent },
				};

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = requestUrl,
					Method = "GET",
					Headers = requestHeaders,
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
				// Get bin days from response
				var rawBinDays = BinDaysRegex().Matches(clientSideResponse.Content);

				var binDays = new List<BinDay>();
				foreach (Match rawBinDay in rawBinDays)
				{
					var binId = rawBinDay.Groups["binId"].Value;
					var dateString = rawBinDay.Groups["date"].Value;

					// Parse date string (e.g. "18 Jul")
					var date = DateOnly.ParseExact(
						dateString,
						"d MMM",
						CultureInfo.InvariantCulture,
						DateTimeStyles.None
					);

					// If the parsed date is in the past, assume it's for the next year
					if (date < DateOnly.FromDateTime(DateTime.Today))
					{
						date = date.AddYears(1);
					}

					// Get matching bin types from the bin ID using the keys
					var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, binId);

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
