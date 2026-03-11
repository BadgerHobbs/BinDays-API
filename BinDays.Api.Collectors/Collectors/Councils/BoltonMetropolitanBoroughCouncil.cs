namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Bolton Metropolitan Borough Council.
/// </summary>
internal sealed partial class BoltonMetropolitanBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Bolton Metropolitan Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.bolton.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "bolton";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Green,
			Keys = [ "fd garden caddy" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Paper and Card Recycling",
			Colour = BinColour.Brown,
			Keys = [ "beige recycling bin" ],
		},
		new()
		{
			Name = "Plastic, Glass and Metal Recycling",
			Colour = BinColour.Red,
			Keys = [ "burgundy plastic bin" ],
		},
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Grey,
			Keys =
			[
				"grey rubbish bin",
				"x_grey rubbish bin",
			],
		},
	];

	/// <summary>
	/// Regex for parsing bin sections from the HTML response.
	/// </summary>
	[GeneratedRegex(@"<strong>\s*(?<bin>[^<]+?)\s*:</strong>\s*</p>\s*<ul[^>]*>(?<dates>[\s\S]*?)</ul>", RegexOptions.Singleline)]
	private static partial Regex BinSectionRegex();

	/// <summary>
	/// Regex for parsing collection dates from a bin section.
	/// </summary>
	[GeneratedRegex(@"<li>\s*(?<date>[^<]+?)\s*</li>", RegexOptions.Singleline)]
	private static partial Regex BinDateRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting authorization token
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://bolton.form.uk.empro.verintcloudservices.com/api/citizen?archived=Y&preview=false&locale=en",
				Method = "GET",
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for loading the form
		else if (clientSideResponse.RequestId == 1)
		{
			var authToken = clientSideResponse.Headers["authorization"];

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = "https://bolton.form.uk.empro.verintcloudservices.com/api/form/es_bin_collection_dates?preview=false&locale=en",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "authorization", authToken },
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for loading the form content
		else if (clientSideResponse.RequestId == 2)
		{
			var authToken = clientSideResponse.Headers["authorization"];

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = "https://bolton.form.uk.empro.verintcloudservices.com/api/content/es_bin_collection_dates?version=2&preview=false&locale=en",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "authorization", authToken },
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for getting addresses
		else if (clientSideResponse.RequestId == 3)
		{
			var authToken = clientSideResponse.Headers["authorization"];

			var requestBody = $$"""
			{
				"name": "es_bin_collection_dates",
				"data": {
					"postcode": "{{postcode}}"
				}
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 4,
				Url = "https://bolton.form.uk.empro.verintcloudservices.com/api/widget?action=propertysearch&actionedby=ps_address&loadform=true&access=citizen&locale=en",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "authorization", authToken },
					{ "content-type", Constants.ApplicationJson },
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
		else if (clientSideResponse.RequestId == 4)
		{
			using var document = JsonDocument.Parse(clientSideResponse.Content);
			var addressesJson = document.RootElement.GetProperty("data").EnumerateArray();

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var addressElement in addressesJson)
			{
				var uid = addressElement.GetProperty("value").GetString()!;

				if (string.IsNullOrWhiteSpace(uid))
				{
					continue;
				}

				var address = new Address
				{
					Property = addressElement.GetProperty("label").GetString()!.Trim(),
					Postcode = postcode,
					Uid = uid,
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
		// Prepare client-side request for getting authorization token
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://bolton.form.uk.empro.verintcloudservices.com/api/citizen?archived=Y&preview=false&locale=en",
				Method = "GET",
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for loading the form
		else if (clientSideResponse.RequestId == 1)
		{
			var authToken = clientSideResponse.Headers["authorization"];

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = "https://bolton.form.uk.empro.verintcloudservices.com/api/form/es_bin_collection_dates?preview=false&locale=en",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "authorization", authToken },
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for loading the form content
		else if (clientSideResponse.RequestId == 2)
		{
			var authToken = clientSideResponse.Headers["authorization"];

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = "https://bolton.form.uk.empro.verintcloudservices.com/api/content/es_bin_collection_dates?version=2&preview=false&locale=en",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "authorization", authToken },
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for refreshing authorization and address context
		else if (clientSideResponse.RequestId == 3)
		{
			var authToken = clientSideResponse.Headers["authorization"];

			var requestBody = $$"""
			{
				"name": "es_bin_collection_dates",
				"data": {
					"postcode": "{{address.Postcode!}}"
				}
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 4,
				Url = "https://bolton.form.uk.empro.verintcloudservices.com/api/widget?action=propertysearch&actionedby=ps_address&loadform=true&access=citizen&locale=en",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "authorization", authToken },
					{ "content-type", Constants.ApplicationJson },
				},
				Body = requestBody,
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for loading property details
		else if (clientSideResponse.RequestId == 4)
		{
			var authToken = clientSideResponse.Headers["authorization"];

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 5,
				Url = $"https://bolton.form.uk.empro.verintcloudservices.com/api/setobjectid?objecttype=property&objectid={address.Uid!}&loaddata=true",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "authorization", authToken },
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting bin collection dates
		else if (clientSideResponse.RequestId == 5)
		{
			var authToken = clientSideResponse.Headers["authorization"];

			using var document = JsonDocument.Parse(clientSideResponse.Content);
			var uprn = document.RootElement.GetProperty("profileData").GetProperty("property-UPRN").GetString()!;

			var startDate = DateTime.UtcNow.Date;
			var endDate = startDate.AddDays(56);

			var requestBody = $$"""
			{
				"name": "es_bin_collection_dates",
				"data": {
					"uprn": "{{uprn}}",
					"start_date": "{{startDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}}",
					"end_date": "{{endDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}}"
				}
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 6,
				Url = "https://bolton.form.uk.empro.verintcloudservices.com/api/custom?action=es_get_bin_collection_dates&actionedby=uprn_changed&loadform=true&access=citizen&locale=en",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "authorization", authToken },
					{ "content-type", Constants.ApplicationJson },
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
		else if (clientSideResponse.RequestId == 6)
		{
			using var document = JsonDocument.Parse(clientSideResponse.Content);
			var collectionHtml = document.RootElement.GetProperty("data").GetProperty("collection_dates").GetString()!;

			var binSections = BinSectionRegex().Matches(collectionHtml)!;

			// Iterate through each bin section, and create bin day objects
			var binDays = new List<BinDay>();
			foreach (Match binSection in binSections)
			{
				var binName = binSection.Groups["bin"].Value.Trim();
				var datesContent = binSection.Groups["dates"].Value;

				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, binName);
				var dateMatches = BinDateRegex().Matches(datesContent)!;

				foreach (Match dateMatch in dateMatches)
				{
					var dateText = dateMatch.Groups["date"].Value.Trim();
					var date = DateUtilities.ParseDateExact(dateText, "dddd dd MMMM yyyy");

					var binDay = new BinDay
					{
						Date = date,
						Address = address,
						Bins = matchedBinTypes,
					};

					binDays.Add(binDay);
				}
			}

			var getBinDaysResponse = new GetBinDaysResponse
			{
				BinDays = ProcessingUtilities.ProcessBinDays(binDays),
			};

			return getBinDaysResponse;
		}

		throw new InvalidOperationException("Invalid client-side request.");
	}
}
