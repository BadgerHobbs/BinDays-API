namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Bradford Council.
/// </summary>
internal sealed partial class BradfordCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Bradford Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.bradford.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "bradford";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General waste",
			Colour = BinColour.Green,
			Keys = [ "General waste" ],
		},
		new()
		{
			Name = "Recycling waste",
			Colour = BinColour.Grey,
			Keys = [ "Recycling waste" ],
		},
		new()
		{
			Name = "Garden waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden waste" ],
		},
	];

	/// <summary>
	/// The form identifier for the collection dates form.
	/// </summary>
	private const string _formId = "/Forms/COLLECTIONDATES";

	/// <summary>
	/// The field identifier for the postcode input.
	/// </summary>
	private const string _postcodeField = "CTRL:Q2YAUZ5b:_:A";

	/// <summary>
	/// The field identifier for showing collections.
	/// </summary>
	private const string _showCollectionsField = "CTID-PieY14aw-_";

	/// <summary>
	/// The hidden inputs for the show collections request.
	/// </summary>
	private const string _showCollectionsHidInputs = "ACTRL:PieY14aw:_,ACTRL:EstZqKRj:_,APAGE:E.h,APAGE:B.h,APAGE:N.h,APAGE:P.h,APAGE:S.h,APAGE:R.h";

	/// <summary>
	/// Regex for the formstack value from the HTML.
	/// </summary>
	[GeneratedRegex("name=\"formstack\" value=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase)]
	private static partial Regex FormstackRegex();

	/// <summary>
	/// Regex for the original request url from the HTML.
	/// </summary>
	[GeneratedRegex("name=\"origrequrl\" value=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase)]
	private static partial Regex OrigRequestUrlRegex();

	/// <summary>
	/// Regex for the ebs value from the HTML.
	/// </summary>
	[GeneratedRegex("name=\"ebs\" value=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase)]
	private static partial Regex EbsRegex();

	/// <summary>
	/// Regex for the page sequence value from the HTML.
	/// </summary>
	[GeneratedRegex("name=\"pageSeq\" value=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase)]
	private static partial Regex PageSequenceRegex();

	/// <summary>
	/// Regex for the page id value from the HTML.
	/// </summary>
	[GeneratedRegex("name=\"pageId\" value=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase)]
	private static partial Regex PageIdRegex();

	/// <summary>
	/// Regex for the form state id value from the HTML.
	/// </summary>
	[GeneratedRegex("name=\"formStateId\" value=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase)]
	private static partial Regex FormStateRegex();

	/// <summary>
	/// Regex for the addresses from the data.
	/// </summary>
	[GeneratedRegex("data-eb-input-name=\"(?<field>CTRL:Go9IHRTP:\\d+:B\\.h)\"[^>]*>(?<address>[^<]+)</a>", RegexOptions.IgnoreCase)]
	private static partial Regex AddressesRegex();

	/// <summary>
	/// Regex for the bin sections on the collection page.
	/// </summary>
	[GeneratedRegex("<h3>(?<service>[^<]+)</h3>(?<content>.*?)((?=<h3>)|\\z)", RegexOptions.Singleline)]
	private static partial Regex BinSectionRegex();

	/// <summary>
	/// Regex for the bin day dates.
	/// </summary>
	[GeneratedRegex("[A-Za-z]{3} [A-Za-z]{3} \\d{2} \\d{4}")]
	private static partial Regex DateRegex();

	/// <summary>
	/// Regex for the html property value from the JSON response.
	/// </summary>
	[GeneratedRegex(@"""html""\s*:\s*""((?:[^""\\]|\\.)*)""", RegexOptions.IgnoreCase)]
	private static partial Regex HtmlPropertyRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Handle session initialization (RequestIds null, 1, 2)
		var sharedRequest = HandleSessionInitialization(postcode, clientSideResponse);
		if (sharedRequest != null)
		{
			return new GetAddressesResponse { NextClientSideRequest = sharedRequest };
		}
		// Process addresses from response
		else if (clientSideResponse!.RequestId == 3)
		{
			var addressesHtml = ExtractUpdatedHtml(clientSideResponse.Content, "Go9IHRTP");

			var rawAddresses = AddressesRegex().Matches(addressesHtml)!;

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				var address = new Address
				{
					Property = WebUtility.HtmlDecode(rawAddress.Groups["address"].Value.Trim()),
					Postcode = postcode,
					Uid = rawAddress.Groups["field"].Value,
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
		// Handle session initialization (RequestIds null, 1, 2)
		var sharedRequest = HandleSessionInitialization(address.Postcode!, clientSideResponse);
		if (sharedRequest != null)
		{
			return new GetBinDaysResponse { NextClientSideRequest = sharedRequest };
		}
		// Prepare client-side request for selecting the address
		else if (clientSideResponse!.RequestId == 3)
		{
			var addressesHtml = ExtractUpdatedHtml(clientSideResponse.Content, "Go9IHRTP");
			var rawAddresses = AddressesRegex().Matches(addressesHtml)!;

			var addressFields = new List<(string Field, string Property)>();
			foreach (Match rawAddress in rawAddresses)
			{
				addressFields.Add((rawAddress.Groups["field"].Value, WebUtility.HtmlDecode(rawAddress.Groups["address"].Value.Trim())));
			}

			var selectedAddress = addressFields.Find(x =>
				string.Equals(x.Field, address.Uid, StringComparison.OrdinalIgnoreCase)
			);

			if (selectedAddress == default)
			{
				selectedAddress = addressFields.Find(x =>
					string.Equals(x.Property, address.Property, StringComparison.OrdinalIgnoreCase)
				);
			}

			if (selectedAddress == default)
			{
				throw new InvalidOperationException("Selected address not found.");
			}

			var metadata = clientSideResponse.Options.Metadata;

			Dictionary<string, string> requestBodyDictionary = new()
			{
				{"formid", _formId},
				{"ebs", metadata["ebs"]},
				{"origrequrl", metadata["origRequestUrl"]},
				{"formstack", metadata["formstack"]},
				{"PAGE:F", "CTID-Go9IHRTP-1-A"},
				{"pageSeq", metadata["pageSeq"]},
				{"pageId", metadata["pageId"]},
				{"formStateId", metadata["formStateId"]},
				{_postcodeField, address.Postcode!},
			};

			// Iterate through each address, and create the request fields
			foreach (var rawAddress in addressFields)
			{
				requestBodyDictionary.Add(rawAddress.Field, rawAddress == selectedAddress
					? rawAddress.Property
					: string.Empty);
			}

			var hidInputs = new List<string>
			{
				"ICTRL:Q2YAUZ5b:_:A",
				"ACTRL:2eDPaBQA:_",
			};

			foreach (var (Field, Property) in addressFields)
			{
				hidInputs.Add(Field.Replace("CTRL:", "ACTRL:"));
			}

			hidInputs.Add("APAGE:E.h");
			hidInputs.Add("APAGE:B.h");
			hidInputs.Add("APAGE:N.h");
			hidInputs.Add("APAGE:P.h");
			hidInputs.Add("APAGE:S.h");
			hidInputs.Add("APAGE:R.h");

			requestBodyDictionary.Add("HID:inputs", string.Join(",", hidInputs));

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(requestBodyDictionary);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 4,
				Url = $"https://onlineforms.bradford.gov.uk/ufs/ufsajax?ebz={metadata["ebs"]}",
				Method = "POST",
				Headers = new() {
					{"user-agent", Constants.UserAgent},
					{"content-type", "application/x-www-form-urlencoded"},
					{"cookie", metadata["cookie"]},
				},
				Body = requestBody,
				Options = new ClientSideOptions
				{
					Metadata = metadata,
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for showing the collection dates
		else if (clientSideResponse.RequestId == 4)
		{
			var metadata = clientSideResponse.Options.Metadata;

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{"formid", _formId},
				{"ebs", metadata["ebs"]},
				{"origrequrl", metadata["origRequestUrl"]},
				{"formstack", metadata["formstack"]},
				{"PAGE:F", _showCollectionsField},
				{"ufsEndUser*", "1"},
				{"pageSeq", metadata["pageSeq"]},
				{"pageId", metadata["pageId"]},
				{"formStateId", metadata["formStateId"]},
				{"HID:inputs", _showCollectionsHidInputs},
				{"CTRL:PieY14aw:_", "Show collection dates"},
			}
			);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 5,
				Url = $"https://onlineforms.bradford.gov.uk/ufs/ufsajax?ebz={metadata["ebs"]}",
				Method = "POST",
				Headers = new() {
					{"user-agent", Constants.UserAgent},
					{"content-type", "application/x-www-form-urlencoded"},
					{"cookie", metadata["cookie"]},
				},
				Body = requestBody,
				Options = new ClientSideOptions
				{
					Metadata = metadata,
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for navigating to the collection dates page
		else if (clientSideResponse.RequestId == 5)
		{
			var metadata = clientSideResponse.Options.Metadata;

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{"formid", _formId},
				{"ebs", metadata["ebs"]},
				{"origrequrl", metadata["origRequestUrl"]},
				{"formstack", metadata["formstack"]},
				{"PAGE:F", _showCollectionsField},
				{"ufsEndUser*", "1"},
				{"pageSeq", metadata["pageSeq"]},
				{"pageId", metadata["pageId"]},
				{"formStateId", metadata["formStateId"]},
				{"HID:inputs", _showCollectionsHidInputs},
				{"ebReshow", "true"},
			}
			);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 6,
				Url = $"https://onlineforms.bradford.gov.uk/ufs/collectiondates.eb?ebz={metadata["ebs"]}",
				Method = "POST",
				Headers = new() {
					{"user-agent", Constants.UserAgent},
					{"content-type", "application/x-www-form-urlencoded"},
					{"cookie", metadata["cookie"]},
				},
				Body = requestBody,
				Options = new ClientSideOptions
				{
					Metadata = metadata,
					FollowRedirects = false,
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for loading the collection dates page
		else if (clientSideResponse.RequestId == 6)
		{
			var metadata = clientSideResponse.Options.Metadata;
			var location = clientSideResponse.Headers["location"];

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 7,
				Url = $"https://onlineforms.bradford.gov.uk/ufs/{location}",
				Method = "GET",
				Headers = new() {
					{"user-agent", Constants.UserAgent},
					{"cookie", metadata["cookie"]},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin days from the response
		else if (clientSideResponse.RequestId == 7)
		{
			var binSections = BinSectionRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each bin section, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match binSection in binSections)
			{
				var service = binSection.Groups["service"].Value.Trim();
				var datesText = binSection.Groups["content"].Value;

				foreach (Match dateMatch in DateRegex().Matches(datesText)!)
				{
					var date = DateOnly.ParseExact(
						dateMatch.Value,
						"ddd MMM dd yyyy",
						CultureInfo.InvariantCulture,
						DateTimeStyles.None
					);

					var matchingBins = ProcessingUtilities.GetMatchingBins(_binTypes, service);

					var binDay = new BinDay
					{
						Date = date,
						Address = address,
						Bins = matchingBins,
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

	/// <summary>
	/// Handles the shared session initialization steps (RequestIds null, 1, 2) used by both
	/// <see cref="GetAddresses"/> and <see cref="GetBinDays"/>.
	/// </summary>
	/// <param name="postcode">The postcode to search for.</param>
	/// <param name="clientSideResponse">The client-side response from the previous request, or null for the initial request.</param>
	/// <returns>A <see cref="ClientSideRequest"/> for RequestIds null/1/2, or null when shared steps are complete.</returns>
	private static ClientSideRequest? HandleSessionInitialization(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting the initial token
		if (clientSideResponse == null)
		{
			return new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://onlineforms.bradford.gov.uk/ufs/collectiondates.eb?ebd=0&ebp=20&ebz=1_1761729510565",
				Method = "GET",
				Headers = new() {
					{"user-agent", Constants.UserAgent},
				},
				Options = new ClientSideOptions
				{
					FollowRedirects = false,
				},
			};
		}
		// Prepare client-side request for loading the form
		else if (clientSideResponse.RequestId == 1)
		{
			var cookie = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(
				clientSideResponse.Headers["set-cookie"]
			);
			var location = clientSideResponse.Headers["location"];

			return new ClientSideRequest
			{
				RequestId = 2,
				Url = $"https://onlineforms.bradford.gov.uk/ufs/{location}",
				Method = "GET",
				Headers = new() {
					{"user-agent", Constants.UserAgent},
					{"cookie", cookie},
				},
				Options = new ClientSideOptions
				{
					Metadata = {
						{ "cookie", cookie },
					},
				},
			};
		}
		// Prepare client-side request for searching for addresses
		else if (clientSideResponse.RequestId == 2)
		{
			var html = clientSideResponse.Content;
			var ebs = EbsRegex().Match(html).Groups["value"].Value;
			var formstack = FormstackRegex().Match(html).Groups["value"].Value;
			var origRequestUrl = OrigRequestUrlRegex().Match(html).Groups["value"].Value.Replace("&amp;", "&");
			var pageSequence = PageSequenceRegex().Match(html).Groups["value"].Value;
			var pageId = PageIdRegex().Match(html).Groups["value"].Value;
			var formStateId = FormStateRegex().Match(html).Groups["value"].Value;

			Dictionary<string, string> metadata = new()
			{
				{ "cookie", clientSideResponse.Options.Metadata["cookie"] },
				{ "ebs", ebs },
				{ "formstack", formstack },
				{ "origRequestUrl", origRequestUrl },
				{ "pageSeq", pageSequence },
				{ "pageId", pageId },
				{ "formStateId", formStateId },
			};

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{"formid", _formId},
				{"ebs", ebs},
				{"origrequrl", origRequestUrl},
				{"formstack", formstack},
				{"PAGE:F", "CTID-2eDPaBQA-_"},
				{"pageSeq", pageSequence},
				{"pageId", pageId},
				{"formStateId", formStateId},
				{_postcodeField, postcode},
				{"HID:inputs", "ICTRL:Q2YAUZ5b:_:A,ACTRL:2eDPaBQA:_,APAGE:E.h,APAGE:B.h,APAGE:N.h,APAGE:P.h,APAGE:S.h,APAGE:R.h"},
				{"CTRL:2eDPaBQA:_", "Find address"},
			}
			);

			return new ClientSideRequest
			{
				RequestId = 3,
				Url = $"https://onlineforms.bradford.gov.uk/ufs/ufsajax?ebz={ebs}",
				Method = "POST",
				Headers = new() {
					{"user-agent", Constants.UserAgent},
					{"content-type", "application/x-www-form-urlencoded"},
					{"cookie", metadata["cookie"]},
				},
				Body = requestBody,
				Options = new ClientSideOptions
				{
					Metadata = metadata,
				},
			};
		}

		return null;
	}

	/// <summary>
	/// Extracts HTML content from a JSON response containing updated controls.
	/// </summary>
	/// <param name="content">The JSON content to parse.</param>
	/// <param name="identifier">The identifier to search for in the HTML content.</param>
	/// <returns>The HTML content from the matching control.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the updated HTML content cannot be found.</exception>
	private static string ExtractUpdatedHtml(string content, string identifier)
	{
		foreach (Match match in HtmlPropertyRegex().Matches(content))
		{
			var html = match.Groups[1].Value;
			if (html.Contains(identifier, StringComparison.OrdinalIgnoreCase))
			{
				return html.Replace("\\\"", "\"");
			}
		}

		throw new InvalidOperationException("Could not find updated HTML content.");
	}
}
