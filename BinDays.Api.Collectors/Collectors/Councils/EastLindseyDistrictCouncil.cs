namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for East Lindsey District Council.
/// </summary>
internal sealed partial class EastLindseyDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "East Lindsey District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.e-lindsey.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "east-lindsey";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Grey,
			Keys = [ "wastenextref" ],
		},
		new()
		{
			Name = "Plastic and Metal Recycling",
			Colour = BinColour.Blue,
			Keys = [ "wastenextrec" ],
		},
		new()
		{
			Name = "Paper and Card Recycling",
			Colour = BinColour.Purple,
			Keys = [ "wastenextpur" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "greenfirst" ],
		},
	];

	/// <summary>
	/// Regex for parsing JSONP responses.
	/// </summary>
	[GeneratedRegex(@"^[^(]+\((?<json>.+)\);?$", RegexOptions.Singleline)]
	private static partial Regex JsonpRegex();

	/// <summary>
	/// Regex for the page session id from the hidden input.
	/// </summary>
	[GeneratedRegex(@"name=""WASTECOLLECTIONDAYS202627_PAGESESSIONID""\s+value=""(?<pageSessionId>[^""]+)""")]
	private static partial Regex PageSessionIdRegex();

	/// <summary>
	/// Regex for the session id from the hidden input.
	/// </summary>
	[GeneratedRegex(@"name=""WASTECOLLECTIONDAYS202627_SESSIONID""\s+value=""(?<sessionId>[^""]+)""")]
	private static partial Regex SessionIdRegex();

	/// <summary>
	/// Regex for the nonce from the hidden input.
	/// </summary>
	[GeneratedRegex(@"name=""WASTECOLLECTIONDAYS202627_NONCE""\s+value=""(?<nonce>[^""]+)""")]
	private static partial Regex NonceRegex();

	/// <summary>
	/// Regex for the base64 encoded results data from the results page script.
	/// </summary>
	[GeneratedRegex(@"WASTECOLLECTIONDAYS202627_RESULTS_FIELD12Data\s*=\s*JSON\.parse\(helper\.utilDecode\('(?<data>[^']+)'\)\);")]
	private static partial Regex ResultsDataRegex();

	/// <summary>
	/// Regex for removing ordinal suffixes from day numbers.
	/// </summary>
	[GeneratedRegex(@"(?<=\d)(st|nd|rd|th)", RegexOptions.IgnoreCase)]
	private static partial Regex OrdinalSuffixRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var jsonRpc = $$$"""{"id":1,"method":"postcodeSearch","params":{"provider":"","postcode":"{{{postcode}}}"}}""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://www.e-lindsey.gov.uk/apiserver/postcode?callback=bindays&jsonrpc={jsonRpc}",
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
			var rawAddresses = jsonDoc.RootElement.GetProperty("result").EnumerateArray();

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			var addressIndex = 0;
			foreach (var rawAddress in rawAddresses)
			{
				var addressParts = new string?[]
				{
					rawAddress.GetProperty("line1").GetString(),
					rawAddress.GetProperty("line2").GetString(),
					rawAddress.GetProperty("line3").GetString(),
					rawAddress.GetProperty("line4").GetString(),
					rawAddress.GetProperty("line5").GetString(),
					rawAddress.GetProperty("town").GetString(),
					rawAddress.GetProperty("county").GetString(),
					rawAddress.GetProperty("postcode").GetString(),
				};
				var property = string.Join(", ", addressParts.Where(part => !string.IsNullOrWhiteSpace(part)));

				// Uid format: "uprn;source;addressIndex;property"
				var address = new Address
				{
					Property = property,
					Postcode = postcode,
					Uid = $"{rawAddress.GetProperty("uprn").GetString()!};{rawAddress.GetProperty("source").GetString()!};{addressIndex};{property}",
				};

				addresses.Add(address);
				addressIndex++;
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
		// Prepare client-side request for getting form state
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://www.e-lindsey.gov.uk/mywastecollections?ccp=true",
				Method = "GET",
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Submit the selected address and load the results page
		else if (clientSideResponse.RequestId == 1)
		{
			var pageSessionId = PageSessionIdRegex().Match(clientSideResponse.Content).Groups["pageSessionId"].Value;
			var sessionId = SessionIdRegex().Match(clientSideResponse.Content).Groups["sessionId"].Value;
			var nonce = NonceRegex().Match(clientSideResponse.Content).Groups["nonce"].Value;

			// Uid format: "uprn;source;addressIndex;property"
			var uidParts = address.Uid!.Split(';', 4);
			var uprn = uidParts[0];
			var source = uidParts[1];
			var addressIndex = uidParts[2];
			var chosenAddress = uidParts[3];

			var variablesJson = $$$"""{"ADDRESSSOURCE":{"value":"{{{source}}}","scope":"SERVERCLIENTWITHUPDATE"},"ADDRESSUPRN":{"value":"{{{uprn}}}","scope":"SERVERCLIENTWITHUPDATE"},"TESTDATELAYOUT_DISPLAYED":{"value":false,"scope":"SERVERCLIENT"}}""";
			var variables = Convert.ToBase64String(Encoding.UTF8.GetBytes(variablesJson));

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "WASTECOLLECTIONDAYS202627_PAGESESSIONID", pageSessionId },
				{ "WASTECOLLECTIONDAYS202627_SESSIONID", sessionId },
				{ "WASTECOLLECTIONDAYS202627_NONCE", nonce },
				{ "WASTECOLLECTIONDAYS202627_VARIABLES", variables },
				{ "WASTECOLLECTIONDAYS202627_PAGENAME", "LOOKUP" },
				{ "WASTECOLLECTIONDAYS202627_PAGEINSTANCE", "0" },
				{ "WASTECOLLECTIONDAYS202627_LOOKUP_ADDRESSSOURCE", source },
				{ "WASTECOLLECTIONDAYS202627_LOOKUP_ADDRESSUPRN", uprn },
				{ "WASTECOLLECTIONDAYS202627_LOOKUP_ADDRESSLOOKUPPOSTCODE", address.Postcode! },
				{ "WASTECOLLECTIONDAYS202627_LOOKUP_ADDRESSLOOKUPADDRESS", addressIndex },
				{ "WASTECOLLECTIONDAYS202627_LOOKUP_CHOSENADDRESS", chosenAddress },
				{ "WASTECOLLECTIONDAYS202627_LOOKUP_TESTDATELAYOUT", "false" },
				{ "WASTECOLLECTIONDAYS202627_FORMACTION_NEXT", "WASTECOLLECTIONDAYS202627_LOOKUP_FIELD2" },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"https://www.e-lindsey.gov.uk/apiserver/formsservice/http/processsubmission?pageSessionId={pageSessionId}&fsid={sessionId}&fsn={nonce}",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
					{ "cookie", $"goss-formsservice-clientid={sessionId}" },
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
		else if (clientSideResponse.RequestId == 2)
		{
			var encodedResultsData = ResultsDataRegex().Match(clientSideResponse.Content).Groups["data"].Value;
			var decodedResultsData = Encoding.UTF8.GetString(Convert.FromBase64String(encodedResultsData));

			using var jsonDoc = JsonDocument.Parse(decodedResultsData);
			var rawResult = jsonDoc.RootElement.GetProperty("result")[0];

			var dateKeys = new[]
			{
				"wastenextref",
				"wastenextrec",
				"wastenextpur",
				"greenfirst",
			};

			// Iterate through each explicit date field, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var dateKey in dateKeys)
			{
				var rawDate = rawResult.GetProperty(dateKey).GetString()!;
				if (string.IsNullOrWhiteSpace(rawDate))
				{
					continue;
				}

				var cleanedDate = OrdinalSuffixRegex().Replace(rawDate, string.Empty).Trim();
				var date = DateUtilities.ParseDateExact(cleanedDate, "dddd d MMMM yyyy");

				if (date < DateOnly.FromDateTime(DateTime.UtcNow))
				{
					continue;
				}

				var bins = ProcessingUtilities.GetMatchingBins(_binTypes, dateKey);
				if (bins.Count == 0)
				{
					continue;
				}

				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = bins,
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
}
