namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Collector implementation for Cheshire West and Chester Council.
/// </summary>
internal sealed class CheshireWestAndChesterCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Cheshire West and Chester Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.cheshirewestandchester.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "cheshire-west-and-chester";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Domestic" ],
		},
		new()
		{
			Name = "Paper, Card & Cartons Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Blue" ],
		},
		new()
		{
			Name = "Plastic, Cans & Glass Recycling",
			Colour = BinColour.Red,
			Keys = [ "Red" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Empty Recycling" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Brown,
			Keys = [ "Food", "Caddy" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Garden" ],
		},
	];

	/// <summary>
	/// The base URL of the council's self-service site.
	/// </summary>
	private const string _baseUrl = "https://my.cheshirewestandchester.gov.uk";

	/// <summary>
	/// The rounds where the council matches collections to a property's containers by task rather than by round.
	/// </summary>
	private static readonly IReadOnlyCollection<string> _taskMatchedRounds = ["EastTwin08", "WestTwin12"];

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting the session
		if (clientSideResponse == null)
		{
			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = CreateSessionRequest(),
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for getting addresses
		else if (clientSideResponse.RequestId == 1)
		{
			var requestBody = $$"""
			{
				"formValues": {
					"Section 1": {
						"postcode_search": { "value": "{{postcode}}" }
					}
				}
			}
			""";

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = CreateLookupRequest(2, "580f214c7f5b5", requestBody, GetSessionMetadata(clientSideResponse)),
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 2)
		{
			var rawAddresses = GetRows(clientSideResponse.Content);

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var rawAddress in rawAddresses)
			{
				var address = new Address
				{
					Property = rawAddress.GetProperty("display").GetString()!.Trim(),
					Postcode = postcode,
					Uid = rawAddress.GetProperty("uprn").GetString()!,
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
		// Prepare client-side request for getting the session
		if (clientSideResponse == null)
		{
			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = CreateSessionRequest(),
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting the waste system token
		else if (clientSideResponse.RequestId == 1)
		{
			var requestBody = """
			{
				"formValues": {
					"Section 1": {}
				}
			}
			""";

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = CreateLookupRequest(2, "609b918c7dd6d", requestBody, GetSessionMetadata(clientSideResponse)),
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting the property's containers
		else if (clientSideResponse.RequestId == 2)
		{
			var token = GetRows(clientSideResponse.Content)[0].GetProperty("AuthenticateResponse").GetString()!;

			var metadata = new Dictionary<string, string>(clientSideResponse.Options.Metadata)
			{
				{ "token", token },
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = CreateLookupRequest(3, "6101d1a29ba09", CreatePropertyRequestBody(token, address), metadata),
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting the property's collections
		else if (clientSideResponse.RequestId == 3)
		{
			var rawContainers = GetRows(clientSideResponse.Content);

			// Iterate through each container, and keep its task and round (one per line, as "task|round")
			var containers = new List<string>();
			foreach (var rawContainer in rawContainers)
			{
				if (rawContainer.GetProperty("type").GetString()! != "WASTE CONTAINER")
				{
					continue;
				}

				containers.Add($"{rawContainer.GetProperty("serviceType").GetString()!}|{rawContainer.GetProperty("Round").GetString()!}");
			}

			var metadata = new Dictionary<string, string>(clientSideResponse.Options.Metadata)
			{
				{ "containers", string.Join('\n', containers) },
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = CreateLookupRequest(4, "6101d23110243", CreatePropertyRequestBody(metadata["token"], address), metadata),
			};

			return getBinDaysResponse;
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 4)
		{
			var containers = clientSideResponse.Options.Metadata["containers"].Split('\n', StringSplitOptions.RemoveEmptyEntries);
			var rawBinDays = GetRows(clientSideResponse.Content);

			// The council matches collections to containers by task on some rounds, and by round on all others
			var matchByTask = false;
			foreach (var container in containers)
			{
				if (_taskMatchedRounds.Contains(container.Split('|')[1]))
				{
					matchByTask = true;
				}
			}

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var rawBinDay in rawBinDays)
			{
				var service = rawBinDay.GetProperty("serviceType").GetString()!;
				var task = rawBinDay.GetProperty("taskName").GetString()!;
				var round = rawBinDay.GetProperty("round").GetString()!;

				// The collections include those of other rounds and enquiries, which the council does not show,
				// so only keep the collections which are for one of the property's containers
				var isForContainer = false;
				foreach (var container in containers)
				{
					var containerParts = container.Split('|');

					if (matchByTask ? task == containerParts[0] : round.Contains(containerParts[1]))
					{
						isForContainer = true;
					}
				}

				if (!isForContainer)
				{
					continue;
				}

				var date = DateUtilities.ParseDateExact(rawBinDay.GetProperty("collectionDateTime").GetString()!, "yyyy-MM-dd'T'HH:mm:ss");

				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, service);

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

	/// <summary>
	/// Creates the client-side request for getting the session.
	/// </summary>
	private static ClientSideRequest CreateSessionRequest()
	{
		var clientSideRequest = new ClientSideRequest
		{
			RequestId = 1,
			Url = $"{_baseUrl}/authapi/isauthenticated",
			Method = "GET",
		};

		return clientSideRequest;
	}

	/// <summary>
	/// Gets the session identifier and cookies from the session response, as metadata for the lookup requests.
	/// </summary>
	private static Dictionary<string, string> GetSessionMetadata(ClientSideResponse clientSideResponse)
	{
		using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);

		var metadata = new Dictionary<string, string>
		{
			{ "sid", jsonDoc.RootElement.GetProperty("auth-session").GetString()! },
			{ "cookie", ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]) },
		};

		return metadata;
	}

	/// <summary>
	/// Creates the client-side request for a lookup, carrying the metadata forward.
	/// </summary>
	private static ClientSideRequest CreateLookupRequest(int requestId, string lookupId, string requestBody, Dictionary<string, string> metadata)
	{
		var clientSideRequest = new ClientSideRequest
		{
			RequestId = requestId,
			Url = $"{_baseUrl}/apibroker/runLookup?id={lookupId}&sid={metadata["sid"]}",
			Method = "POST",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
				{ "content-type", Constants.ApplicationJson },
				{ "cookie", metadata["cookie"] },
			},
			Body = requestBody,
			Options = new ClientSideOptions
			{
				Metadata = metadata,
			},
		};

		return clientSideRequest;
	}

	/// <summary>
	/// Creates the request body for the lookups of a property's containers and collections.
	/// </summary>
	private static string CreatePropertyRequestBody(string token, Address address)
	{
		var requestBody = $$"""
		{
			"formValues": {
				"Section 1": {
					"AuthenticateResponse": { "value": "{{token}}" },
					"UPRN": { "value": "{{address.Uid}}" }
				}
			}
		}
		""";

		return requestBody;
	}

	/// <summary>
	/// Gets the rows from a lookup response, which are an object keyed by row number, or an empty array when there are none.
	/// </summary>
	private static List<JsonElement> GetRows(string content)
	{
		using var jsonDoc = JsonDocument.Parse(content);
		var rowsData = jsonDoc.RootElement.GetProperty("integration").GetProperty("transformed").GetProperty("rows_data");

		var rows = new List<JsonElement>();
		if (rowsData.ValueKind == JsonValueKind.Object)
		{
			foreach (var row in rowsData.EnumerateObject())
			{
				rows.Add(row.Value.Clone());
			}
		}

		return rows;
	}
}
