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
	/// Collector implementation for Dorset Council.
	/// </summary>
	internal sealed partial class DorsetCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "Dorset Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://www.dorsetcouncil.gov.uk/my-local-information");

		/// <inheritdoc/>
		public override string GovUkId => "dorset";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly ReadOnlyCollection<Bin> _binTypes = new List<Bin>()
		{
			new()
			{
				Name = "Refuse",
				Colour = BinColor.Black,
				Keys = new List<string>() { "Refuse" }.AsReadOnly(),
			},
			new()
			{
				Name = "Recycling",
				Colour = BinColor.Green,
				Keys = new List<string>() { "Recycling" }.AsReadOnly(),
			},
			new()
			{
				Name = "Food Waste",
				Colour = BinColor.Brown,
				Keys = new List<string>() { "Food" }.AsReadOnly(),
				Type = BinType.Caddy
			},
			new()
			{
				Name = "Garden Waste",
				Colour = BinColor.Brown,
				Keys = new List<string>() { "Garden" }.AsReadOnly(),
			},
		}.AsReadOnly();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting addresses
			if (clientSideResponse == null)
			{
				var requestUrl = "https://www.dorsetcouncil.gov.uk/api/jsonws/invoke";

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = requestUrl,
					Method = "POST",
					Body = JsonSerializer.Serialize(new Dictionary<string, object>
					{
						["/placecube_digitalplace.addresscontext/search-address-by-postcode"] = new
						{
							companyId = "35001",
							postcode = postcode.Replace(" ", String.Empty),
							fallbackToNationalLookup = false
						}
					}),
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
				// Parse response content as JSON array
				using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);

				// Iterate through each address json, and create a new address object
				var addresses = new List<Address>();
				foreach (var addressElement in jsonDoc.RootElement.EnumerateArray())
				{
					var address = new Address()
					{
						Property = addressElement.GetProperty("fullAddress").GetString()!.Trim(),
						Street = null,
						Town = null,
						Postcode = postcode,
						Uid = addressElement.GetProperty("UPRN").GetString()!.Trim(),
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
			// Prepare client-side request for getting recycling bin days
			if (clientSideResponse == null)
			{
				var requestUrl = $"https://geoapi.dorsetcouncil.gov.uk/v1/Services/recyclingday/{address.Uid}";
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = requestUrl,
					Method = "GET",
					Headers = new Dictionary<string, string>()
					{
						{ "user-agent", Constants.UserAgent },
						{ "accept", "application/json" }
					},
					Body = null,
				};

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					BinDays = null,
					NextClientSideRequest = clientSideRequest
				};

				return getBinDaysResponse;
			}
			else if (clientSideResponse.RequestId == 1)
			{
				var requestUrl = $"https://geoapi.dorsetcouncil.gov.uk/v1/Services/refuseday/{address.Uid}";
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 2,
					Url = requestUrl,
					Method = "GET",
					Headers = new Dictionary<string, string>()
					{
						{ "user-agent", Constants.UserAgent },
						{ "accept", "application/json" }
					},
					Body = null,
					Options = new ClientSideOptions
					{
						Metadata = {
							{ "recyclingday", clientSideResponse.Content },
						}
					},
				};

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					BinDays = null,
					NextClientSideRequest = clientSideRequest

				};

				return getBinDaysResponse;
			}
			else if (clientSideResponse.RequestId == 2)
			{
				var metadata = clientSideResponse.Options.Metadata;
				metadata.Add("refuseday", clientSideResponse.Content);

				var requestUrl = $"https://geoapi.dorsetcouncil.gov.uk/v1/Services/foodwasteday/{address.Uid}";
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 3,
					Url = requestUrl,
					Method = "GET",
					Headers = new Dictionary<string, string>()
					{
						{ "user-agent", Constants.UserAgent },
						{ "accept", "application/json" }
					},
					Body = null,
					Options = new ClientSideOptions
					{
						Metadata = metadata
					},
				};

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					BinDays = null,
					NextClientSideRequest = clientSideRequest

				};

				return getBinDaysResponse;
			}
			else if (clientSideResponse.RequestId == 3)
			{
				var metadata = clientSideResponse.Options.Metadata;
				metadata.Add("foodwasteday", clientSideResponse.Content);

				var requestUrl = $"https://geoapi.dorsetcouncil.gov.uk/v1/Services/gardenwasteday/{address.Uid}";
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 4,
					Url = requestUrl,
					Method = "GET",
					Headers = new Dictionary<string, string>()
					{
						{ "user-agent", Constants.UserAgent },
						{ "accept", "application/json" }
					},
					Body = null,
					Options = new ClientSideOptions
					{
						Metadata = metadata
					},
				};

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					BinDays = null,
					NextClientSideRequest = clientSideRequest

				};

				return getBinDaysResponse;
			}
			// Process bin days from response
			else if (clientSideResponse.RequestId == 4)
			{
				clientSideResponse.Options.Metadata.Add("gardenwasteday", clientSideResponse.Content);

				var binDays = new List<BinDay>();
				foreach (var metadata in clientSideResponse.Options.Metadata)
				{
					using var jsonDoc = JsonDocument.Parse(metadata.Value);
					var resultsElement = jsonDoc.RootElement.GetProperty("values");
					foreach (var binTypeElement in resultsElement.EnumerateArray())
					{
						// Determine matching bin types from the description
						var dateEl = binTypeElement.GetProperty("dateNextVisit");
						var type = binTypeElement.GetProperty("type").GetString()!;
						var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, type);

						var date = DateOnly.ParseExact(
							dateEl.GetString()!,
							"yyyy-MM-dd",
							CultureInfo.InvariantCulture,
							DateTimeStyles.None
						);

						var binDay = new BinDay()
						{
							Date = date,
							Address = address,
							Bins = matchedBinTypes.ToList().AsReadOnly()
						};

						binDays.Add(binDay);
					}
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
