namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Collectors.Vendors;
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Text.Json;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Collector implementation for Telford and Wrekin Council.
	/// </summary>
	internal sealed partial class TelfordAndWrekinCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "Telford & Wrekin Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://www.telford.gov.uk");

		/// <inheritdoc/>
		public override string GovUkId => "telford-and-wrekin";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly IReadOnlyCollection<Bin> _binTypes = new List<Bin>()
		{
			new()
			{
				Name = "General Waste",
				Colour = BinColour.Black,
				Keys = new List<string>() { "Red Top Container" }.AsReadOnly(),
			},
			new()
			{
				Name = "Recycling",
				Colour = BinColour.Purple,
				Keys = new List<string>() { "Purple / Blue Containers" }.AsReadOnly(),
			},
			new()
			{
				Name = "Cardboard",
				Colour = BinColour.Blue,
				Keys = new List<string>() { "Purple / Blue Containers" }.AsReadOnly(),
				Type = BinType.Bag,
			},
			new()
			{
				Name = "Food Waste",
				Colour = BinColour.Grey,
				Keys = new List<string>() { "Silver Containers" }.AsReadOnly(),
				Type = BinType.Caddy,
			},
			new()
			{
				Name = "Garden Waste",
				Colour = BinColour.Green,
				Keys = new List<string>() { "Green Container" }.AsReadOnly(),
			},
		}.AsReadOnly();

		/// <summary>
		/// Regex for removing the st|nd|rd|th from the date part
		/// </summary>
		[GeneratedRegex(@"(?<=\d)(st|nd|rd|th)")]
		private static partial Regex CollectionDateRegex();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting addresses
			if (clientSideResponse == null)
			{
				var requestUrl = $"https://dac.telford.gov.uk/BinDayFinder/Find/PostcodeSearch?postcode={postcode}";

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
				using var jsonDoc = JsonDocument.Parse(JsonDocument.Parse(clientSideResponse.Content).RootElement.GetString()!);
				var rawAddresses = jsonDoc.RootElement.GetProperty("properties");

				var addresses = new List<Address>();

				foreach (var addressElement in rawAddresses.EnumerateArray())
				{
					var address = new Address
					{
						Property = addressElement.GetProperty("Description").GetString(),
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
				var requestUrl = $"https://dac.telford.gov.uk/BinDayFinder/Find/PropertySearch?uprn={address.Uid}";

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
				using var jsonDoc = JsonDocument.Parse(JsonDocument.Parse(clientSideResponse.Content).RootElement.GetString()!);
				var rawBinDays = jsonDoc.RootElement.GetProperty("bincollections");

				var binDays = new List<BinDay>();

				foreach (var rawBinDay in rawBinDays.EnumerateArray())
				{
					var dateString = rawBinDay.GetProperty("nextDate").GetString()!;

					if (string.IsNullOrEmpty(dateString))
					{
						continue;
					}

					// Strip the st|nd|rd|th and remove day of the week
					dateString = CollectionDateRegex().Replace(dateString, "");

					// Parse the date (e.g. "Monday 15th December")
					var date = DateOnly.ParseExact(
						dateString,
						"dddd d MMMM",
						CultureInfo.InvariantCulture,
						DateTimeStyles.None
					);

					// If the parsed date is in a month that has already passed this year, assume it's for next year
					if (date.Month < DateTime.Now.Month)
					{
						date = date.AddYears(1);
					}

					var binType = rawBinDay.GetProperty("name").GetString()!;
					var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, binType);

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
