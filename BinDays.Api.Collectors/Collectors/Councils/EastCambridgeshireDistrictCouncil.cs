namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for East Cambridgeshire District Council.
/// </summary>
internal sealed partial class EastCambridgeshireDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "East Cambridgeshire District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://eastcambs-self.achieveservice.com/service/Check_your_waste_collection_day");

	/// <inheritdoc/>
	public override string GovUkId => "east-cambridgeshire";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "Household Waste",
			Colour = BinColour.Black,
			Keys = [ "RUBBISH BIN" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Blue,
			Keys = [ "RECYCLING BIN" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "GARDEN WASTE BIN" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Grey,
			Keys = [ "OUTDOOR FOOD CADDY" ],
			Type = BinType.Caddy,
		},
	];

	/// <summary>
	/// The URL for the AchieveForms waste collections calendar, used to obtain session cookies.
	/// </summary>
	private const string InitialUrl = "https://eastcambs-self.achieveservice.com/AchieveForms/?mode=fill&consentMessage=yes&form_uri=sandbox-publish://AF-Process-2c7575a6-0139-4555-9d8a-ab504a44d989/AF-Stage-94ee5097-94db-474d-bc7a-d1796e3ab83a/definition.json&process=1&process_uri=sandbox-processes://AF-Process-2c7575a6-0139-4555-9d8a-ab504a44d989&process_id=AF-Process-2c7575a6-0139-4555-9d8a-ab504a44d989";

	/// <summary>
	/// The AchieveForms form URI used in all API lookup request bodies.
	/// </summary>
	private const string FormUri = "sandbox-publish://AF-Process-2c7575a6-0139-4555-9d8a-ab504a44d989/AF-Stage-94ee5097-94db-474d-bc7a-d1796e3ab83a/definition.json";

	/// <summary>
	/// Regex for extracting the Bartec authentication token from the auth lookup response.
	/// </summary>
	[GeneratedRegex(@"<result column=""AuthenticateResponse""[^>]*>(?<token>[^<]+)<\/result>")]
	private static partial Regex AuthTokenRegex();

	/// <summary>
	/// Regex for parsing addresses from the XML response.
	/// </summary>
	[GeneratedRegex(@"<Row id=.*?<result column=""display"".*?>(?<address>.*?)<\/result>.*?<result column=""uprn"".*?>(?<uprn>\d+)<\/result>.*?<\/Row>")]
	private static partial Regex AddressesRegex();

	/// <summary>
	/// Regex for parsing bin collection data from the XML response.
	/// </summary>
	[GeneratedRegex(@"<result column=""name""[^>]*>(?<binType>[^<]+)<\/result><result column=""ScheduledStart""[^>]*>(?<date>[^<]+)<\/result>")]
	private static partial Regex BinDaysRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting session cookies
		if (clientSideResponse == null)
		{
			return new GetAddressesResponse
			{
				NextClientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = InitialUrl,
					Method = "GET",
				},
			};
		}
		// Prepare auth lookup request, stashing cookies in metadata
		else if (clientSideResponse.RequestId == 1)
		{
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);
			return new GetAddressesResponse
			{
				NextClientSideRequest = BuildAuthLookupRequest(cookies),
			};
		}
		// Prepare address lookup request using stashed cookies
		else if (clientSideResponse.RequestId == 2)
		{
			var cookies = clientSideResponse.Options.Metadata["cookie"];

			var requestBody = $$"""
			{
				"formValues": {
					"Section 1": {
						"PostcodeSearch": { "value": "{{postcode}}" }
					}
				},
				"isPublished": true,
				"formName": "Waste collections calendar",
				"formUri": "{{FormUri}}"
			}
			""";

			return new GetAddressesResponse
			{
				NextClientSideRequest = new ClientSideRequest
				{
					RequestId = 3,
					Url = "https://eastcambs-self.achieveservice.com/apibroker/runLookup?id=54915cbced788",
					Method = "POST",
					Headers = new()
					{
						{ "user-agent", Constants.UserAgent },
						{ "content-type", Constants.ApplicationJson },
						{ "cookie", cookies },
					},
					Body = requestBody,
				},
			};
		}
		// Parse addresses from XML embedded in JSON response
		else if (clientSideResponse.RequestId == 3)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var xmlData = jsonDoc.RootElement.GetProperty("data").GetString()!;

			var rawAddresses = AddressesRegex().Matches(xmlData)!;

			var addresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				addresses.Add(new Address
				{
					Property = rawAddress.Groups["address"].Value.Trim(),
					Uid = rawAddress.Groups["uprn"].Value,
					Postcode = postcode,
				});
			}

			return new GetAddressesResponse
			{
				Addresses = [.. addresses],
			};
		}

		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <inheritdoc/>
	public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting session cookies
		if (clientSideResponse == null)
		{
			return new GetBinDaysResponse
			{
				NextClientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = InitialUrl,
					Method = "GET",
				},
			};
		}
		// Prepare auth lookup request, stashing cookies in metadata
		else if (clientSideResponse.RequestId == 1)
		{
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);
			return new GetBinDaysResponse
			{
				NextClientSideRequest = BuildAuthLookupRequest(cookies),
			};
		}
		// Prepare bin day lookup request using auth token and stashed cookies
		else if (clientSideResponse.RequestId == 2)
		{
			using var authDoc = JsonDocument.Parse(clientSideResponse.Content);
			var authXml = authDoc.RootElement.GetProperty("data").GetString()!;
			var authToken = AuthTokenRegex().Match(authXml).Groups["token"].Value;
			var cookies = clientSideResponse.Options.Metadata["cookie"];

			var today = DateOnly.FromDateTime(DateTime.Today);
			var minDate = today.ToString("yyyy-MM-dd");
			var maxDate = today.AddDays(45).ToString("yyyy-MM-dd");

			var requestBody = $$"""
			{
				"formValues": {
					"Section 1": {
						"AuthenticateResponse": { "value": "{{authToken}}" },
						"selected_uprn": { "value": "{{address.Uid}}" },
						"MinimumDateForNextDates": { "value": "{{minDate}}" },
						"MaximumDateFormattedNext": { "value": "{{maxDate}}" }
					}
				},
				"isPublished": true,
				"formName": "Waste collections calendar",
				"formUri": "{{FormUri}}"
			}
			""";

			return new GetBinDaysResponse
			{
				NextClientSideRequest = new ClientSideRequest
				{
					RequestId = 3,
					Url = "https://eastcambs-self.achieveservice.com/apibroker/runLookup?id=6784e74793b68",
					Method = "POST",
					Headers = new()
					{
						{ "user-agent", Constants.UserAgent },
						{ "content-type", Constants.ApplicationJson },
						{ "cookie", cookies },
					},
					Body = requestBody,
				},
			};
		}
		// Parse bin days from XML embedded in JSON response
		else if (clientSideResponse.RequestId == 3)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var xmlData = jsonDoc.RootElement.GetProperty("data").GetString()!;

			var rawBinDays = BinDaysRegex().Matches(xmlData)!;

			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var binTypeStr = rawBinDay.Groups["binType"].Value.Trim();
				var dateStr = rawBinDay.Groups["date"].Value.Trim();

				var date = DateUtilities.ParseDateExact(dateStr, "dd/MM/yyyy");
				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, binTypeStr);

				binDays.Add(new BinDay
				{
					Date = date,
					Address = address,
					Bins = matchedBins,
				});
			}

			return new GetBinDaysResponse
			{
				BinDays = ProcessingUtilities.ProcessBinDays(binDays),
			};
		}

		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <summary>
	/// Builds the Bartec authentication lookup request, stashing the session cookies in metadata for subsequent steps.
	/// </summary>
	/// <remarks>
	/// The council publishes two authentication lookups that resolve to different Bartec credentials.
	/// Only the one used here (the one the live council page calls) returns bank-holiday-adjusted
	/// collection dates -- the other returns the unadjusted schedule, which is a day out whenever a
	/// bank holiday shifts the round. Do not swap it for the other id.
	/// </remarks>
	private static ClientSideRequest BuildAuthLookupRequest(string cookies)
	{
		return new ClientSideRequest
		{
			RequestId = 2,
			Url = "https://eastcambs-self.achieveservice.com/apibroker/runLookup?id=64f7354c7f8a9",
			Method = "POST",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
				{ "content-type", Constants.ApplicationJson },
				{ "cookie", cookies },
			},
			Body = $$"""
			{
				"formValues": { "Section 1": {} },
				"isPublished": true,
				"formName": "Waste collections calendar",
				"formUri": "{{FormUri}}"
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
	}
}
