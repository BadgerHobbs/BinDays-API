namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Doncaster Metropolitan Borough Council.
/// </summary>
internal sealed partial class DoncasterMetropolitanBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Doncaster Metropolitan Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://doncaster-wasterecycling.oncreate.app/w/webpage/bin-query");

	/// <inheritdoc/>
	public override string GovUkId => "doncaster";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Refuse" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Recycling" ],
		},
		new()
		{
			Name = "Glass Recycling",
			Colour = BinColour.Green,
			Keys = [ "Recycling" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Green Garden Waste Collection Service" ],
		},
	];

	/// <summary>
	/// The base URL of the council's waste and recycling portal.
	/// </summary>
	private const string _baseUrl = "https://doncaster-wasterecycling.oncreate.app";

	/// <summary>
	/// Regex for the addresses from the address listing links.
	/// </summary>
	[GeneratedRegex(@"href=""/w/webpage/address-collections\?webpage_token=(?<token>[a-f0-9]+)&amp;auth=(?<auth>[^&""]+)&amp;id=(?<id>\d+)""[^>]*aria-label=""View collection details, (?<address>[^""]+)""")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for the address listing pagination summary, only present when there is more than one page.
	/// </summary>
	[GeneratedRegex(@"Results \d+-(?<end>\d+) of (?<total>\d+)")]
	private static partial Regex PaginationRegex();

	/// <summary>
	/// Regex for the bin days from the collection calendar events.
	/// </summary>
	[GeneratedRegex(@"\{""id"":\d+,""title"":""(?<service>[^""]+)"",""date"":""(?<date>\d{4}-\d{2}-\d{2})""")]
	private static partial Regex BinDaysRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting the first page of addresses
		if (clientSideResponse == null)
		{
			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = CreateAddressesRequest(postcode, 0, []),
			};

			return getAddressesResponse;
		}
		// Process addresses from response, following pagination as required
		else if (clientSideResponse.RequestId == 1)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var listingHtml = jsonDoc.RootElement.GetProperty("data").GetString()!;
			var rawAddresses = AddressRegex().Matches(listingHtml)!;

			var addresses = new List<Address>();

			// Iterate through each address accumulated from previous pages, and restore it
			var accumulatedAddresses = clientSideResponse.Options.Metadata["addresses"];
			foreach (var entry in accumulatedAddresses.Split('\n', StringSplitOptions.RemoveEmptyEntries))
			{
				var parts = entry.Split('|', 2);

				addresses.Add(new Address
				{
					Property = parts[0],
					Postcode = postcode,
					Uid = parts[1],
				});
			}

			// Iterate through each address, and create a new address object
			foreach (Match rawAddress in rawAddresses)
			{
				var token = rawAddress.Groups["token"].Value;
				var auth = rawAddress.Groups["auth"].Value;
				var id = rawAddress.Groups["id"].Value;

				var address = new Address
				{
					Property = WebUtility.HtmlDecode(rawAddress.Groups["address"].Value).Trim(),
					Postcode = postcode,
					// Uid format: "webpageToken;auth;id"
					Uid = $"{token};{auth};{id}",
				};

				addresses.Add(address);
			}

			// Request the next page of results if the listing has more addresses
			var pagination = PaginationRegex().Match(listingHtml);
			if (pagination.Success)
			{
				var end = int.Parse(pagination.Groups["end"].Value, CultureInfo.InvariantCulture);
				var total = int.Parse(pagination.Groups["total"].Value, CultureInfo.InvariantCulture);

				if (end < total)
				{
					var nextPageResponse = new GetAddressesResponse
					{
						NextClientSideRequest = CreateAddressesRequest(postcode, end, addresses),
					};

					return nextPageResponse;
				}
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
		// Prepare client-side request for getting bin days
		if (clientSideResponse == null)
		{
			// Uid format: "webpageToken;auth;id"
			var uidParts = address.Uid!.Split(';');

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}/w/webpage/address-collections?webpage_token={uidParts[0]}&auth={uidParts[1]}&id={uidParts[2]}",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "x-requested-with", Constants.XmlHttpRequest },
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 1)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var resultsHtml = WebUtility.HtmlDecode(jsonDoc.RootElement.GetProperty("data").GetString()!);
			var rawBinDays = BinDaysRegex().Matches(resultsHtml)!;

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var service = rawBinDay.Groups["service"].Value.Trim();
				var date = DateUtilities.ParseDateExact(rawBinDay.Groups["date"].Value, "yyyy-MM-dd");

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
	/// Creates the client-side request for a page of the address listing, filtered by postcode,
	/// carrying forward the addresses found on previous pages.
	/// </summary>
	private static ClientSideRequest CreateAddressesRequest(string postcode, int offset, List<Address> addresses)
	{
		// The listing is filtered and paged by the session storage sent with each reload
		var sessionStorage = $$"""
		{
			"/w/webpage/bin-query": {
				"filters": {
					"defined": {
						"OBJ0000008GBNZR1": [
							{
								"PAG0000624GBSLB1": {
									"PCL0004569GBSLB1": {
										"search": {
											"PCF0014212GBSLB1": {
												"comparator_id": "1",
												"value": "{{postcode}}"
											}
										}
									}
								}
							}
						]
					}
				},
				"cell_PCL0004570GBSLB1": {
					"pagination_start_offset": {{offset}}
				}
			}
		}
		""";

		// Iterate through each address, and serialize it to carry forward to the next page
		var serializedAddresses = new List<string>();
		foreach (var address in addresses)
		{
			serializedAddresses.Add($"{address.Property}|{address.Uid}");
		}

		var clientSideRequest = new ClientSideRequest
		{
			RequestId = 1,
			Url = $"{_baseUrl}/w/webpage/bin-query?widget_action=reload",
			Method = "POST",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
				{ "content-type", Constants.FormUrlEncoded },
				{ "x-requested-with", Constants.XmlHttpRequest },
			},
			Body = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "_session_storage", sessionStorage },
			}),
			Options = new ClientSideOptions
			{
				Metadata =
				{
					{ "addresses", string.Join("\n", serializedAddresses) },
				},
			},
		};

		return clientSideRequest;
	}
}
