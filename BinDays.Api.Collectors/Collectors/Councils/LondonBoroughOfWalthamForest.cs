namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for London Borough of Waltham Forest.
/// </summary>
internal sealed partial class LondonBoroughOfWalthamForest : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "London Borough of Waltham Forest";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.walthamforest.gov.uk/rubbish-and-recycling/household-bin-collections/check-your-collection-days");

	/// <inheritdoc/>
	public override string GovUkId => "waltham-forest";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Domestic Waste Collection Service" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Green,
			Keys = [ "Recycling Collection Service" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden Waste Collection Service" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Brown,
			Keys = [ "Food Waste Collection Service" ],
			Type = BinType.Caddy,
		},
	];

	/// <summary>
	/// The AchieveForms URL for the bin collection lookup form.
	/// </summary>
	private const string _achieveFormsUrl = "https://portal.walthamforest.gov.uk/AchieveForms/?mode=fill&consentMessage=yes&form_uri=sandbox-publish://AF-Process-d62ccdd2-3de9-48eb-a229-8e20cbdd6393/AF-Stage-8bf39bf9-5391-4c24-857f-0dc2025c67f4/definition.json&process=1&process_uri=sandbox-processes://AF-Process-d62ccdd2-3de9-48eb-a229-8e20cbdd6393&process_id=AF-Process-d62ccdd2-3de9-48eb-a229-8e20cbdd6393";

	/// <summary>
	/// The base URL for API broker requests.
	/// </summary>
	private const string _apibrokerBaseUrl = "https://portal.walthamforest.gov.uk/apibroker/";

	/// <summary>
	/// The form ID for address lookup.
	/// </summary>
	private const string _addressFormId = "AF-Form-08647570-43e4-4e68-9d6a-65d914e27ef7";

	/// <summary>
	/// The lookup ID for address search.
	/// </summary>
	private const string _addressLookupId = "5694fd42a5541";

	/// <summary>
	/// Regex to extract the session ID (sid) from HTML content.
	/// </summary>
	[GeneratedRegex(@"sid=([a-f0-9]+)")]
	private static partial Regex SessionIdRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _achieveFormsUrl,
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for address lookup
		else if (clientSideResponse.RequestId == 1)
		{
			var sid = SessionIdRegex().Match(clientSideResponse.Content).Groups[1].Value;
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(
				clientSideResponse.Headers["set-cookie"]
			);

			var requestBody = BuildAddressLookupPayload(postcode);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_apibrokerBaseUrl}?api=RunLookup&id={_addressLookupId}&repeat_against=&noRetry=false&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&sid={sid}",
				Method = "POST",
				Headers = new()
				{
					{ "content-type", "application/json" },
					{ "cookie", requestCookies },
					{ "x-requested-with", "XMLHttpRequest" },
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
			var rowsData = jsonDoc.RootElement
				.GetProperty("integration")
				.GetProperty("transformed")
				.GetProperty("rows_data");

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var property in rowsData.EnumerateObject())
			{
				var addressData = property.Value;
				var display = addressData.GetProperty("display").GetString()!;
				var uprn = addressData.GetProperty("overview_uprn").GetString()!;
				var addressPostcode = addressData.GetProperty("overview_postcode").GetString()!;

				var address = new Address
				{
					Property = display.Trim(),
					Postcode = addressPostcode.Trim(),
					Uid = uprn,
				};

				addresses.Add(address);
			}

			var getAddressesResponse = new GetAddressesResponse
			{
				Addresses = [.. addresses],
			};

			return getAddressesResponse;
		}

		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <inheritdoc/>
	public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting bin days
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _achieveFormsUrl,
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
				},
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "uprn", address.Uid! },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for address lookup before bin days
		else if (clientSideResponse.RequestId == 1)
		{
			var sid = SessionIdRegex().Match(clientSideResponse.Content).Groups[1].Value;
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(
				clientSideResponse.Headers["set-cookie"]
			);

			var requestBody = BuildAddressLookupPayload(address.Postcode!);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_apibrokerBaseUrl}?api=RunLookup&id={_addressLookupId}&repeat_against=&noRetry=false&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&sid={sid}",
				Method = "POST",
				Headers = new()
				{
					{ "content-type", "application/json" },
					{ "cookie", requestCookies },
					{ "x-requested-with", "XMLHttpRequest" },
				},
				Body = requestBody,
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "cookie", requestCookies },
						{ "sid", sid },
						{ "uprn", address.Uid! },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for bin collections lookup
		else if (clientSideResponse.RequestId == 2)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var rowsData = jsonDoc.RootElement
				.GetProperty("integration")
				.GetProperty("transformed")
				.GetProperty("rows_data");

			var uprn = clientSideResponse.Options.Metadata["uprn"];
			var matchedAddress = rowsData.EnumerateObject()
				.Select(p => p.Value)
				.FirstOrDefault(v => v.GetProperty("overview_uprn").GetString()! == uprn);

			var sid = clientSideResponse.Options.Metadata["sid"];
			var requestCookies = clientSideResponse.Options.Metadata["cookie"];

			var addressDisplay = matchedAddress.GetProperty("display").GetString()!;
			var ward = matchedAddress.GetProperty("overview_ward").GetString()!;

			var requestBody = BuildMainFormPayload(address.Postcode!, uprn, addressDisplay, ward);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = $"{_apibrokerBaseUrl}runLookup?id=5e42e28b44d9e&repeat_against=&noRetry=true&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&sid={sid}",
				Method = "POST",
				Headers = new()
				{
					{ "content-type", "application/json" },
					{ "cookie", requestCookies },
					{ "x-requested-with", "XMLHttpRequest" },
				},
				Body = requestBody,
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "cookie", requestCookies },
						{ "sid", sid },
						{ "uprn", uprn },
						{ "address", addressDisplay },
						{ "ward", ward },
					},
				},
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
			var sid = clientSideResponse.Options.Metadata["sid"];
			var requestCookies = clientSideResponse.Options.Metadata["cookie"];
			var uprn = clientSideResponse.Options.Metadata["uprn"];
			var addressDisplay = clientSideResponse.Options.Metadata["address"];
			var ward = clientSideResponse.Options.Metadata["ward"];

			var requestBody = BuildMainFormPayload(address.Postcode!, uprn, addressDisplay, ward);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 4,
				Url = $"{_apibrokerBaseUrl}runLookup?id=5e208cda0d0a0&repeat_against=&noRetry=false&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&sid={sid}",
				Method = "POST",
				Headers = new()
				{
					{ "content-type", "application/json" },
					{ "cookie", requestCookies },
					{ "x-requested-with", "XMLHttpRequest" },
				},
				Body = requestBody,
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin day results
		else if (clientSideResponse.RequestId == 4)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var rowsData = jsonDoc.RootElement
				.GetProperty("integration")
				.GetProperty("transformed")
				.GetProperty("rows_data");

			// Iterate through each bin day record, and create a new bin day object
			var binDays = new List<BinDay>();
			var binDayEntries = rowsData.ValueKind == JsonValueKind.Object
				? rowsData.EnumerateObject().Select(p => p.Value)
				: rowsData.EnumerateArray();

			foreach (var binData in binDayEntries)
			{
				var serviceName = binData.GetProperty("ServiceName").GetString()!;
				var nextCollectionDate = binData.GetProperty("NextCollectionDate").GetString()!.Trim();

				if (string.IsNullOrWhiteSpace(nextCollectionDate) || nextCollectionDate.Contains("NaN", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				var date = nextCollectionDate.ParseDateInferringYear("dddd d MMMM");
				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, serviceName);

				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = matchedBins,
				};

				binDays.Add(binDay);
			}

			var getBinDaysResponse = new GetBinDaysResponse
			{
				BinDays = ProcessingUtilities.ProcessBinDays(binDays),
			};

			return getBinDaysResponse;
		}

		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <summary>
	/// Builds the address lookup request payload for postcode search.
	/// </summary>
	/// <param name="postcode">The formatted postcode to search for.</param>
	/// <returns>JSON payload as a string.</returns>
	private static string BuildAddressLookupPayload(string postcode)
	{
		return $$"""
		{
			"formId": "{{_addressFormId}}",
			"formValues": {
				"Section 1": {
					"postcode_search": {
						"value": "{{postcode}}"
					}
				}
			}
		}
		""";
	}

	/// <summary>
	/// Builds the main form request payload for bin collection lookups.
	/// </summary>
	/// <param name="postcode">The formatted postcode.</param>
	/// <param name="uprn">The unique property reference number.</param>
	/// <param name="addressDisplay">The full display address.</param>
	/// <param name="ward">The ward name.</param>
	/// <returns>JSON payload as a string.</returns>
	private static string BuildMainFormPayload(string postcode, string uprn, string addressDisplay, string ward)
	{
		return $$"""
		{
			"formId": "AF-Form-07a98da7-bc6b-4df6-aa46-33a06312acce",
			"formValues": {
				"Section 1": {
					"calcWardCode": {
						"value": "{{ward}}"
					},
					"addressLookup": {
						"value": {
							"Section 1": {
								"postcode_search": {
									"value": "{{postcode}}"
								},
								"postcodeFound": {
									"value": "1"
								},
								"YourAddress": {
									"value_label": ["{{addressDisplay}}"],
									"value": "{{uprn}}"
								},
								"uprnConfirm": {
									"value": "{{uprn}}"
								},
								"wardName": {
									"value": "{{ward}}"
								}
							}
						}
					},
					"inputUPRN": {
						"value": "{{uprn}}"
					}
				}
			}
		}
		""";
	}
}
