namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Globalization;
	using System.Linq;
	using System.Text.Json;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Collector implementation for Exeter City Council.
	/// </summary>
	internal sealed partial class ExeterCityCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "Exeter City Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://exeter.gov.uk/");

		/// <inheritdoc/>
		public override string GovUkId => "exeter";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly ReadOnlyCollection<Bin> _binTypes = new List<Bin>()
		{
			new()
			{
				Name = "Refuse",
				Colour = BinColour.Black,
				Keys = new List<string>() { "Refuse collection" }.AsReadOnly(),
			},
			new()
			{
				Name = "Recycling",
				Colour = BinColour.Green,
				Keys = new List<string>() { "Recycling collection" }.AsReadOnly(),
			},
			new()
			{
				Name = "Food Waste",
				Colour = BinColour.Grey,
				Keys = new List<string>() { "Food waste collection" }.AsReadOnly(),
				Type = BinType.Caddy
			},
			new()
			{
				Name = "Garden Waste",
				Colour = BinColour.Brown,
				Keys = new List<string>() { "Garden waste collection" }.AsReadOnly(),
			},
		}.AsReadOnly();

		/// <summary>
		/// Regex for the bin days from the results html.
		/// </summary>
		[GeneratedRegex(@"(?s)<h2>(?<collection>.*?)</h2>.*?<h3>.*?>(?<date>.*?)</h3>")]
		private static partial Regex BinDaysRegex();

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
				var requestUrl = $"https://exeter.gov.uk/repositories/hidden-pages/address-finder?qtype=bins&term={postcode}";

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = requestUrl,
					Method = "GET",
				};

				var getAddressesResponse = new GetAddressesResponse()
				{
					NextClientSideRequest = clientSideRequest
				};

				return getAddressesResponse;
			}
			// Process addresses from response
			else if (clientSideResponse.RequestId == 1)
			{
				// Parse response content as JSON array
				using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);

				// Iterate through each address json, and create a new address object
				var addresses = new List<Address>();
				foreach (var addressElement in jsonDoc.RootElement.EnumerateArray())
				{
					string? property = addressElement.GetProperty("label").GetString();
					string? uprn = addressElement.GetProperty("UPRN").GetString();

					var address = new Address()
					{
						Property = property?.Trim(),
						Postcode = postcode,
						Uid = uprn,
					};

					addresses.Add(address);
				}

				var getAddressesResponse = new GetAddressesResponse()
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
				var requestUrl = $"https://exeter.gov.uk/repositories/hidden-pages/address-finder?qtype=bins&term={address.Postcode}";

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = requestUrl,
					Method = "GET",
				};

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					NextClientSideRequest = clientSideRequest
				};

				return getBinDaysResponse;
			}
			// Process bin days from response
			else if (clientSideResponse.RequestId == 1)
			{
				// Parse response content as JSON array
				using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);

				// Find the address in the response
				var addressElement = jsonDoc.RootElement.EnumerateArray()
					.FirstOrDefault(e => e.GetProperty("UPRN").GetString() == address.Uid);

				// Get the results html from the address element
				var resultsHtml = addressElement.GetProperty("Results").GetString();

				// Get bin days from response
				var rawBinDays = BinDaysRegex().Matches(resultsHtml!);

				// Iterate through each bin day, and create a new bin day object
				var binDays = new List<BinDay>();
				foreach (Match rawBinDay in rawBinDays)
				{
					var collection = rawBinDay.Groups["collection"].Value;
					var dateString = rawBinDay.Groups["date"].Value;

					// Remove the st|nd|rd|th from the date part (e.g. '16th')
					dateString = CollectionDateRegex().Replace(dateString, "");

					// Parse the date (e.g. 'Wednesday, 8 October 2025')
					var date = DateOnly.ParseExact(
						dateString,
						"dddd, d MMMM yyyy",
						CultureInfo.InvariantCulture,
						DateTimeStyles.None
					);

					// Get matching bin types from the collection using the keys
					var matchedBinTypes = _binTypes.Where(x => x.Keys.Any(y => collection.Contains(y)));

					var binDay = new BinDay()
					{
						Date = date,
						Address = address,
						Bins = matchedBinTypes.ToList().AsReadOnly()
					};

					binDays.Add(binDay);
				}

				var getBinDaysResponse = new GetBinDaysResponse()
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
