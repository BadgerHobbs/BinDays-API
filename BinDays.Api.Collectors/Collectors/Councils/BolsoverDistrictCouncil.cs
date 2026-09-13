namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Exceptions;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

/// <summary>
/// Collector implementation for Bolsover District Council.
/// </summary>
internal sealed partial class BolsoverDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Bolsover District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.bolsover.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "bolsover";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Black" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Red,
			Keys = [ "Burgundy" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Green" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Brown,
			Keys = [ "Brown" ],
			Type = BinType.Caddy,
		},
	];

	/// <summary>
	/// The base URL for the council's self-service portal.
	/// </summary>
	private const string _baseUrl = "https://selfservice.bolsover.gov.uk";

	/// <summary>
	/// Regex to extract the session identifier (sid) from HTML.
	/// </summary>
	[GeneratedRegex(@"sid=(?<sid>[a-f0-9]+)")]
	private static partial Regex SidRegex();

	/// <summary>
	/// Regex to extract the garden waste winter suspension start date from the stage definition.
	/// </summary>
	[GeneratedRegex(@"""dataName"":""greenSusStart"".*?""defaultValue"":""(?<date>[^""]+)""")]
	private static partial Regex GreenWasteSuspensionStartRegex();

	/// <summary>
	/// Regex to extract the garden waste winter suspension end date from the stage definition.
	/// </summary>
	[GeneratedRegex(@"""dataName"":""greenSusEnd"".*?""defaultValue"":""(?<date>[^""]+)""")]
	private static partial Regex GreenWasteSuspensionEndRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for starting the session
		if (clientSideResponse == null)
		{
			var clientSideRequest = CreateInitialRequest();

			var response = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return response;
		}
		// Fetch reference and session id
		else if (clientSideResponse.RequestId == 1)
		{
			var nextClientSideRequest = CreateNextRefRequest(clientSideResponse);

			var response = new GetAddressesResponse
			{
				NextClientSideRequest = nextClientSideRequest,
			};

			return response;
		}
		// Request addresses for the supplied postcode
		else if (clientSideResponse.RequestId == 2)
		{
			var metadata = BuildMetadataWithReference(clientSideResponse);
			var requestBody = $$"""
			{
				"formValues": {
					"Section 1": {
						"postcode_search": { "value": "{{postcode}}" }
					}
				},
				"reference": "{{metadata["reference"]}}"
			}
			""";

			var clientSideRequest = CreateLookupRequest(3, "6058a5f55dc2e", requestBody, metadata);

			var response = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return response;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 3)
		{
			var rows = ParseLookupRows(clientSideResponse);

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var row in rows)
			{
				var results = row.Elements("result").ToDictionary(result => result.Attribute("column")!.Value, result => result.Value);
				var uprn = results["uprn"].Trim();
				var display = results["display"].Trim();

				var address = new Address
				{
					Property = display,
					Postcode = postcode,
					Uid = uprn,
				};

				addresses.Add(address);
			}

			var response = new GetAddressesResponse
			{
				Addresses = [.. addresses],
			};

			return response;
		}

		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <inheritdoc/>
	public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for starting the session
		if (clientSideResponse == null)
		{
			var clientSideRequest = CreateInitialRequest();

			var response = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return response;
		}
		// Fetch reference and session id
		else if (clientSideResponse.RequestId == 1)
		{
			var nextClientSideRequest = CreateNextRefRequest(clientSideResponse);

			var response = new GetBinDaysResponse
			{
				NextClientSideRequest = nextClientSideRequest,
			};

			return response;
		}
		// Request the currently published garden waste suspension window
		else if (clientSideResponse.RequestId == 2)
		{
			var metadata = BuildMetadataWithReference(clientSideResponse);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = $"{_baseUrl}/api/get-document/json?uri=sandbox-publish://AF-Process-d058fcb0-ae09-4b98-a33a-4f74ccd078a8/AF-Stage-bbf14e7c-f21b-45de-b793-72fd5988f34d/definition.json&sid={metadata["sid"]}",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "cookie", metadata["cookie"] },
					{ "x-requested-with", Constants.XmlHttpRequest },
				},
				Options = new ClientSideOptions
				{
					Metadata = metadata,
				},
			};

			var response = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return response;
		}
		// Request the collection route for the selected address
		else if (clientSideResponse.RequestId == 3)
		{
			var (suspensionStart, suspensionEnd) = ParseGreenWasteSuspensionWindow(clientSideResponse);

			Dictionary<string, string> metadata = new(clientSideResponse.Options.Metadata)
			{
				["gardenWasteSuspensionStart"] = suspensionStart,
				["gardenWasteSuspensionEnd"] = suspensionEnd,
			};
			var requestBody = $$"""
			{
				"formValues": {
					"Bin Collection": {
						"uprnLoggedIn": { "value": "{{address.Uid}}" }
					}
				},
				"reference": "{{metadata["reference"]}}"
			}
			""";

			var clientSideRequest = CreateLookupRequest(4, "6023d37e037c3", requestBody, metadata);

			var response = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return response;
		}
		// Request whether the next collection is a general waste (black bin) week
		else if (clientSideResponse.RequestId == 4)
		{
			var row = ParseLookupRows(clientSideResponse).SingleOrDefault()
				?? throw new BinDaysNotFoundException(GovUkId, address.Postcode!, address.Uid!);

			var route = row.Elements("result").Single(result => result.Attribute("column")!.Value == "Route").Value;
			if (route.Length < 2)
			{
				throw new InvalidOperationException($"Unrecognised collection route: {route}");
			}

			var today = DateOnly.FromDateTime(DateTime.UtcNow);
			var collectionDayOfWeek = route[..2] switch
			{
				"Mo" => DayOfWeek.Monday,
				"Tu" => DayOfWeek.Tuesday,
				"We" => DayOfWeek.Wednesday,
				"Th" => DayOfWeek.Thursday,
				"Fr" => DayOfWeek.Friday,
				_ => throw new InvalidOperationException($"Unrecognised collection route: {route}"),
			};
			var daysUntilCollection = ((int)collectionDayOfWeek - (int)today.DayOfWeek + 7) % 7;
			var firstCollectionDate = today.AddDays(daysUntilCollection);
			var week = ISOWeek.GetWeekOfYear(firstCollectionDate.ToDateTime(TimeOnly.MinValue));

			Dictionary<string, string> metadata = new(clientSideResponse.Options.Metadata)
			{
				["firstCollectionDate"] = firstCollectionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
			};
			var requestBody = $$"""
			{
				"formValues": {
					"Bin Collection": {
						"week": { "value": "{{week}}" },
						"Route": { "value": "{{route}}" },
						"year": { "value": "{{today.Year}}" }
					}
				},
				"reference": "{{metadata["reference"]}}"
			}
			""";

			var clientSideRequest = CreateLookupRequest(5, "6023d2785d11f", requestBody, metadata);

			var response = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return response;
		}
		// Process the collection rota into the next four collection dates
		else if (clientSideResponse.RequestId == 5)
		{
			var isFirstCollectionGeneralWaste = ParseLookupRows(clientSideResponse).Any();
			var metadata = clientSideResponse.Options.Metadata;
			var firstCollectionDate = DateUtilities.ParseDateExact(metadata["firstCollectionDate"], "yyyy-MM-dd");
			var gardenWasteSuspensionStart = DateUtilities.ParseDateExact(metadata["gardenWasteSuspensionStart"], "yyyy-MM-dd");
			var gardenWasteSuspensionEnd = DateUtilities.ParseDateExact(metadata["gardenWasteSuspensionEnd"], "yyyy-MM-dd");

			// The general waste (black bin) and recycling/garden (burgundy/green bin) collections
			// alternate weekly, with food waste (brown caddy) collected every week
			var binDays = new List<BinDay>();
			for (var week = 0; week < 4; week++)
			{
				var date = firstCollectionDate.AddDays(week * 7);
				var isGeneralWasteWeek = isFirstCollectionGeneralWaste == (week % 2 == 0);
				var isGardenWasteSuspended = date >= gardenWasteSuspensionStart && date < gardenWasteSuspensionEnd;

				var service = (isGeneralWasteWeek, isGardenWasteSuspended) switch
				{
					(true, _) => "Black & Brown",
					(false, true) => "Burgundy & Brown",
					(false, false) => "Burgundy, Green & Brown",
				};

				binDays.Add(new BinDay
				{
					Date = date,
					Address = address,
					Bins = ProcessingUtilities.GetMatchingBins(_binTypes, service),
				});
			}

			var response = new GetBinDaysResponse
			{
				BinDays = ProcessingUtilities.ProcessBinDays(binDays),
			};

			return response;
		}

		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <summary>
	/// Creates the initial client-side request used to start the session.
	/// </summary>
	private static ClientSideRequest CreateInitialRequest()
	{
		var clientSideRequest = new ClientSideRequest
		{
			RequestId = 1,
			Url = $"{_baseUrl}/service/Check_your_Bin_Day",
			Method = "GET",
		};

		return clientSideRequest;
	}

	/// <summary>
	/// Builds the metadata dictionary and prepares the next reference request.
	/// </summary>
	private static ClientSideRequest CreateNextRefRequest(ClientSideResponse clientSideResponse)
	{
		var setCookieHeader = clientSideResponse.Headers["set-cookie"];
		var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

		Dictionary<string, string> metadata = new()
		{
			{ "cookie", cookies },
			{ "sid", SidRegex().Match(clientSideResponse.Content).Groups["sid"].Value },
		};

		var clientSideRequest = new ClientSideRequest
		{
			RequestId = 2,
			Url = $"{_baseUrl}/api/nextref?sid={metadata["sid"]}",
			Method = "GET",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
				{ "cookie", metadata["cookie"] },
				{ "x-requested-with", Constants.XmlHttpRequest },
			},
			Options = new ClientSideOptions
			{
				Metadata = metadata,
			},
		};

		return clientSideRequest;
	}

	/// <summary>
	/// Adds the reference token to the metadata dictionary.
	/// </summary>
	private static Dictionary<string, string> BuildMetadataWithReference(ClientSideResponse clientSideResponse)
	{
		Dictionary<string, string> metadata = new(clientSideResponse.Options.Metadata);

		using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
		metadata["reference"] = jsonDoc.RootElement.GetProperty("data").GetProperty("reference").GetString()!;

		return metadata;
	}

	/// <summary>
	/// Extracts the currently published garden waste winter suspension window from the stage definition response.
	/// </summary>
	private static (string Start, string End) ParseGreenWasteSuspensionWindow(ClientSideResponse clientSideResponse)
	{
		using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
		var base64Content = jsonDoc.RootElement.GetProperty("data").GetProperty("content").GetString()!;
		var definitionJson = Encoding.UTF8.GetString(Convert.FromBase64String(base64Content));

		var start = GreenWasteSuspensionStartRegex().Match(definitionJson).Groups["date"].Value;
		var end = GreenWasteSuspensionEndRegex().Match(definitionJson).Groups["date"].Value;

		return (start, end);
	}

	/// <summary>
	/// Creates a client-side request for the AchieveForms lookup.
	/// </summary>
	private static ClientSideRequest CreateLookupRequest(
		int requestId,
		string lookupId,
		string requestBody,
		Dictionary<string, string> metadata)
	{
		var clientSideRequest = new ClientSideRequest
		{
			RequestId = requestId,
			Url = $"{_baseUrl}/apibroker/runLookup?id={lookupId}&app_name=AF-Renderer::Self&sid={metadata["sid"]}",
			Method = "POST",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
				{ "content-type", Constants.ApplicationJson },
				{ "cookie", metadata["cookie"] },
				{ "x-requested-with", Constants.XmlHttpRequest },
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
	/// Parses the XML rows out of an AchieveForms lookup response.
	/// </summary>
	private static IEnumerable<XElement> ParseLookupRows(ClientSideResponse clientSideResponse)
	{
		using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
		var xmlData = jsonDoc.RootElement.GetProperty("data").GetString()!;

		var xml = XDocument.Parse(xmlData);

		return xml.Descendants("Row");
	}
}
