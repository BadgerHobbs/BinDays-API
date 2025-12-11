namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Collectors.Vendors;
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Text.Json;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Collector implementation for East Devon District Council.
	/// </summary>
	internal sealed partial class EastDevonDistrictCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "East Devon District Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://eastdevon.gov.uk/recycling-and-waste/recycling-waste-information/when-is-my-bin-collected/");

		/// <inheritdoc/>
		public override string GovUkId => "east-devon";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly IReadOnlyCollection<Bin> _binTypes = new List<Bin>()
		{
			new()
			{
				Name = "General Waste",
				Colour = BinColour.Black,
				Keys = new List<string>() { "Rubbish" }.AsReadOnly(),
				Type = BinType.Bin,
			},
			new()
			{
				Name = "Recycling (Paper/Glass/Cardboard)",
				Colour = BinColour.Green,
				Keys = new List<string>() { "Recycling and food waste" }.AsReadOnly(),
				Type = BinType.Box,
			},
			new()
			{
				Name = "Recycling (Plastics/Tins)",
				Colour = BinColour.Green,
				Keys = new List<string>() { "Recycling and food waste" }.AsReadOnly(),
				Type = BinType.Sack,
			},
			new()
			{
				Name = "Food Waste",
				Colour = BinColour.Blue,
				Keys = new List<string>() { "Recycling and food waste" }.AsReadOnly(),
				Type = BinType.Caddy,
			},
			new()
			{
				Name = "Garden Waste",
				Colour = BinColour.Green,
				Keys = new List<string>() { "Green waste" }.AsReadOnly(),
				Type = BinType.Bin,
			},
		}.AsReadOnly();

		/// <summary>
		/// Regex for the event month headers.
		/// </summary>
		[GeneratedRegex(@"<li class=""eventmonth""[^>]*><h2>(?<Month>[^<]+)</h2></li>", RegexOptions.IgnoreCase)]
		private static partial Regex EventMonthRegex();

		/// <summary>
		/// Regex for a month section and its collections.
		/// </summary>
		[GeneratedRegex(@"<li class=""eventmonth""[^>]*><h2>(?<Month>[^<]+)</h2></li>(?<Section>.*?)(?=<li class=""eventmonth""|</ol>)", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
		private static partial Regex MonthSectionRegex();

		/// <summary>
		/// Regex for the collection entries within a month.
		/// </summary>
		[GeneratedRegex(@"<li>\s*<span class=""collectiondate [^""]+"">(?<DayText>[^<]+)</span>(?<BinHtml>.*?)</li>", RegexOptions.Singleline)]
		private static partial Regex CollectionRegex();

		/// <summary>
		/// Regex for the bin labels inside a collection entry.
		/// </summary>
		[GeneratedRegex(@"<span class=""collection-[^""]+"">(?<BinType>[^<]+)</span>", RegexOptions.Singleline)]
		private static partial Regex CollectionBinRegex();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting addresses
			if (clientSideResponse == null)
			{
				var requestUrl = $"https://eastdevon.gov.uk/addressfinder?qtype=bins&term={postcode}";

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = requestUrl,
					Method = "GET",
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
				using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);

				var addresses = new List<Address>();
				foreach (var addressElement in jsonDoc.RootElement.EnumerateArray())
				{
					var address = new Address
					{
						Property = addressElement.GetProperty("label").GetString()?.Trim(),
						Postcode = postcode,
						Uid = addressElement.GetProperty("UPRN").GetString(),
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
				var requestUrl = $"https://eastdevon.gov.uk/recycling-and-waste/recycling-waste-information/when-is-my-bin-collected/future-collections-calendar/?UPRN={address.Uid}";

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = requestUrl,
					Method = "GET",
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
				var content = clientSideResponse.Content;
				var monthSections = MonthSectionRegex().Matches(content);

				var binDays = new List<BinDay>();

				foreach (Match monthSection in monthSections)
				{
					var month = monthSection.Groups["Month"].Value.Trim();
					var collectionMatches = CollectionRegex().Matches(monthSection.Groups["Section"].Value);
					foreach (Match collectionMatch in collectionMatches)
					{
						var dayText = collectionMatch.Groups["DayText"].Value.Trim();
						var day = int.Parse(
							dayText.Split([' ', '\u00A0'], StringSplitOptions.RemoveEmptyEntries)[0],
							CultureInfo.InvariantCulture
						);

						var dateString = $"{day} {month}";
						var collectionDate = DateOnly.ParseExact(
							dateString,
							"d MMMM yyyy",
							CultureInfo.InvariantCulture
						);

						var binHtml = collectionMatch.Groups["BinHtml"].Value;
						var matchedBinTypes = CollectionBinRegex()
							.Matches(binHtml)
							.SelectMany(binMatch => ProcessingUtilities.GetMatchingBins(_binTypes, binMatch.Groups["BinType"].Value.Trim()))
							.Distinct()
							.ToList()
							.AsReadOnly();

						var binDay = new BinDay
						{
							Date = collectionDate,
							Address = address,
							Bins = matchedBinTypes,
						};

						binDays.Add(binDay);
					}
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
