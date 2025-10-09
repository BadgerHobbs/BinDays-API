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
				Colour = BinColour.Black,
				Keys = new List<string>() { "residualNextDate" }.AsReadOnly(),
			},
			new()
			{
				Name = "Food Waste",
				Colour = BinColour.Grey,
				Keys = new List<string>() { "recyclingNextDate" }.AsReadOnly(),
			},
			new()
			{
				Name = "Card & Brown Paper",
				Colour = BinColour.Blue,
				Keys = new List<string>() { "recyclingNextDate" }.AsReadOnly(),
				Type = BinType.Bag,
			},
			new()
			{
				Name = "Metal, Glass, Paper & Plastic",
				Colour = BinColour.Green,
				Keys = new List<string>() { "recyclingNextDate" }.AsReadOnly(),
				Type = BinType.Box,
			},
			new()
			{
				Name = "Garden Waste",
				Colour = BinColour.Green,
				Keys = new List<string>() { "organicNextDate" }.AsReadOnly(),
			},
		}.AsReadOnly();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting addresses
			if (clientSideResponse == null)
			{
				var requestUrl = $"https://www.bathnes.gov.uk/webapi/api/AddressesAPI/v2/search/{postcode}/150/true";

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
					string? property = addressElement.GetProperty("full_Address").ToString();
					string? uprn = addressElement.GetProperty("uprn").ToString().Split('.').First();

					var address = new Address()
					{
						Property = property?.Trim(),
						Street = string.Empty,
						Town = string.Empty,
						Postcode = postcode,
						Uid = uprn,
					};
					addresses.Add(address);
				}

				var getAddressesResponse = new GetAddressesResponse()
				{
					Addresses = addresses.AsReadOnly(),
					NextClientSideRequest = null
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
				// Parse response content as JSON object
				using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
				var rawBinDaysObject = jsonDoc.RootElement;

				// Iterate through all bin type keys and get associated collection date
				var binDays = new List<BinDay>();
				foreach (var binType in binTypes)
				{
					foreach (var key in binType.Keys)
					{
						var collectionDate = rawBinDaysObject.GetProperty(key).ToString();

						// Parse the date (e.g., "2024-07-29T00:00:00")
						var date = DateOnly.ParseExact(
							collectionDate,
							"yyyy-MM-ddTHH:mm:ss",
							CultureInfo.InvariantCulture,
							DateTimeStyles.None
						);

						var binDay = new BinDay()
						{
							Date = date,
							Address = address,
							Bins = new List<Bin>() { binType }.AsReadOnly(),
						};

						binDays.Add(binDay);
					}
				}

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					BinDays = ProcessingUtilities.ProcessBinDays(binDays),
					NextClientSideRequest = null
				};

				return getBinDaysResponse;
			}

			// Throw exception for invalid request
			throw new InvalidOperationException("Invalid client-side request.");
		}
	}
}
