namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Collectors.Vendors;
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Collector implementation for South Ayrshire Council.
	/// </summary>
	internal sealed partial class SouthAyrshireCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "South Ayrshire Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://www.south-ayrshire.gov.uk/");

		/// <inheritdoc/>
		public override string GovUkId => "south-ayrshire";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly IReadOnlyCollection<Bin> _binTypes = new List<Bin>()
		{
			new()
			{
				Name = "General Waste",
				Colour = BinColour.Green,
				Type = BinType.Bin,
				Keys = new List<string>() { "Green Bin" }.AsReadOnly()
			},
			new()
			{
				Name = "Recycling",
				Colour = BinColour.Blue,
				Type = BinType.Bin,
				Keys = new List<string>() { "Blue Bin" }.AsReadOnly()
			},
			new()
			{
				Name = "Garden Waste",
				Colour = BinColour.Brown,
				Type = BinType.Bin,
				Keys = new List<string>() { "Brown Bin" }.AsReadOnly()
			},
			new()
			{
				Name = "Glass",
				Colour = BinColour.Purple,
				Type = BinType.Bin,
				Keys = new List<string>() { "Purple Bin" }.AsReadOnly()
			},
			new()
			{
				Name = "Cardboard",
				Colour = BinColour.Grey,
				Type = BinType.Bin,
				Keys = new List<string>() { "Anthracite Bin" }.AsReadOnly()
			},
			new()
			{
				Name = "Food Waste",
				Colour = BinColour.Grey,
				Type = BinType.Caddy,
				Keys = new List<string>() { "Food Bin" }.AsReadOnly()
			},
		}.AsReadOnly();

		/// <summary>
		/// Regex for address entries on the search results page.
		/// </summary>
		[GeneratedRegex(@"<h3><a href=""calendar\.php\?id=(?<id>\d+)&postcode=[^""]+"">(?<property>[^<]+)<\/a><\/h3>")]
		private static partial Regex AddressesRegex();

		/// <summary>
		/// Regex for bin collection events embedded in the calendar page.
		/// </summary>
		[GeneratedRegex(@"title:\s*'(?<bin>[^']+)'[\s\S]*?start:'(?<date>\d{4}-\d{2}-\d{2})'")]
		private static partial Regex BinEventsRegex();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			if (clientSideResponse == null)
			{
				var sanitizedPostcode = postcode.Replace(" ", string.Empty).ToUpperInvariant();

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = $"https://www.sac-bins.co.uk/search.php?postcode={sanitizedPostcode}",
					Method = "GET",
					Headers = new()
					{
						{ "user-agent", Constants.UserAgent },
					},
				};

				return new GetAddressesResponse
				{
					NextClientSideRequest = clientSideRequest,
				};
			}
			else if (clientSideResponse.RequestId == 1)
			{
				var addresses = new List<Address>();
				var matches = AddressesRegex().Matches(clientSideResponse.Content);

				foreach (Match match in matches)
				{
					addresses.Add(new Address
					{
						Property = match.Groups["property"].Value.Trim(),
						Postcode = postcode,
						Uid = match.Groups["id"].Value.Trim(),
					});
				}

				return new GetAddressesResponse
				{
					Addresses = addresses.AsReadOnly(),
				};
			}

			throw new InvalidOperationException("Invalid client-side request.");
		}

		/// <inheritdoc/>
		public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
			{
				if (clientSideResponse == null)
				{
					var sanitizedPostcode = (address.Postcode ?? string.Empty)
						.Replace(" ", string.Empty)
						.ToUpperInvariant();

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = $"https://www.sac-bins.co.uk/calendar.php?id={address.Uid}&postcode={sanitizedPostcode}",
					Method = "GET",
					Headers = new()
					{
						{ "user-agent", Constants.UserAgent },
					},
				};

				return new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest,
				};
			}
			else if (clientSideResponse.RequestId == 1)
			{
				var eventsByBin = new Dictionary<string, List<DateOnly>>(StringComparer.OrdinalIgnoreCase);
				var matches = BinEventsRegex().Matches(clientSideResponse.Content);

				foreach (Match match in matches)
				{
					var binName = match.Groups["bin"].Value.Trim();
					var date = DateOnly.ParseExact(
						match.Groups["date"].Value,
						"yyyy-MM-dd",
						CultureInfo.InvariantCulture
					);

					if (!eventsByBin.TryGetValue(binName, out var dates))
					{
						dates = new List<DateOnly>();
						eventsByBin[binName] = dates;
					}

					dates.Add(date);
				}

				var binDays = BuildBinDays(address, eventsByBin);

				return new GetBinDaysResponse
				{
					BinDays = ProcessingUtilities.ProcessBinDays(binDays),
				};
			}

			throw new InvalidOperationException("Invalid client-side request.");
		}

		private List<BinDay> BuildBinDays(Address address, Dictionary<string, List<DateOnly>> eventsByBin)
		{
			var binDays = new List<BinDay>();
			var targetDate = DateOnly.FromDateTime(DateTime.Now).AddMonths(12);

			foreach (var entry in eventsByBin)
			{
				var binName = entry.Key;
				var dates = entry.Value.OrderBy(d => d).ToList();
				if (dates.Count == 0)
				{
					continue;
				}

				var matchingBins = ProcessingUtilities.GetMatchingBins(_binTypes, binName);
				if (matchingBins.Count == 0)
				{
					continue;
				}

				var intervalDays = GetIntervalDays(dates);
				if (intervalDays > 0)
				{
					var seenDates = new HashSet<DateOnly>(dates);
					var lastDate = dates[^1];

					while (lastDate < targetDate)
					{
						lastDate = lastDate.AddDays(intervalDays);

						if (seenDates.Add(lastDate))
						{
							dates.Add(lastDate);
						}
						else
						{
							break;
						}
					}
				}

				foreach (var date in dates)
				{
					binDays.Add(new BinDay
					{
						Date = date,
						Address = address,
						Bins = matchingBins,
					});
				}
			}

			return binDays;
		}

		private static int GetIntervalDays(IReadOnlyList<DateOnly> dates)
		{
			if (dates.Count < 2)
			{
				return 0;
			}

			var differences = dates
				.Zip(dates.Skip(1), (first, second) => (second.DayNumber - first.DayNumber))
				.GroupBy(d => d)
				.OrderByDescending(group => group.Count())
				.ThenBy(group => group.Key)
				.First();

			return differences.Key;
		}
	}
}
