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
	/// Collector implementation for South Gloucestershire Council.
	/// </summary>
	internal sealed partial class SouthGloucestershireCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "South Gloucestershire Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://beta.southglos.gov.uk/waste-and-recycling-collection-dates/");

		/// <inheritdoc/>
		public override string GovUkId => "south-gloucestershire";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly ReadOnlyCollection<Bin> _binTypes = new List<Bin>()
		{
			new()
			{
				Name = "Recycling",
				Colour = BinColor.Green,
				Keys = new List<string>() { "C", "R" }.AsReadOnly(),
			},
			new()
			{
				Name = "Food Waste",
				Colour = BinColor.Grey,
				Keys = new List<string>() { "C", "R" }.AsReadOnly(),
			},
			new()
			{
				Name = "General Waste",
				Colour = BinColor.Black,
				Keys = new List<string>() { "R" }.AsReadOnly(),
			},
		}.AsReadOnly();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting addresses
			if (clientSideResponse == null)
			{
				var requestUrl = $"https://webapps.southglos.gov.uk/Webservices/SGC.RefuseCollectionService/RefuseCollectionService.svc/getAddresses/{postcode}";

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = requestUrl,
					Method = "GET",
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
					string? property = addressElement.GetProperty("Property").GetString();
					string? street = addressElement.GetProperty("Street").GetString();
					string? town = addressElement.GetProperty("Town").GetString();
					string? uprn = addressElement.GetProperty("Uprn").GetString();

					var address = new Address()
					{
						Property = property?.Trim(),
						Street = street?.Trim(),
						Town = town?.Trim(),
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
				var requestUrl = $"https://webapps.southglos.gov.uk/Webservices/SGC.RefuseCollectionService/RefuseCollectionService.svc/getCollections/{address.Uid}";

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = requestUrl,
					Method = "GET",
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

				// Parse response content as JSON object (within an array)
				using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
				var rawBinDaysObject = jsonDoc.RootElement[0];

				// Iterate through each property (collection type and date)
				var binDays = new List<BinDay>();
				foreach (var rawBinDay in rawBinDaysObject.EnumerateObject())
				{
					// Skip if name is 'CalendarName' or if date is empty
					if (rawBinDay.Name == "CalendarName" || rawBinDay.Value.GetString() == string.Empty)
					{
						continue;
					}

					// Parse the date (e.g. "15/04/2025")
					var date = DateOnly.ParseExact(
						rawBinDay.Value.GetString()!,
						"dd/MM/yyyy",
						CultureInfo.InvariantCulture,
						DateTimeStyles.None
					);

					// Find all matching bin types based on the date key
					var matchedBins = _binTypes.Where(bin => bin.Keys.Contains(rawBinDay.Name[0].ToString()));

					var binDay = new BinDay()
					{
						Date = date,
						Address = address,
						Bins = matchedBins.ToList().AsReadOnly()
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