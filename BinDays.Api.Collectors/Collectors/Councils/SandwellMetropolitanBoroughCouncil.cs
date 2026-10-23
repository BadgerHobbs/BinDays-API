namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Collector implementation for Sandwell Metropolitan Borough Council.
/// </summary>
internal sealed class SandwellMetropolitanBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Sandwell Metropolitan Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.sandwell.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "sandwell";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Grey,
			Keys = [ "Household Waste" ],
		},
		new()
		{
			Name = "Glass, Cans & Plastics",
			Colour = BinColour.Blue,
			Keys = [ "Mixed Dry Recycling" ],
		},
		new()
		{
			Name = "Paper & Card",
			Colour = BinColour.Blue,
			Keys = [ "Mixed Dry Recycling" ],
			Type = BinType.Sack,
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Brown,
			Keys = [ "Food Recycling" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Garden waste" ],
		},
	];

	/// <summary>
	/// The URL of the council's bin collection days form, used to start the session.
	/// </summary>
	private const string _sessionUrl = "https://my.sandwell.gov.uk/en/AchieveForms/?form_uri=sandbox-publish://AF-Process-ebaa26a2-393c-4a3c-84f5-e61564192a8a/AF-Stage-e4c2cb32-db55-4ff5-845c-8b27f87346c4/definition.json";

	/// <summary>
	/// The URL of the council's Achieve Forms lookups.
	/// </summary>
	private const string _lookupUrl = "https://my.sandwell.gov.uk/apibroker/runLookup";

	/// <summary>
	/// The collection lookups for each service (household waste, garden waste, recycling and food waste), which are queried in turn.
	/// </summary>
	private static readonly string[] _collectionLookupIds = ["686294de50729", "686295a88a750", "68629dd642423", "6863a78a1dd8e"];

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for starting the session
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _sessionUrl,
				Method = "GET",
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for getting addresses
		else if (clientSideResponse.RequestId == 1)
		{
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var clientSideRequest = CreateLookupRequest(2, "55dee71f20a9a", cookies, "postcode_search", postcode);

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 2)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var rawAddresses = jsonDoc.RootElement
				.GetProperty("integration")
				.GetProperty("transformed")
				.GetProperty("select_data");

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var rawAddress in rawAddresses.EnumerateArray())
			{
				var address = new Address
				{
					Property = rawAddress.GetProperty("label").GetString()!.Trim(),
					Postcode = postcode,
					Uid = rawAddress.GetProperty("value").GetString()!,
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
		// Prepare client-side request for starting the session
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _sessionUrl,
				Method = "GET",
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side requests for each service's collections in turn, collecting the rows as they are returned
		else if (clientSideResponse.RequestId >= 1 && clientSideResponse.RequestId <= _collectionLookupIds.Length)
		{
			string cookies;
			string collections;

			if (clientSideResponse.RequestId == 1)
			{
				var setCookieHeader = clientSideResponse.Headers["set-cookie"];
				cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);
				collections = string.Empty;
			}
			else
			{
				cookies = clientSideResponse.Options.Metadata["cookie"];
				collections = clientSideResponse.Options.Metadata["collections"] + GetCollections(clientSideResponse.Content);
			}

			var clientSideRequest = CreateLookupRequest(clientSideResponse.RequestId + 1, _collectionLookupIds[clientSideResponse.RequestId - 1], cookies, "Uprn", address.Uid!);
			clientSideRequest.Options.Metadata.Add("collections", collections);

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin days from the collected rows
		else if (clientSideResponse.RequestId == _collectionLookupIds.Length + 1)
		{
			var collections = clientSideResponse.Options.Metadata["collections"] + GetCollections(clientSideResponse.Content);

			// Iterate through each collection, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var collection in collections.Split('\n', StringSplitOptions.RemoveEmptyEntries))
			{
				var parts = collection.Split('|');

				var binDay = new BinDay
				{
					Date = DateUtilities.ParseDateExact(parts[1], "dd/MM/yyyy"),
					Address = address,
					Bins = ProcessingUtilities.GetMatchingBins(_binTypes, parts[0]),
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

	/// <summary>
	/// Creates a client-side request for an Achieve Forms lookup with a single field, keeping the session cookies in the metadata.
	/// </summary>
	private static ClientSideRequest CreateLookupRequest(int requestId, string lookupId, string cookies, string fieldName, string fieldValue)
	{
		var clientSideRequest = new ClientSideRequest
		{
			RequestId = requestId,
			Url = $"{_lookupUrl}?id={lookupId}",
			Method = "POST",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
				{ "content-type", Constants.ApplicationJson },
				{ "cookie", cookies },
			},
			Body = $$"""
			{
				"formValues": {
					"Property details": {
						"{{fieldName}}": { "value": "{{fieldValue}}" }
					}
				}
			}
			""",
			Options = new ClientSideOptions
			{
				Metadata =
				{
					{ "cookie", cookies },
				},
			},
		};

		return clientSideRequest;
	}

	/// <summary>
	/// Gets the collections from a collection lookup response, as "service|date" lines.
	/// </summary>
	private static string GetCollections(string content)
	{
		using var jsonDoc = JsonDocument.Parse(content);
		var rawCollections = jsonDoc.RootElement
			.GetProperty("integration")
			.GetProperty("transformed")
			.GetProperty("rows_data");

		// Services the property does not have return an empty array rather than an object of rows
		if (rawCollections.ValueKind != JsonValueKind.Object)
		{
			return string.Empty;
		}

		var collections = string.Empty;
		foreach (var rawCollection in rawCollections.EnumerateObject())
		{
			// Each service prefixes its columns (e.g. DWCollection and DWDate for household waste)
			var service = string.Empty;
			var date = string.Empty;
			foreach (var column in rawCollection.Value.EnumerateObject())
			{
				if (column.Name.EndsWith("Collection"))
				{
					service = column.Value.GetString()!;
				}
				else if (column.Name.EndsWith("Date"))
				{
					date = column.Value.GetString()!;
				}
			}

			// Properties without collections (e.g. businesses) return a "No Results" row without a date
			if (date == string.Empty)
			{
				continue;
			}

			collections += $"{service}|{date}\n";
		}

		return collections;
	}
}
