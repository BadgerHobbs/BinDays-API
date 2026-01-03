namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Collectors.Vendors;
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Globalization;
	using System.Linq;
	using System.Text.RegularExpressions;
	using System.Web;

	/// <summary>
	/// Collector implementation for The Moray Council.
	/// </summary>
	internal sealed partial class TheMorayCouncil : GovUkCollectorBase, ICollector
	{
		private const string BaseUrl = "https://bindayfinder.moray.gov.uk/";

		/// <inheritdoc/>
		public string Name => "The Moray Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new(BaseUrl);

		/// <inheritdoc/>
		public override string GovUkId => "moray";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly IReadOnlyCollection<Bin> _binTypes = new List<Bin>()
		{
			new()
			{
				Name = "General Waste",
				Colour = BinColour.Green,
				Keys = new List<string>() { "Green" }.AsReadOnly(),
				Type = BinType.Bin,
			},
			new()
			{
				Name = "Garden Waste",
				Colour = BinColour.Brown,
				Keys = new List<string>() { "Brown" }.AsReadOnly(),
				Type = BinType.Bin,
			},
			new()
			{
				Name = "Paper and Card",
				Colour = BinColour.Blue,
				Keys = new List<string>() { "Blue", "Card" }.AsReadOnly(),
				Type = BinType.Bin,
			},
			new()
			{
				Name = "Cans and Plastics",
				Colour = BinColour.Purple,
				Keys = new List<string>() { "Purple" }.AsReadOnly(),
				Type = BinType.Bin,
			},
			new()
			{
				Name = "Glass",
				Colour = BinColour.Orange,
				Keys = new List<string>() { "Glass", "Orange" }.AsReadOnly(),
				Type = BinType.Box,
			},
		}.AsReadOnly();

		/// <summary>
		/// Regex for addresses from the search response.
		/// </summary>
		[GeneratedRegex("<a href=['\"]disp_bins\\.php\\?id=(?<Id>\\d+)['\"]>(?<Property>.*?)</a>", RegexOptions.Singleline)]
		private static partial Regex AddressRegex();

		/// <summary>
		/// Regex for calendar links in the property response.
		/// </summary>
		[GeneratedRegex("href=['\"](?<Url>(?:https?://bindayfinder\\.moray\\.gov\\.uk/)?cal_(?<Year>\\d{4})_view\\.php\\?id=\\d+)['\"]", RegexOptions.Singleline)]
		private static partial Regex CalendarLinkRegex();

		/// <summary>
		/// Regex for months and day containers in the calendar.
		/// </summary>
		[GeneratedRegex("<div class='month-container'>\\s*<div class='month-header'><h2>(?<Month>[^<]+)</h2></div>.*?<div class='days-container'>(?<Days>.*?)</div>\\s*</div>", RegexOptions.Singleline)]
		private static partial Regex MonthRegex();

		/// <summary>
		/// Regex for day entries in the calendar.
		/// </summary>
		[GeneratedRegex("<div class='(?<Class>[^']*)'>(?<Day>\\d+)</div>", RegexOptions.Singleline)]
		private static partial Regex DayRegex();

		/// <summary>
		/// Regex for the calendar year from the header.
		/// </summary>
		[GeneratedRegex("Collections for (?<Year>\\d{4})")]
		private static partial Regex CalendarYearRegex();

		/// <summary>
		/// Regex for normalising whitespace.
		/// </summary>
		[GeneratedRegex("\\s+")]
		private static partial Regex WhitespaceRegex();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			var formattedPostcode = ProcessingUtilities.FormatPostcode(postcode);

			// Prepare client-side request for getting addresses
			if (clientSideResponse == null)
			{
				var encodedPostcode = HttpUtility.UrlEncode(formattedPostcode);
				var requestUrl = $"{BaseUrl}refuse_roads.php?strname=&pcode={encodedPostcode}";

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = requestUrl,
					Method = "GET",
					Headers = new()
					{
						{ "user-agent", Constants.UserAgent },
					},
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
				var rawAddresses = AddressRegex().Matches(clientSideResponse.Content);

				var addresses = new List<Address>();
				foreach (Match rawAddress in rawAddresses)
				{
					var property = NormalizeWhitespace(rawAddress.Groups["Property"].Value);
					var id = rawAddress.Groups["Id"].Value;

					var address = new Address
					{
						Property = property,
						Postcode = formattedPostcode,
						Uid = id,
					};

					addresses.Add(address);
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
			// Prepare client-side request for getting bin days
			if (clientSideResponse == null)
			{
				var requestUrl = $"{BaseUrl}disp_bins.php?id={address.Uid}";

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = requestUrl,
					Method = "GET",
					Headers = new()
					{
						{ "user-agent", Constants.UserAgent },
					},
				};

				var getBinDaysResponse = new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest
				};

				return getBinDaysResponse;
			}
			// Parse the property page to find calendar links
			else if (clientSideResponse.RequestId == 1)
			{
				var calendarLinks = CalendarLinkRegex()
					.Matches(clientSideResponse.Content)
					.Cast<Match>()
					.Select(match => new
					{
						Url = EnsureAbsoluteUrl(match.Groups["Url"].Value),
						Year = int.Parse(match.Groups["Year"].Value, CultureInfo.InvariantCulture)
					})
					.OrderBy(link => link.Year)
					.ToList();

				if (calendarLinks.Count == 0)
				{
					throw new InvalidOperationException("No calendar links found for the selected address.");
				}

				var remainingCalendars = calendarLinks.Skip(1).Select(link => link.Url).ToList();
				var metadata = new Dictionary<string, string>();

				if (remainingCalendars.Any())
				{
					metadata.Add("calendarUrls", string.Join("|", remainingCalendars));
				}

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 2,
					Url = calendarLinks.First().Url,
					Method = "GET",
					Headers = new()
					{
						{ "user-agent", Constants.UserAgent },
					},
					Options = new ClientSideOptions
					{
						Metadata = metadata
					},
				};

				var getBinDaysResponse = new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest
				};

				return getBinDaysResponse;
			}
			// Process calendar pages, chaining through any remaining years
			else if (clientSideResponse.RequestId >= 2)
			{
				var metadata = clientSideResponse.Options.Metadata;
				var binData = new List<(DateOnly Date, string ClassCode)>();

				if (metadata.TryGetValue("binData", out var binDataValue))
				{
					binData.AddRange(DeserializeBinData(binDataValue));
				}

				binData.AddRange(ParseCalendar(clientSideResponse.Content));

				if (metadata.TryGetValue("calendarUrls", out var remainingCalendars) && !string.IsNullOrWhiteSpace(remainingCalendars))
				{
					var calendars = remainingCalendars.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList();
					var nextCalendar = calendars.First();
					calendars.RemoveAt(0);

					metadata["binData"] = SerializeBinData(binData);

					if (calendars.Any())
					{
						metadata["calendarUrls"] = string.Join("|", calendars);
					}
					else
					{
						metadata.Remove("calendarUrls");
					}

					var clientSideRequest = new ClientSideRequest
					{
						RequestId = clientSideResponse.RequestId + 1,
						Url = EnsureAbsoluteUrl(nextCalendar),
						Method = "GET",
						Headers = new()
						{
							{ "user-agent", Constants.UserAgent },
						},
						Options = new ClientSideOptions
						{
							Metadata = metadata
						},
					};

					var getBinDaysResponse = new GetBinDaysResponse
					{
						NextClientSideRequest = clientSideRequest
					};

					return getBinDaysResponse;
				}

				var binDays = binData.Select(collection => new BinDay
				{
					Date = collection.Date,
					Address = address,
					Bins = GetBinsForClass(collection.ClassCode),
				}).ToList();

				var getBinDaysResponse = new GetBinDaysResponse
				{
					BinDays = ProcessingUtilities.ProcessBinDays(binDays),
				};

				return getBinDaysResponse;
			}

			throw new InvalidOperationException("Invalid client-side request.");
		}

		private static string EnsureAbsoluteUrl(string url)
		{
			if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
			{
				return url;
			}

			return $"{BaseUrl}{url.TrimStart('/')}";
		}

		private static string NormalizeWhitespace(string value)
		{
			return WhitespaceRegex().Replace(value, " ").Trim();
		}

		private static string SerializeBinData(IEnumerable<(DateOnly Date, string ClassCode)> binData)
		{
			return string.Join(";", binData.Select(data => $"{data.Date:yyyy-MM-dd}|{data.ClassCode}"));
		}

		private static IEnumerable<(DateOnly Date, string ClassCode)> DeserializeBinData(string binData)
		{
			return binData
				.Split(";", StringSplitOptions.RemoveEmptyEntries)
				.Select(data =>
				{
					var parts = data.Split("|", StringSplitOptions.RemoveEmptyEntries);
					return (DateOnly.ParseExact(parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture), parts[1]);
				});
		}

		private static List<(DateOnly Date, string ClassCode)> ParseCalendar(string content)
		{
			var year = int.Parse(CalendarYearRegex().Match(content).Groups["Year"].Value, CultureInfo.InvariantCulture);
			var collections = new List<(DateOnly Date, string ClassCode)>();

			var months = MonthRegex().Matches(content);

			foreach (Match monthMatch in months)
			{
				var monthName = monthMatch.Groups["Month"].Value.Trim();
				var daysContent = monthMatch.Groups["Days"].Value;

				var dayMatches = DayRegex().Matches(daysContent);

				foreach (Match dayMatch in dayMatches)
				{
					var collectionClass = dayMatch.Groups["Class"].Value.Trim();
					var day = dayMatch.Groups["Day"].Value;

					if (string.IsNullOrWhiteSpace(collectionClass) || collectionClass.Equals("blank", StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}

					var date = DateOnly.ParseExact($"{day} {monthName} {year}", "d MMMM yyyy", CultureInfo.InvariantCulture);

					collections.Add((date, collectionClass));
				}
			}

			return collections;
		}

		private ReadOnlyCollection<Bin> GetBinsForClass(string collectionClass)
		{
			return collectionClass switch
			{
				"B" => ProcessingUtilities.GetMatchingBins(_binTypes, "Brown"),
				"GPOC" => ProcessingUtilities.GetMatchingBins(_binTypes, "Green Purple Blue Orange"),
				"GBPOC" => ProcessingUtilities.GetMatchingBins(_binTypes, "Green Brown Purple Blue Orange"),
				_ => throw new InvalidOperationException($"Unrecognised collection class '{collectionClass}'."),
			};
		}
	}
}
