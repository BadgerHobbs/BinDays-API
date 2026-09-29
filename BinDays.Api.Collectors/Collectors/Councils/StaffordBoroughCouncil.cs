namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Stafford Borough Council.
/// </summary>
internal sealed partial class StaffordBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Stafford Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.staffordbc.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "stafford";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Keys = [ "refuse" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Blue,
			Keys = [ "recycling" ],
		},
		// Garden waste has no dated entry of its own; the site only states it is collected
		// alongside recycling for subscribed properties, so it shares the recycling key.
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "recycling" ],
		},
		// Food waste has no dated entry of its own; the site only states it is collected
		// weekly alongside both refuse and recycling, so it shares both keys.
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Grey,
			Keys = [ "refuse", "recycling" ],
			Type = BinType.Caddy,
		},
	];

	/// <summary>
	/// The base URL of the council's customer portal.
	/// </summary>
	private const string _baseUrl = "https://customers.staffordbc.gov.uk";

	/// <summary>
	/// The form's session variables, marking an address as selected.
	/// Base64 of <c>{"ADDRESSSELECTED":{"value":true,"scope":"SERVERCLIENTWITHUPDATE"}}</c>.
	/// </summary>
	private const string _addressSelectedVariables = "eyJBRERSRVNTU0VMRUNURUQiOnsidmFsdWUiOnRydWUsInNjb3BlIjoiU0VSVkVSQ0xJRU5UV0lUSFVQREFURSJ9fQ==";

	/// <summary>
	/// Regex to parse JSONP responses.
	/// </summary>
	[GeneratedRegex(@"^[^(]+\((?<json>.*)\)$", RegexOptions.Singleline)]
	private static partial Regex JsonpRegex();

	/// <summary>
	/// Regex to capture hidden form fields.
	/// </summary>
	[GeneratedRegex(@"name=""(?<name>ABOUTMYAREA_[^""]+)"" value=""(?<value>[^""]*)""")]
	private static partial Regex HiddenFieldRegex();

	/// <summary>
	/// Regex for the next collection dates from the html.
	/// </summary>
	[GeneratedRegex(@"Next (?<type>Refuse|Recycling) Collection:</strong>\s*</td>\s*<td[^>]*>\s*[A-Za-z]+\s+(?<date>\d{2}/\d{2}/\d{4})\s*</td>")]
	private static partial Regex BinDaysRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var jsonPayload = $$$"""
			{"id":1,"method":"postcodeSearch","params":{"provider":"EndPoint","postcode":"{{{postcode}}}","includecommercial":false}}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}/apiserver/postcode?callback=cb&jsonrpc={Uri.EscapeDataString(jsonPayload)}",
				Method = "GET",
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
			var json = JsonpRegex().Match(clientSideResponse.Content).Groups["json"].Value;

			using var jsonDoc = JsonDocument.Parse(json);

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var addressElement in jsonDoc.RootElement.GetProperty("result").EnumerateArray())
			{
				string[] addressParts =
				[
					addressElement.GetProperty("line1").GetString()!,
					addressElement.GetProperty("line2").GetString()!,
					addressElement.GetProperty("line3").GetString()!,
					addressElement.GetProperty("town").GetString()!,
				];

				var address = new Address
				{
					Property = string.Join(", ", addressParts.Where(p => !string.IsNullOrWhiteSpace(p))),
					Postcode = postcode,
					Uid = addressElement.GetProperty("udprn").GetString()!,
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
		// Prepare client-side request for the initial page load
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}/about-my-area",
				Method = "GET",
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare form submission with the selected address
		else if (clientSideResponse.RequestId == 1)
		{
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);

			var hiddenFields = HiddenFieldRegex().Matches(clientSideResponse.Content)!;
			var hiddenFieldValues = hiddenFields
				.DistinctBy(x => x.Groups["name"].Value)
				.ToDictionary(
					x => x.Groups["name"].Value,
					x => x.Groups["value"].Value
				);

			var pageSessionId = hiddenFieldValues["ABOUTMYAREA_PAGESESSIONID"];
			var sessionId = hiddenFieldValues["ABOUTMYAREA_SESSIONID"];
			var nonce = hiddenFieldValues["ABOUTMYAREA_NONCE"];

			var formData = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "ABOUTMYAREA_PAGESESSIONID", pageSessionId },
				{ "ABOUTMYAREA_SESSIONID", sessionId },
				{ "ABOUTMYAREA_NONCE", nonce },
				{ "ABOUTMYAREA_VARIABLES", _addressSelectedVariables },
				{ "ABOUTMYAREA_UPRN", address.Uid! },
				{ "ABOUTMYAREA_POSTCODE", address.Postcode! },
				{ "ABOUTMYAREA_FORMACTION_NEXT", "ABOUTMYAREA_UPDATE" },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseUrl}/apiserver/formsservice/http/processsubmission?pageSessionId={pageSessionId}&fsid={sessionId}&fsn={nonce}",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
					{ "cookie", cookies },
				},
				Body = formData,
				Options = new ClientSideOptions
				{
					FollowRedirects = false,
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
		// Follow each redirect, carrying the session cookies, until the results page is reached
		else if (clientSideResponse.Headers.TryGetValue("location", out var location))
		{
			var cookies = clientSideResponse.Options.Metadata["cookie"];
			if (clientSideResponse.Headers.TryGetValue("set-cookie", out var setCookieHeader))
			{
				cookies = $"{cookies}; {ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader!)}";
			}

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = clientSideResponse.RequestId + 1,
				Url = new Uri(new Uri(_baseUrl), location).ToString(),
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "cookie", cookies },
				},
				Options = new ClientSideOptions
				{
					FollowRedirects = false,
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
		// Process bin days from the results page
		else
		{
			var rawBinDays = BinDaysRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each next collection date, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var type = rawBinDay.Groups["type"].Value;
				var date = DateUtilities.ParseDateExact(rawBinDay.Groups["date"].Value, "dd/MM/yyyy");

				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = ProcessingUtilities.GetMatchingBins(_binTypes, type),
				};

				binDays.Add(binDay);
			}

			var getBinDaysResponse = new GetBinDaysResponse
			{
				BinDays = ProcessingUtilities.ProcessBinDays(binDays),
			};

			return getBinDaysResponse;
		}
	}
}
