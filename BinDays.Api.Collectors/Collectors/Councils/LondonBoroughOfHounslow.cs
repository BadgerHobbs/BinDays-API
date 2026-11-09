namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for London Borough of Hounslow.
/// </summary>
internal sealed partial class LondonBoroughOfHounslow : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "London Borough of Hounslow";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.hounslow.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "hounslow";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	/// <remarks>
	/// Keys match "{jobType}/{jobName}", e.g. "Residual/Empty Bin Residual240" or "Recycling/Empty RecyclingBlue".
	/// </remarks>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Residual/" ],
		},
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Purple,
			Keys = [ "ResidualPurpleSack/" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Paper & Card Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Empty RecyclingBlue", "Empty Bin RecyclingBlue" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Plastics & Cans Recycling",
			Colour = BinColour.Red,
			Keys = [ "Empty RecyclingRed", "Empty Bin RecyclingRed" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Glass Recycling",
			Colour = BinColour.Green,
			Keys = [ "Empty RecyclingGreen", "Empty Bin RecyclingGreen" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Green,
			Keys = [ "Food/" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden/" ],
		},
		new()
		{
			Name = "Communal Paper & Card Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Cardboard" ],
			Type = BinType.Container,
		},
		new()
		{
			Name = "Communal Plastics & Cans Recycling",
			Colour = BinColour.Red,
			Keys = [ "Plastic" ],
			Type = BinType.Container,
		},
		new()
		{
			Name = "Communal Mixed Recycling",
			Colour = BinColour.Grey,
			Keys = [ "Unknown/Empty Bin Recycling" ],
			Type = BinType.Container,
		},
	];

	/// <summary>
	/// The URL of the waste and recycling collections service, used to start a session.
	/// </summary>
	private const string _sessionUrl = "https://my.hounslow.gov.uk/en/service/Waste_and_recycling_collections";

	/// <summary>
	/// The URL of the AchieveForms lookup endpoint.
	/// </summary>
	private const string _lookupUrl = "https://my.hounslow.gov.uk/apibroker/runLookup";

	/// <summary>
	/// Regex for the Bartec authentication token from the token lookup response.
	/// </summary>
	[GeneratedRegex(@"<result column=""bartecToken""[^>]*>(?<token>[^<]+)</result>")]
	private static partial Regex TokenRegex();

	/// <summary>
	/// Regex for the JSON list of jobs from the jobs lookup response.
	/// </summary>
	[GeneratedRegex(@"<result column=""jobsJSON""[^>]*>(?<jobs>.*?)</result>", RegexOptions.Singleline)]
	private static partial Regex JobsRegex();

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
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);

			var requestBody = $$"""
			{
				"formValues": {
					"Section 1": {
						"searchPostcode": { "value": "{{postcode}}" }
					}
				}
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_lookupUrl}?id=670cc7533bf71",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.ApplicationJson },
					{ "cookie", cookies },
				},
				Body = requestBody,
			};

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
					Property = rawAddress.GetProperty("label").GetString()!,
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
		// Prepare client-side request for getting the Bartec authentication token
		else if (clientSideResponse.RequestId == 1)
		{
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);

			var requestBody = """
			{
				"formValues": {}
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_lookupUrl}?id=655f4290810cf",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.ApplicationJson },
					{ "cookie", cookies },
				},
				Body = requestBody,
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "cookie", cookies },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting the scheduled jobs
		else if (clientSideResponse.RequestId == 2)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var xmlData = jsonDoc.RootElement.GetProperty("data").GetString()!;
			var token = TokenRegex().Match(xmlData).Groups["token"].Value;

			var cookies = clientSideResponse.Options.Metadata["cookie"];

			// Same three week window ahead that the council's form uses
			var today = DateOnly.FromDateTime(DateTime.Today);
			var dateFrom = today.ToString("yyyy-MM-dd");
			var dateTo = today.AddDays(21).ToString("yyyy-MM-dd");

			var requestBody = $$"""
			{
				"formValues": {
					"Section 1": {
						"bartecToken": { "value": "{{token}}" },
						"searchUPRN": { "value": "{{address.Uid}}" },
						"searchFromDate": { "value": "{{dateFrom}}" },
						"searchToDate": { "value": "{{dateTo}}" }
					}
				}
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = $"{_lookupUrl}?id=659eb39b66d5a",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.ApplicationJson },
					{ "cookie", cookies },
				},
				Body = requestBody,
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 3)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var xmlData = jsonDoc.RootElement.GetProperty("data").GetString()!;
			var jobsJson = JobsRegex().Match(xmlData).Groups["jobs"].Value;

			using var jobsDoc = JsonDocument.Parse(jobsJson);

			// Iterate through each job, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var job in jobsDoc.RootElement.EnumerateArray())
			{
				var jobType = job.GetProperty("jobType").GetString()!;
				var jobName = job.GetProperty("jobName").GetString()!;
				var date = DateUtilities.ParseDateExact(job.GetProperty("jobDate").GetString()!, "yyyy-MM-dd");

				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = ProcessingUtilities.GetMatchingBins(_binTypes, $"{jobType}/{jobName}"),
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
}
