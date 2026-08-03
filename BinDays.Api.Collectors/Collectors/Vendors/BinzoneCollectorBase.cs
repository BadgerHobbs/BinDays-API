namespace BinDays.Api.Collectors.Collectors.Vendors;

using BinDays.Api.Collectors.Exceptions;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Base collector implementation for councils using the South and Vale BinDays API.
/// </summary>
internal abstract partial class BinzoneCollectorBase : GovUkCollectorBase
{
	/// <summary>
	/// The council code used by the BinDays API ("S" for South Oxfordshire or "V" for Vale).
	/// </summary>
	protected abstract string CouncilCode { get; }

	/// <summary>
	/// The base URL of the legacy ebase Binzone form (e.g. "https://eform.southoxon.gov.uk"), used only
	/// to resolve legacy UIDs. See the remarks on <see cref="GetLegacyBinDays"/>.
	/// </summary>
	protected abstract string EformBaseUrl { get; }

	/// <summary>
	/// The service identifier used in the legacy ebase form's SOVA_TAG query parameter ("SOUTH" or "VALE").
	/// </summary>
	protected abstract string ServiceId { get; }

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "Rubbish",
			Colour = BinColour.Black,
			Keys = [ "Non-recyclable refuse waste" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Green,
			Keys = [ "Recycling" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Green,
			Keys = [ "Food waste" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden Waste subscribers" ],
		},
		new()
		{
			Name = "Small Electrical Items",
			Colour = BinColour.Any,
			Keys = [ "Small electricals" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Textiles",
			Colour = BinColour.Any,
			Keys = [ "Textiles/Clothes" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Batteries",
			Colour = BinColour.Clear,
			Keys = [ "Batteries" ],
			Type = BinType.Bag,
		},
	];

	/// <summary>
	/// The base URL for the BinDays property API.
	/// </summary>
	private const string _propertyApiBaseUrl = "https://forms.southandvale.gov.uk/api/property";

	/// <summary>
	/// Regex for the ebz/ebs token value from a legacy ebase form URL.
	/// </summary>
	[GeneratedRegex("ebz=([^&]+)")]
	private static partial Regex EbzRegex();

	/// <summary>
	/// Regex for extracting addresses and their corresponding control IDs from the legacy ebase form's
	/// address search results HTML.
	/// </summary>
	[GeneratedRegex(@"(?s)class=""[^""]*eb-58-fieldHyperlink[^""]*""[^>]*>\s*(?<address>[^<]+)\s*</a>.*?name=""(?<uid>CTRL:63:_:D:\d+)""")]
	private static partial Regex LegacyAddressRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_propertyApiBaseUrl}/postcode/{postcode}",
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
			using var jsonDocument = JsonDocument.Parse(clientSideResponse.Content);
			var setData = jsonDocument.RootElement.GetProperty("setData");

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var addressItem in setData.EnumerateArray())
			{
				var council = addressItem.GetProperty("council").GetString()!;

				if (council != CouncilCode)
				{
					continue;
				}

				var address = new Address
				{
					Property = addressItem.GetProperty("address").GetString()!,
					Postcode = postcode,
					Uid = addressItem.GetProperty("uprn").GetString()!,
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
		// TODO: Remove once legacy UIDs are no longer in circulation. Addresses are cached both
		// server-side (30-day TTL) and client-side (indefinitely), so this may need to stay long-term.
		// Clients that cached addresses before the migration to the South and Vale JSON API still
		// hold UIDs in the legacy ebase Binzone form format (CTRL:63:_:D:N). These cannot be used
		// directly as UPRNs, so they are resolved via the legacy form before fetching bin days.
		if (address.Uid!.Contains(':'))
		{
			return GetLegacyBinDays(address, clientSideResponse);
		}

		// Prepare client-side request for getting bin days
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_propertyApiBaseUrl}/bins/{address.Uid}",
				Method = "GET",
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
			return ParseBinDaysResponse(clientSideResponse.Content, address);
		}

		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <summary>
	/// Handles bin days requests for legacy UIDs (CTRL:63:_:D:N format).
	/// </summary>
	/// <remarks>
	/// The legacy UID only encodes the address's position in the old ebase form's address list, not a
	/// stable identifier. That position does not correspond to the position of the same address in the
	/// new South and Vale JSON API's address list (the two systems order addresses differently), so
	/// resolving it by index against the new API returns the wrong address (or throws, if the index is
	/// out of range). Instead, the old ebase form -- which is still live -- is queried fresh to recover
	/// the actual address text for the legacy UID, which is then matched by content (not position)
	/// against the new API's address list to find the correct UPRN.
	/// </remarks>
	private GetBinDaysResponse GetLegacyBinDays(Address address, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for the legacy form's initial session redirect
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{EformBaseUrl}/ebase/ufsmain?formid=BINZONE_DESKTOP&SOVA_TAG={ServiceId}",
				Method = "GET",
				Options = new ClientSideOptions
				{
					// We need to trap the 302 to get the Location header
					FollowRedirects = false,
				},
			};

			return new GetBinDaysResponse { NextClientSideRequest = clientSideRequest };
		}
		// Prepare client-side request for initializing the legacy form's session
		else if (clientSideResponse.RequestId == 1)
		{
			var cookie = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);
			var relativeLocation = clientSideResponse.Headers["location"];
			var fullRedirectUrl = $"{EformBaseUrl}/ebase/{relativeLocation}";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = fullRedirectUrl,
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "cookie", cookie },
				},
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "cookie", cookie },
						{ "referer", fullRedirectUrl },
					},
				},
			};

			return new GetBinDaysResponse { NextClientSideRequest = clientSideRequest };
		}
		// Prepare client-side request for performing the legacy form's postcode search
		else if (clientSideResponse.RequestId == 2)
		{
			var cookie = clientSideResponse.Options.Metadata["cookie"];
			var refererUrl = clientSideResponse.Options.Metadata["referer"];
			var ebs = EbzRegex().Match(refererUrl).Groups[1].Value;

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "formid", "/Forms/BINZONE_DESKTOP" },
				{ "ebs", ebs },
				{ "formstack", "BINZONE_DESKTOP:f267e852-5fff-456e-96d7-83cd429c5109" },
				{ "pageSeq", "1" },
				{ "pageId", "WHERE_DO_YOU_LIVE" },
				{ "formStateId", "1" },
				{ "CTRL:2:_:A", address.Postcode! },
				{ "CTRL:20:_", "Search" },
				{ "HID:inputs", "ICTRL:2:_:A,ACTRL:20:_,ACTRL:24:_,ICTRL:70:_:A,ICTRL:31:_:A,ICTRL:32:_:A,APAGE:E.h,APAGE:B.h,APAGE:N.h,APAGE:S.h,APAGE:R.h" },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = $"{EformBaseUrl}/ebase/BINZONE_DESKTOP.eb?ebz={ebs}",
				Method = "POST",
				Body = requestBody,
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "cookie", cookie },
					{ "content-type", Constants.FormUrlEncoded },
				},
			};

			return new GetBinDaysResponse { NextClientSideRequest = clientSideRequest };
		}
		// Resolve the legacy UID's address text from the legacy form's search results, then prepare
		// client-side request for the new API's postcode lookup
		else if (clientSideResponse.RequestId == 3)
		{
			string? legacyAddressText = null;

			foreach (Match legacyMatch in LegacyAddressRegex().Matches(clientSideResponse.Content))
			{
				if (legacyMatch.Groups["uid"].Value != address.Uid)
				{
					continue;
				}

				legacyAddressText = WebUtility.HtmlDecode(legacyMatch.Groups["address"].Value).Trim();
				break;
			}

			if (legacyAddressText == null)
			{
				throw new BinDaysNotFoundException(GovUkId, address.Postcode!, address.Uid!);
			}

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 4,
				Url = $"{_propertyApiBaseUrl}/postcode/{address.Postcode}",
				Method = "GET",
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "legacyAddress", legacyAddressText },
					},
				},
			};

			return new GetBinDaysResponse { NextClientSideRequest = clientSideRequest };
		}
		// Match the resolved address text against the new API's address list to find the UPRN, then
		// prepare client-side request for getting bin days
		else if (clientSideResponse.RequestId == 4)
		{
			var legacyAddressText = clientSideResponse.Options.Metadata["legacyAddress"];

			using var jsonDocument = JsonDocument.Parse(clientSideResponse.Content);
			var setData = jsonDocument.RootElement.GetProperty("setData");

			string? uprn = null;

			foreach (var addressItem in setData.EnumerateArray())
			{
				if (addressItem.GetProperty("council").GetString() != CouncilCode)
				{
					continue;
				}

				if (!string.Equals(addressItem.GetProperty("address").GetString(), legacyAddressText, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				uprn = addressItem.GetProperty("uprn").GetString();
				break;
			}

			if (uprn == null)
			{
				throw new BinDaysNotFoundException(GovUkId, address.Postcode!, address.Uid!);
			}

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 5,
				Url = $"{_propertyApiBaseUrl}/bins/{uprn}",
				Method = "GET",
			};

			return new GetBinDaysResponse { NextClientSideRequest = clientSideRequest };
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 5)
		{
			return ParseBinDaysResponse(clientSideResponse.Content, address);
		}

		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <summary>
	/// Parses a bin days response from the new South and Vale JSON API.
	/// </summary>
	private GetBinDaysResponse ParseBinDaysResponse(string content, Address address)
	{
		using var jsonDocument = JsonDocument.Parse(content);
		var setData = jsonDocument.RootElement.GetProperty("setData");
		var binDays = new List<BinDay>();

		if (setData.GetProperty("site").GetString()! != CouncilCode)
		{
			throw new InvalidOperationException("Address does not belong to this council.");
		}

		// Iterate through each collection week
		foreach (var week in setData.GetProperty("week").EnumerateArray())
		{
			// Iterate through each collection day, and create bin day objects
			foreach (var day in week.GetProperty("day").EnumerateArray())
			{
				var collectionDate = day.GetProperty("collection_date").GetString()!;
				var date = DateUtilities.ParseDateExact(collectionDate, "dd/MM/yyyy");

				// Iterate through each bin entry for the collection day
				foreach (var bin in day.GetProperty("bins").EnumerateArray())
				{
					var service = bin.GetProperty("bin_type").GetString()!;
					var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, service);

					if (matchedBins.Count == 0)
					{
						continue;
					}

					var binDay = new BinDay
					{
						Date = date,
						Address = address,
						Bins = matchedBins,
					};

					binDays.Add(binDay);
				}
			}
		}

		return new GetBinDaysResponse
		{
			BinDays = ProcessingUtilities.ProcessBinDays(binDays),
		};
	}
}
