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

	/// <summary>
	/// Collector implementation for Ards and North Down Council.
	/// </summary>
	internal sealed partial class ArdsAndNorthDownCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "Ards and North Down Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://www.ardsandnorthdown.gov.uk/article/1141/Bins-and-recycling");

		/// <inheritdoc/>
		public override string GovUkId => "ards-and-north-down";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly IReadOnlyCollection<Bin> _binTypes = [
			new()
			{
				Name = "General Waste",
				Colour = BinColour.Grey,
				Keys = [ "General waste bin" ],
			},
			new()
			{
				Name = "Garden and Food Waste",
				Colour = BinColour.Brown,
				Keys = [ "Garden and food waste bin" ],
			},
			new()
			{
				Name = "Recycling",
				Colour = BinColour.Blue,
				Keys = [ "Recycling bin" ],
			},
			new()
			{
				Name = "Glass",
				Colour = BinColour.Yellow,
				Keys = [ "Glass container" ],
				Type = BinType.Box,
			},
		];

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting addresses
			if (clientSideResponse == null)
			{
				var requestUrl = $"https://ardsandnorthdownbincalendar.azurewebsites.net/api/addresses/{postcode}";

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
				var addresses = new List<Address>();

				// Parse response content as JSON array
				using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);

				// Iterate through each address json, and create a new address object
				foreach (var addressElement in jsonDoc.RootElement.GetProperty("addresses").EnumerateArray())
				{
					var property = addressElement.GetProperty("addressText").GetString();
					var uprn = addressElement.GetProperty("uprn").GetString();

					// Remove postcode from property (e.g.'1 OLD MILL COURT, NEWTOWNARDS, BT23 4JG')
					property = property?.Replace($", {postcode}", "", StringComparison.OrdinalIgnoreCase).Trim();

					var address = new Address
					{
						Property = property,
						Postcode = postcode,
						Uid = uprn,
					};
					addresses.Add(address);
				}

				var getAddressesResponse = new GetAddressesResponse
				{
					Addresses = [.. addresses],
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
				var requestUrl = $"https://ardsandnorthdownbincalendar.azurewebsites.net/api/collectiondates/{address.Uid}";

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
				// Parse response content as JSON object
				using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
				var rawBinDaysObject = jsonDoc.RootElement;

				// Iterate through all bin type keys and get associated collection date
				var binDays = new List<BinDay>();

				foreach (var week in new[] { "lastWeek", "thisWeek", "nextWeek" })
				{
					foreach (var dayEntry in rawBinDaysObject.GetProperty(week).EnumerateArray())
					{
						var collectionDate = dayEntry.GetProperty("date").GetString();
						ArgumentNullException.ThrowIfNull(collectionDate);

						// Parse the date (e.g. "2024-07-29T00:00:00")
						var date = DateOnly.ParseExact(
							collectionDate,
							"yyyy-MM-ddTHH:mm:ss",
							CultureInfo.InvariantCulture,
							DateTimeStyles.None
						);

						var binsForDay = new List<Bin>();
						foreach (var binEntry in dayEntry.GetProperty("bins").EnumerateArray())
						{
							var keyVal = binEntry.GetProperty("name").GetString();
							ArgumentNullException.ThrowIfNull(keyVal);

							var binType = _binTypes.Single(b => b.Keys.Contains(keyVal));
							binsForDay.Add(binType);
						}

						if (binsForDay.Count != 0)
						{
							var binDay = new BinDay
							{
								Date = date,
								Address = address,
								Bins = [.. binsForDay],
							};

							binDays.Add(binDay);
						}
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
