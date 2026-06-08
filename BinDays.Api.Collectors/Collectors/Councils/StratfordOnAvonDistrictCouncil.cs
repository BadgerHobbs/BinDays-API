namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Stratford-on-Avon District Council.
/// </summary>
internal sealed partial class StratfordOnAvonDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Stratford-on-Avon District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.stratford.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "stratford-on-avon";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Grey,
			Keys = [ "Refuse", "General refuse" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Recycling" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Garden" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Green,
			Keys = [ "Food waste" ],
			Type = BinType.Caddy,
		},
	];

	/// <summary>
	/// Regex for the bin collection rows.
	/// </summary>
	[GeneratedRegex(@"<tr>\s*<td>(?<date>[^<]+)</td>(?<cells>.*?)</tr>", RegexOptions.Singleline)]
	private static partial Regex BinRowsRegex();

	/// <summary>
	/// Regex for cells that contain a check mark, capturing their title.
	/// </summary>
	[GeneratedRegex(@"<td[^>]+title=""(?<title>[^""]+)""[^>]*>.*?check-img", RegexOptions.Singleline)]
	private static partial Regex CheckedCellRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://api.stratford.gov.uk/v1/addresses/postcode/{postcode}?nationalSearch=false&includeNonPostal=false&firstLine=",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "referer", "https://www.stratford.gov.uk/" },
					{ "origin", "https://www.stratford.gov.uk" },
				},
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

			var addressElements = jsonDoc.RootElement.GetProperty("data").EnumerateArray();

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var addressElement in addressElements)
			{
				var uprn = addressElement.GetProperty("uprn").GetInt64().ToString(CultureInfo.InvariantCulture);
				var addressLine1 = addressElement.GetProperty("addressLine1").GetString();
				var addressLine2 = addressElement.GetProperty("addressLine2").GetString();
				var addressLine3 = addressElement.GetProperty("addressLine3").GetString();
				var addressLine4 = addressElement.GetProperty("addressLine4").GetString();

				var propertyParts = new[] { addressLine1, addressLine2, addressLine3, addressLine4 }
					.Where(part => !string.IsNullOrWhiteSpace(part))
					.Select(part => part!.Trim());

				var property = string.Join(", ", propertyParts);

				// UID format: uprn;addressLine1;addressLine2;addressLine3;addressLine4
				var uid = string.Join(
					";",
					[
						uprn,
						addressLine1?.Trim(),
						addressLine2?.Trim(),
						addressLine3?.Trim(),
						addressLine4?.Trim(),
					]
				);

				var address = new Address
				{
					Property = property,
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

		// Throw exception for invalid request
		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <inheritdoc/>
	public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting bin days
		if (clientSideResponse == null)
		{
			// UID format: uprn;addressLine1;addressLine2;addressLine3;addressLine4
			var uidParts = address.Uid!.Split(';');
			var uprn = uidParts[0];
			var addressLine1 = uidParts[1];
			var addressLine2 = uidParts[2];
			var addressLine3 = uidParts[3];
			var addressLine4 = uidParts[4];

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "frmAddress1", addressLine1 },
				{ "frmAddress2", addressLine2 },
				{ "frmAddress3", addressLine3 },
				{ "frmAddress4", addressLine4 },
				{ "frmPostcode", address.Postcode! },
				{ "frmUPRN", uprn },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://www.stratford.gov.uk/waste-recycling/when-we-collect.cfm/part/calendar",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
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
		else if (clientSideResponse.RequestId == 1)
		{
			var rawBinRows = BinRowsRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinRow in rawBinRows)
			{
				var date = DateUtilities.ParseDateExact(rawBinRow.Groups["date"].Value.Trim(), "dddd, dd/MM/yyyy");

				var bins = CheckedCellRegex()
					.Matches(rawBinRow.Groups["cells"].Value)!
					.SelectMany(m => ProcessingUtilities.GetMatchingBins(_binTypes, m.Groups["title"].Value))
					.ToList();

				binDays.Add(new BinDay
				{
					Date = date,
					Address = address,
					Bins = bins,
				});
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
