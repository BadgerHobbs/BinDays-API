namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Argyll and Bute Council.
/// </summary>
internal sealed partial class ArgyllAndButeCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Argyll and Bute Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.argyll-bute.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "argyll-and-bute";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Keys = [ "General" ],
		},
		new()
		{
			Name = "Paper, Cardboard, Plastics and Cartons Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Recycling" ],
		},
		new()
		{
			Name = "Glass Recycling",
			Colour = BinColour.Grey,
			Keys = [ "Glass" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Brown,
			Keys = [ "Food" ],
			Type = BinType.Caddy,
		},
	];

	/// <summary>
	/// The bin collection page URL.
	/// </summary>
	private const string _binCollectionUrl = "https://www.argyll-bute.gov.uk/rubbish-and-recycling/household-waste/bin-collection";

	/// <summary>
	/// The Drupal form identifier.
	/// </summary>
	private const string _formId = "abc_bins_dates_lookup_form";

	/// <summary>
	/// Regex for the form build id.
	/// </summary>
	[GeneratedRegex(@"name=""form_build_id""\s+value=""(?<formBuildId>[^""]+)""")]
	private static partial Regex FormBuildIdRegex();

	/// <summary>
	/// Regex for the addresses from the data.
	/// </summary>
	[GeneratedRegex(@"<option value=""(?<uid>[^""]*)"">(?<address>[^<]+)</option>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for the bin days from the data.
	/// </summary>
	[GeneratedRegex(@"<tr class=""[^""]+"">\s*<td>(?<service>[^<]+)</td>\s*<td>(?<collectionDate>[^<]+)</td>")]
	private static partial Regex BinDayRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _binCollectionUrl,
				Method = "GET",
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		else if (clientSideResponse.RequestId == 1)
		{
			// Prepare client-side request for posting the postcode
			var formBuildId = FormBuildIdRegex().Match(clientSideResponse.Content).Groups["formBuildId"].Value;

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = _binCollectionUrl,
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
				},
				Body = ProcessingUtilities.ConvertDictionaryToFormData(new()
				{
					{ "postcode", postcode },
					{ "form_build_id", formBuildId },
					{ "form_id", _formId },
				}),
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		else if (clientSideResponse.RequestId == 2)
		{
			// Process addresses from response
			var formBuildId = FormBuildIdRegex().Match(clientSideResponse.Content).Groups["formBuildId"].Value;
			var rawAddresses = AddressRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				var uid = rawAddress.Groups["uid"].Value.Trim();

				if (string.IsNullOrWhiteSpace(uid))
				{
					continue;
				}

				var address = new Address
				{
					Property = rawAddress.Groups["address"].Value.Trim(),
					Postcode = postcode,
					// Uid format: "{uid};{form_build_id}".
					Uid = $"{uid};{formBuildId}",
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
		// Uid format is "{uid};{form_build_id}" for addresses returned by GetAddresses.
		// Fall back to the legacy 3-step flow for older cached UIDs that contain only the uid.
		var uidParts = address.Uid!.Split(';', 2);
		var uid = uidParts[0];
		var embeddedFormBuildId = uidParts.Length == 2 ? uidParts[1] : null;

		if (embeddedFormBuildId != null)
		{
			if (clientSideResponse == null)
			{
				var postcode = address.Postcode!;

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = _binCollectionUrl,
					Method = "POST",
					Headers = new()
					{
						{ "user-agent", Constants.UserAgent },
						{ "content-type", Constants.FormUrlEncoded },
					},
					Body = ProcessingUtilities.ConvertDictionaryToFormData(new()
					{
						{ "postcode", postcode },
						{ "address", uid },
						{ "form_build_id", embeddedFormBuildId },
						{ "form_id", _formId },
					}),
				};

				var getBinDaysResponse = new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest,
				};

				return getBinDaysResponse;
			}
			else if (clientSideResponse.RequestId == 1)
			{
				var getBinDaysResponse = ProcessBinDaysResponse(address, clientSideResponse.Content);

				return getBinDaysResponse;
			}
		}
		else
		{
			// Prepare client-side request for getting the form
			if (clientSideResponse == null)
			{
				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = _binCollectionUrl,
					Method = "GET",
				};

				var getBinDaysResponse = new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest,
				};

				return getBinDaysResponse;
			}
			else if (clientSideResponse.RequestId == 1)
			{
				// Prepare client-side request for posting the postcode
				var formBuildId = FormBuildIdRegex().Match(clientSideResponse.Content).Groups["formBuildId"].Value;
				var postcode = address.Postcode!;

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 2,
					Url = _binCollectionUrl,
					Method = "POST",
					Headers = new()
					{
						{ "user-agent", Constants.UserAgent },
						{ "content-type", Constants.FormUrlEncoded },
					},
					Body = ProcessingUtilities.ConvertDictionaryToFormData(new()
					{
						{ "postcode", postcode },
						{ "form_build_id", formBuildId },
						{ "form_id", _formId },
					}),
				};

				var getBinDaysResponse = new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest,
				};

				return getBinDaysResponse;
			}
			else if (clientSideResponse.RequestId == 2)
			{
				// Prepare client-side request for posting the selected address
				var formBuildId = FormBuildIdRegex().Match(clientSideResponse.Content).Groups["formBuildId"].Value;
				var postcode = address.Postcode!;

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 3,
					Url = _binCollectionUrl,
					Method = "POST",
					Headers = new()
					{
						{ "user-agent", Constants.UserAgent },
						{ "content-type", Constants.FormUrlEncoded },
					},
					Body = ProcessingUtilities.ConvertDictionaryToFormData(new()
					{
						{ "postcode", postcode },
						{ "address", uid },
						{ "form_build_id", formBuildId },
						{ "form_id", _formId },
					}),
				};

				var getBinDaysResponse = new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest,
				};

				return getBinDaysResponse;
			}
			else if (clientSideResponse.RequestId == 3)
			{
				var getBinDaysResponse = ProcessBinDaysResponse(address, clientSideResponse.Content);

				return getBinDaysResponse;
			}
		}

		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <summary>
	/// Processes bin days from the response content.
	/// </summary>
	private GetBinDaysResponse ProcessBinDaysResponse(Address address, string content)
	{
		var rawBinDays = BinDayRegex().Matches(content)!;

		// Iterate through each bin day, and create a new bin day object
		var binDays = new List<BinDay>();
		foreach (Match rawBinDay in rawBinDays)
		{
			var service = rawBinDay.Groups["service"].Value.Trim();
			var collectionDate = rawBinDay.Groups["collectionDate"].Value.Trim();

			var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, service);

			var binDay = new BinDay
			{
				Date = DateUtilities.ParseDateInferringYear(collectionDate, "dddd d MMMM"),
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
}
