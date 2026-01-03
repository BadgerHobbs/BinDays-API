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
	/// Collector implementation for Cheshire East Council.
	/// </summary>
	internal sealed partial class CheshireEastCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "Cheshire East Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://online.cheshireeast.gov.uk/");

		/// <inheritdoc/>
		public override string GovUkId => "cheshire-east";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly IReadOnlyCollection<Bin> _binTypes = new List<Bin>()
		{
			new()
			{
				Name = "General waste",
				Colour = BinColour.Black,
				Keys = new List<string>() { "General Waste" }.AsReadOnly(),
				Type = BinType.Bin,
			},
			new()
			{
				Name = "Recycling",
				Colour = BinColour.Grey,
				Keys = new List<string>() { "Mixed Recycling" }.AsReadOnly(),
				Type = BinType.Bin,
			},
			new()
			{
				Name = "Garden waste",
				Colour = BinColour.Green,
				Keys = new List<string>() { "Garden Waste" }.AsReadOnly(),
				Type = BinType.Bin,
			},
		}.AsReadOnly();

		/// <summary>
		/// Regex for parsing addresses from the search response.
		/// </summary>
		[GeneratedRegex(@"<a class=""select-csv-address""[^>]*data-uprn=""(?<uprn>[^""]+)""[^>]*>(?<address>[^<]+)</a>", RegexOptions.IgnoreCase)]
		private static partial Regex AddressRegex();

		/// <summary>
		/// Regex for parsing bin day rows from the job list.
		/// </summary>
		[GeneratedRegex(@"BartecSimplifiedJobList_(?<index>\d+)__Name""[^>]*value=""(?<name>[^""]+)"" .*?BartecSimplifiedJobList_\k<index>__ScheduledStart""[^>]*value=""(?<date>[^""]+)""", RegexOptions.Singleline)]
		private static partial Regex BinDayRegex();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting session cookies
			if (clientSideResponse == null)
			{
				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = "https://online.cheshireeast.gov.uk/MyCollectionDay/",
					Method = "GET",
					Headers = new Dictionary<string, string>
					{
						{ "User-Agent", Constants.UserAgent }
					},
				};

				return new GetAddressesResponse
				{
					NextClientSideRequest = clientSideRequest
				};
			}
			// Prepare client-side request for getting addresses
			else if (clientSideResponse.RequestId == 1)
			{
				var formattedPostcode = ProcessingUtilities.FormatPostcode(postcode);
				var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
				var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(
					clientSideResponse.Headers.GetValueOrDefault("set-cookie") ?? string.Empty);
				var requestUrl = $"https://online.cheshireeast.gov.uk/MyCollectionDay/SearchByAjax/Search?postcode={Uri.EscapeDataString(formattedPostcode)}&propertyname=&_={timestamp}";

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 2,
					Url = requestUrl,
					Method = "GET",
					Headers = new Dictionary<string, string>
					{
						{ "User-Agent", Constants.UserAgent },
						{ "x-requested-with", "XMLHttpRequest" },
						{ "cookie", requestCookies },
					},
				};

				return new GetAddressesResponse
				{
					NextClientSideRequest = clientSideRequest
				};
			}
			// Process addresses from response
			else if (clientSideResponse.RequestId == 2)
			{
				var rawAddresses = AddressRegex().Matches(clientSideResponse.Content);

				var formattedPostcode = ProcessingUtilities.FormatPostcode(postcode);
				var addresses = new List<Address>();
				foreach (Match rawAddress in rawAddresses)
				{
					var addressText = rawAddress.Groups["address"].Value.Trim();
					var addressParts = addressText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

					var address = new Address
					{
						Property = addressParts.Length > 0 ? addressParts[0] : null,
						Street = addressParts.Length > 3 ? addressParts[1] : null,
						Town = addressParts.Length > 3 ? addressParts[2] : addressParts.Length > 2 ? addressParts[1] : null,
						Postcode = addressParts.Length > 0 ? addressParts[^1] : formattedPostcode,
						Uid = rawAddress.Groups["uprn"].Value,
					};

					addresses.Add(address);
				}

				return new GetAddressesResponse
				{
					Addresses = addresses.AsReadOnly(),
				};
			}

			// Throw exception for invalid request
			throw new InvalidOperationException("Invalid client-side request.");
		}

		/// <inheritdoc/>
		public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting session cookies
			if (clientSideResponse == null)
			{
				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = "https://online.cheshireeast.gov.uk/MyCollectionDay/",
					Method = "GET",
					Headers = new Dictionary<string, string>
					{
						{ "User-Agent", Constants.UserAgent }
					},
				};

				return new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest
				};
			}
			// Prepare client-side request for getting bin days
			else if (clientSideResponse.RequestId == 1)
			{
				var formattedPostcode = ProcessingUtilities.FormatPostcode(address.Postcode ?? string.Empty);
				var onelineAddress = address.Property ?? address.Street ?? address.Town ?? string.Empty;
				if (!string.IsNullOrWhiteSpace(formattedPostcode))
				{
					onelineAddress = string.IsNullOrWhiteSpace(onelineAddress)
						? formattedPostcode
						: $"{onelineAddress}, {formattedPostcode}";
				}

				var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
				var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(
					clientSideResponse.Headers.GetValueOrDefault("set-cookie") ?? string.Empty);
				var requestUrl = $"https://online.cheshireeast.gov.uk/MyCollectionDay/SearchByAjax/GetBartecJobList?uprn={address.Uid}&onelineaddress={Uri.EscapeDataString(onelineAddress)}&_={timestamp}";

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 2,
					Url = requestUrl,
					Method = "GET",
					Headers = new Dictionary<string, string>
					{
						{ "User-Agent", Constants.UserAgent },
						{ "x-requested-with", "XMLHttpRequest" },
						{ "cookie", requestCookies },
					},
				};

				return new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest
				};
			}
			// Process bin days from response
			else if (clientSideResponse.RequestId == 2)
			{
				var rawBinDays = BinDayRegex().Matches(clientSideResponse.Content);

				var binDays = new List<BinDay>();
				foreach (Match rawBinDay in rawBinDays)
				{
					var binTypeStr = rawBinDay.Groups["name"].Value.Trim();
					var dateStr = rawBinDay.Groups["date"].Value.Trim();

					// Parse date string (e.g. "13/01/2026 07:00:00")
					var dateTime = DateTime.ParseExact(dateStr, "dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);

					var binDay = new BinDay
					{
						Date = DateOnly.FromDateTime(dateTime),
						Address = address,
						Bins = ProcessingUtilities.GetMatchingBins(_binTypes, binTypeStr),
					};

					binDays.Add(binDay);
				}

				return new GetBinDaysResponse
				{
					BinDays = ProcessingUtilities.ProcessBinDays(binDays),
				};
			}

			// Throw exception for invalid request
			throw new InvalidOperationException("Invalid client-side request.");
		}
	}
}
