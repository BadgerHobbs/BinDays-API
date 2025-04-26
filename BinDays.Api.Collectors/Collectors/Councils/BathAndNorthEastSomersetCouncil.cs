namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Models;
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Globalization;
	using System.Linq;
	using System.Text.Json;

	/// <summary>
	/// Collector implementation for Bath and North East Somerset Council.
	/// </summary>
	internal sealed partial class BathAndNorthEastSomersetCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "Bath and North East Somerset Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://www.bathnes.gov.uk/webforms/waste/collectionday/");

		/// <inheritdoc/>
		public override string GovUkId => "bath-and-north-east-somerset";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly ReadOnlyCollection<Bin> binTypes = new List<Bin>()
		{
			new()
			{
				Name = "General Waste",
				Colour = "Black",
				Keys = new List<string>() { "residualNextDate" }.AsReadOnly(),
			},
			new()
			{
				Name = "Food Waste",
				Colour = "Grey",
				Keys = new List<string>() { "foodNextDate" }.AsReadOnly(),
			},
			new()
			{
				Name = "Card & Brown Paper",
				Colour = "Blue",
				Keys = new List<string>() { "recyclingNextDate" }.AsReadOnly(),
				Type = "Bag",
			},
			new()
			{
				Name = "Metal, Glass, Paper & Plastic",
				Colour = "Green",
				Keys = new List<string>() { "recyclingNextDate" }.AsReadOnly(),
				Type = "Box",
			},
			new()
			{
				Name = "Garden Waste",
				Colour = "Green",
				Keys = new List<string>() { "organicNextDate" }.AsReadOnly(),
			},
		}.AsReadOnly();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting addresses
			if (clientSideResponse == null)
			{
				var requestUrl = $"https://www.bathnes.gov.uk/webapi/api/AddressesAPI/v2/search/{Uri.EscapeDataString(postcode)}/150/true";

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = requestUrl,
					Method = "GET",
					Headers = [],
					Body = string.Empty,
				};

				var getAddressesResponse = new GetAddressesResponse()
				{
					Addresses = null,
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
				foreach (var addressElement in jsonDoc.RootElement.EnumerateArray())
				{
					string? fullAddressStr = addressElement.TryGetProperty("full_Address", out var fullAddressProp) ? fullAddressProp.GetString() : null;
					string? streetNameStr = addressElement.TryGetProperty("street_Name", out var streetNameProp) ? streetNameProp.GetString() : null;
					string? townStr = addressElement.TryGetProperty("town", out var townProp) ? townProp.GetString() : null;
					string? uprnStr = addressElement.TryGetProperty("uprn", out var uprnProp) ? uprnProp.ToString() : null; // UPRN might be number or string

					string? property = null;
					string? street = streetNameStr?.Trim();
					string? town = townStr?.Trim();
					string? uid = uprnStr?.Split('.')[0]; // Match Dart's uid extraction (removes potential decimals)

					if (!string.IsNullOrWhiteSpace(fullAddressStr))
					{
						fullAddressStr = fullAddressStr.Trim();
						if (!string.IsNullOrWhiteSpace(streetNameStr))
						{
							streetNameStr = streetNameStr.Trim();
							// Find the street name within the full address to extract the property part
							int index = fullAddressStr.IndexOf(streetNameStr, StringComparison.OrdinalIgnoreCase);
							if (index > 0) // Found street name, and it's not at the very beginning
							{
								property = fullAddressStr[..index].TrimEnd([' ', ',']); // Trim trailing space/comma
							}
							else if (index == -1) // Street name not found in full address
							{
								// Fallback: use the full address as the property if street name isn't found within it
								property = fullAddressStr;
							}
							// If index is 0, property remains null (address starts with street)
						}
						else // Street name is null/empty
						{
							// Fallback: use the full address as property if street name is missing
							property = fullAddressStr;
						}
					}

					// Ensure property isn't just whitespace
					if (string.IsNullOrWhiteSpace(property))
					{
						property = null;
					}

					// Only add address if UID is present
					if (!string.IsNullOrEmpty(uid))
					{
						var address = new Address()
						{
							Property = property,
							Street = street,
							Town = town,
							Postcode = postcode,
							Uid = uid,
						};
						addresses.Add(address);
					}
				}

				var getAddressesResponse = new GetAddressesResponse()
				{
					Addresses = addresses.AsReadOnly(),
					NextClientSideRequest = null
				};

				return getAddressesResponse;
			}

			// Throw exception for invalid request
			throw new InvalidOperationException($"Invalid client-side request.");
		}

		/// <inheritdoc/>
		public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting bin days
			if (clientSideResponse == null)
			{
				var requestUrl = $"https://www.bathnes.gov.uk/webapi/api/BinsAPI/v2/getbartecroute/{address.Uid}/true";

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = requestUrl,
					Method = "GET",
					Headers = [],
					Body = string.Empty,
				};

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					BinDays = null,
					NextClientSideRequest = clientSideRequest
				};

				return getBinDaysResponse;
			}
			// Process bin days from response
			else if (clientSideResponse.RequestId == 1)
			{
				var aggregatedBinDays = new Dictionary<DateOnly, List<Bin>>();

				// Parse response content as JSON object
				using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
				var rawBinDaysObject = jsonDoc.RootElement;

				// Iterate through each property (collection type key and date value)
				foreach (var property in rawBinDaysObject.EnumerateObject())
				{
					var dateKey = property.Name;
					string? dateValue = null;

					// Check the type of the JSON value before getting it as a string
					if (property.Value.ValueKind == JsonValueKind.String)
					{
						dateValue = property.Value.GetString();
					}

					// Skip if date value is null, empty, or wasn't a string
					if (string.IsNullOrEmpty(dateValue))
					{
						continue;
					}

					// Try parsing the date string (e.g., "2024-07-29T00:00:00")
					// We only care about the date part.
					if (!DateTime.TryParseExact(dateValue, "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var collectionDateTime))
					{
						// Skip if date parsing fails
						continue;
					}
					var collectionDate = DateOnly.FromDateTime(collectionDateTime);

					// Find all matching bin types based on the date key
					var matchedBins = this.binTypes.Where(bin => bin.Keys.Contains(dateKey));

					// Aggregate bins by date
					foreach (var binType in matchedBins)
					{
						if (!aggregatedBinDays.TryGetValue(collectionDate, out var binsForDate))
						{
							binsForDate = [];
							aggregatedBinDays[collectionDate] = binsForDate;
						}

						// Add bin type if it's not already in the list for this date
						if (!binsForDate.Any(b => b.Name == binType.Name && b.Colour == binType.Colour))
						{
							binsForDate.Add(binType);
						}
					}
				}

				// Create BinDay objects from the aggregated data, ordered by date
				var binDays = aggregatedBinDays
					.Select(kvp => new BinDay()
					{
						Date = kvp.Key,
						Address = address,
						Bins = kvp.Value.AsReadOnly()
					})
					.OrderBy(bd => bd.Date)
					.ToList();

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					BinDays = binDays.AsReadOnly(),
					NextClientSideRequest = null
				};

				return getBinDaysResponse;
			}

			// Throw exception for invalid request
			throw new InvalidOperationException($"Invalid client-side request.");
		}
	}
}