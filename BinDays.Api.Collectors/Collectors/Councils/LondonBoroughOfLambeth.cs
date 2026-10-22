namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Collector implementation for London Borough of Lambeth.
/// </summary>
internal sealed class LondonBoroughOfLambeth : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "London Borough of Lambeth";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.lambeth.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "lambeth";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	/// <remarks>
	/// Keys are matched against the service description followed by the container's display phrase,
	/// e.g. "Domestic Refuse Collection Service (black bin)", as the same service can use bins, bulk bins or sacks.
	/// </remarks>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Refuse Collection Service (black bin)" ],
		},
		new()
		{
			Name = "Communal General Waste",
			Colour = BinColour.Black,
			Keys = [ "Refuse Collection Service (bulk bin)" ],
			Type = BinType.Container,
		},
		new()
		{
			Name = "General Waste Sacks",
			Colour = BinColour.Black,
			Keys = [ "Refuse Collection Service (waste sacks)" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Green,
			Keys = [ "Recycling Collection Service (green bin)" ],
		},
		new()
		{
			Name = "Mixed Recycling Sacks",
			Colour = BinColour.Clear,
			Keys = [ "Recycling Collection Service (recycling sacks)" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Brown,
			Keys = [ "Food Collection Service (brown bin)" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Garden Collection Service (garden waste sacks)" ],
			Type = BinType.Sack,
		},
	];

	/// <summary>
	/// The base URL for the council's waste services.
	/// </summary>
	private const string _baseUrl = "https://wasteservice.lambeth.gov.uk";

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}/LLPG/addressSearch",
				Method = "POST",
				Body = $$"""
				{
					"postcode": "{{postcode}}",
					"ignorePostal": true
				}
				""",
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 1)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var results = jsonDoc.RootElement.GetProperty("payload").GetProperty("results");

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var result in results.EnumerateArray())
			{
				// Skip addresses without a location, as the council's own lookup does
				if (string.IsNullOrEmpty(result.GetProperty("LATITUDE").GetString()))
				{
					continue;
				}

				var secondary = JoinNonEmpty(" ", result.GetProperty("SAO_TEXT").GetString()!, $"{result.GetProperty("SAO_START_NUMBER").GetString()}{result.GetProperty("SAO_START_SUFFIX").GetString()}");
				var primaryNumber = JoinNonEmpty("-", $"{result.GetProperty("PAO_START_NUMBER").GetString()}{result.GetProperty("PAO_START_SUFFIX").GetString()}", result.GetProperty("PAO_END_NUMBER").GetString()!);

				var property = JoinNonEmpty(
					", ",
					JoinNonEmpty(" ", secondary, result.GetProperty("PAO_TEXT").GetString()!),
					JoinNonEmpty(" ", primaryNumber, result.GetProperty("STREET").GetString()!)
				);

				var address = new Address
				{
					Property = property,
					Postcode = postcode,
					Uid = result.GetProperty("UPRN").GetString()!,
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
		// Prepare client-side request for getting the parent property, which holds communal and shared services for flats
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}/WhitespaceComms/GetParentUPRN",
				Method = "POST",
				Body = $$"""
				{
					"uprn": "{{address.Uid}}"
				}
				""",
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting the property's services
		else if (clientSideResponse.RequestId == 1)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var parent = jsonDoc.RootElement.GetProperty("parent");

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseUrl}/WhitespaceComms/GetServicesByUprn",
				Method = "POST",
				Body = $$"""
				{
					"uprn": "{{address.Uid}}"
				}
				""",
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "parent", parent.ValueKind == JsonValueKind.Null ? string.Empty : parent.GetRawText() },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin days from the property's services, or prepare client-side request for getting the parent property's services
		else if (clientSideResponse.RequestId == 2)
		{
			var parent = clientSideResponse.Options.Metadata["parent"];

			if (parent == string.Empty)
			{
				var getBinDaysResponse = new GetBinDaysResponse
				{
					BinDays = CreateBinDays(address, GetServices(clientSideResponse.Content)),
				};

				return getBinDaysResponse;
			}

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = $"{_baseUrl}/WhitespaceComms/GetServicesByUprn",
				Method = "POST",
				Body = $$"""
				{
					"uprn": "{{parent}}"
				}
				""",
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "services", clientSideResponse.Content },
					},
				},
			};

			var getBinDaysResponseWithRequest = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponseWithRequest;
		}
		// Process bin days from the property's and parent property's services
		else if (clientSideResponse.RequestId == 3)
		{
			// The property's own services take precedence over the parent property's equivalent services
			var services = GetServices(clientSideResponse.Options.Metadata["services"]);
			var serviceIds = services.Select(service => service.ServiceId).ToHashSet();

			services.AddRange(GetServices(clientSideResponse.Content).Where(service => !serviceIds.Contains(service.ServiceId)));

			var getBinDaysResponse = new GetBinDaysResponse
			{
				BinDays = CreateBinDays(address, services),
			};

			return getBinDaysResponse;
		}

		// Throw exception for invalid request
		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <summary>
	/// Gets the current collection services with a next collection date from a services response.
	/// </summary>
	private static List<(int ServiceId, string Service, string Date)> GetServices(string content)
	{
		using var jsonDoc = JsonDocument.Parse(content);
		var siteServices = jsonDoc.RootElement.GetProperty("SiteServices");

		// Iterate through each service, and keep the current collection services
		var services = new List<(int ServiceId, string Service, string Date)>();
		foreach (var siteService in siteServices.EnumerateArray())
		{
			var description = siteService.GetProperty("ServiceDescription").GetString()!;
			var container = siteService.GetProperty("Container");
			var nextCollectionDate = siteService.GetProperty("NextCollectionDate").GetString()!;
			var validTo = siteService.GetProperty("SiteServiceValidTo").GetString()!;

			// Skip street cleaning, deliveries and other services that are not bin collections
			if (!description.Contains("Collection Service") || container.ValueKind == JsonValueKind.Null || nextCollectionDate == string.Empty)
			{
				continue;
			}

			// Skip ended services, such as lapsed garden waste subscriptions ("01/01/0001" means no end date)
			if (validTo != "01/01/0001" && DateUtilities.ParseDateExact(validTo, "dd/MM/yyyy") < DateOnly.FromDateTime(DateTime.Today))
			{
				continue;
			}

			services.Add((
				siteService.GetProperty("ServiceID").GetInt32(),
				$"{description} ({container.GetProperty("DisplayPhrase").GetString()})",
				nextCollectionDate
			));
		}

		return services;
	}

	/// <summary>
	/// Creates the bin days from the collection services.
	/// </summary>
	private IReadOnlyCollection<BinDay> CreateBinDays(Address address, List<(int ServiceId, string Service, string Date)> services)
	{
		// Iterate through each service, and create a new bin day object
		var binDays = new List<BinDay>();
		foreach (var (_, service, date) in services)
		{
			var binDay = new BinDay
			{
				Date = DateUtilities.ParseDateExact(date, "dd/MM/yyyy"),
				Address = address,
				Bins = ProcessingUtilities.GetMatchingBins(_binTypes, service),
			};

			binDays.Add(binDay);
		}

		return ProcessingUtilities.ProcessBinDays(binDays);
	}

	/// <summary>
	/// Joins the non-empty values with the separator.
	/// </summary>
	private static string JoinNonEmpty(string separator, params string[] values)
	{
		return string.Join(separator, values.Where(value => !string.IsNullOrWhiteSpace(value)));
	}
}
