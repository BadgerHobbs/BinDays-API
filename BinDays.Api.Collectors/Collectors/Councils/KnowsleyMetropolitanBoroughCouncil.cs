namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Collector implementation for Knowsley Metropolitan Borough Council.
/// </summary>
internal sealed class KnowsleyMetropolitanBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Knowsley Metropolitan Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.knowsley.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "knowsley";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Red,
			Keys = [ "Maroon" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Grey,
			Keys = [ "Grey" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Blue,
			Keys = [ "Blue" ],
		},
	];

	/// <summary>
	/// The base URL for the Mendix service.
	/// </summary>
	private const string _baseUrl = "https://knowsleytransaction.mendixcloud.com";

	/// <summary>
	/// The validation guid required by the Mendix runtime operations.
	/// </summary>
	private const string _validationGuid = "437412113808372365";

	/// <summary>
	/// The advertised bin collection financial year label.
	/// </summary>
	private const string _binCollectionFinancialYear = "Dec 2025 - Mar 2026";

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}/link/youarebeingredirected?target=bincollectioninformation",
				Method = "GET",
				Options = new ClientSideOptions
				{
					FollowRedirects = false,
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		else if (clientSideResponse.RequestId == 1)
		{
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			var requestBody = $$"""
{
  "action":"get_session_data",
  "params":{
    "hybrid":false,
    "offline":false,
    "referrer":null,
    "profile":"",
    "timezoneoffset":0,
    "timezoneId":"UTC",
    "preferredLanguages":[
      "undefined"
    ],
    "version":2
  },
  "profiledata":{
    "{{timestamp}}-0":24
  }
}
""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseUrl}/xas/",
				Method = "POST",
				Headers = CreateHeaders(cookies, null, CreateRequestToken(1)),
				Body = requestBody,
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "cookie", cookies },
					},
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		else if (clientSideResponse.RequestId == 2)
		{
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var parsedCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);
			var existingCookies = clientSideResponse.Options.Metadata["cookie"];
			var cookies = string.IsNullOrWhiteSpace(parsedCookies) ? existingCookies : $"{existingCookies}; {parsedCookies}";
			var cookiesWithOrigin = $"{cookies}; originURI=/login.html";

			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var csrfToken = jsonDoc.RootElement.GetProperty("csrftoken").GetString()!;
			var redirectObject = jsonDoc.RootElement.GetProperty("objects");
			var redirectGuid = string.Empty;
			var redirectHash = string.Empty;

			foreach (var redirectCandidate in redirectObject.EnumerateArray())
			{
				if (redirectCandidate.GetProperty("objectType").GetString() == "Service_YouAreBeingRedirected.YouAreBeingRedirected_Redirect")
				{
					redirectGuid = redirectCandidate.GetProperty("guid").GetString()!;
					redirectHash = redirectCandidate.GetProperty("hash").GetString()!;
					break;
				}
			}

			var executeActionProfileKey = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-3";

			var requestBody = $$"""
{
  "action":"executeaction",
  "params":{
    "actionname":"Service_YouAreBeingRedirected.SUB_YouAreBeingRedirected",
    "applyto":"selection",
    "guids":[
      "{{redirectGuid}}"
    ]
  },
  "changes":{},
  "objects":[
    {
      "attributes":{},
      "guid":"{{redirectGuid}}",
      "hash":"{{redirectHash}}",
      "objectType":"Service_YouAreBeingRedirected.YouAreBeingRedirected_Redirect"
    }
  ],
  "context":[],
  "profiledata":{
    "{{executeActionProfileKey}}":17
  }
}
""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = $"{_baseUrl}/xas/",
				Method = "POST",
				Headers = CreateHeaders(cookiesWithOrigin, csrfToken, CreateRequestToken(2)),
				Body = requestBody,
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "cookie", cookiesWithOrigin },
						{ "csrfToken", csrfToken },
						{ "redirectGuid", redirectGuid },
						{ "redirectHash", redirectHash },
					},
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		else if (clientSideResponse.RequestId == 3)
		{
			var setCookieHeader = clientSideResponse.Headers.ContainsKey("set-cookie")
				? clientSideResponse.Headers["set-cookie"]
				: string.Empty;
			var parsedCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);
			var existingCookies = clientSideResponse.Options.Metadata["cookie"];
			var cookies = string.IsNullOrWhiteSpace(parsedCookies) ? existingCookies : $"{existingCookies}; {parsedCookies}";
			var cookiesWithOrigin = $"{cookies}; originURI=/login.html";
			var csrfToken = clientSideResponse.Options.Metadata["csrfToken"];
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var objects = jsonDoc.RootElement.GetProperty("objects");
			var binCollectionGuid = string.Empty;
			var binCollectionHash = string.Empty;

			foreach (var obj in objects.EnumerateArray())
			{
				if (obj.GetProperty("objectType").GetString() == "OnlineServices.OS_vmBinCollectionEnquiry")
				{
					binCollectionGuid = obj.GetProperty("guid").GetString()!;
					binCollectionHash = obj.GetProperty("hash").GetString()!;
					break;
				}
			}

			var profileDataKey = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-6";
			var requestBody = $$"""
{
  "action":"retrieve",
  "params":{
    "queryId":"zs8SBEQS7kyR5QGTZ0Yg3Q",
    "params":{},
    "options":{}
  },
  "changes":{},
  "objects":[],
  "profiledata":{
    "{{profileDataKey}}":21
  }
}
""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 4,
				Url = $"{_baseUrl}/xas/",
				Method = "POST",
				Headers = CreateHeaders(cookiesWithOrigin, csrfToken, CreateRequestToken(3)),
				Body = requestBody,
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "cookie", cookiesWithOrigin },
						{ "csrfToken", csrfToken },
						{ "binCollectionGuid", binCollectionGuid },
						{ "binCollectionHash", binCollectionHash },
					},
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		else if (clientSideResponse.RequestId == 4)
		{
			var setCookieHeader = clientSideResponse.Headers.ContainsKey("set-cookie")
				? clientSideResponse.Headers["set-cookie"]
				: string.Empty;
			var parsedCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);
			var existingCookies = clientSideResponse.Options.Metadata["cookie"];
			var cookies = string.IsNullOrWhiteSpace(parsedCookies) ? existingCookies : $"{existingCookies}; {parsedCookies}";
			var cookiesWithOrigin = $"{cookies}; originURI=/login.html";
			var csrfToken = clientSideResponse.Options.Metadata["csrfToken"];
			var binCollectionGuid = clientSideResponse.Options.Metadata["binCollectionGuid"];
			var binCollectionHash = clientSideResponse.Options.Metadata["binCollectionHash"];

			var profileDataKey = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-1";
			var requestBody = $$"""
{
  "action":"runtimeOperation",
  "operationId":"VIPoQq+TS0aN/pBsFv3LUg",
  "params":{
    "OS_MissedBinEnquiry":{
      "guid":"{{binCollectionGuid}}"
    }
  },
  "validationGuids":[
    "{{binCollectionGuid}}",
    "{{_validationGuid}}"
  ],
  "changes":{
    "{{binCollectionGuid}}":{
      "BlueBinSuspensionMsg":{
        "value":""
      },
      "ShowBlueBinMessage":{
        "value":false
      },
      "BinCollectionFinancialYear":{
        "value":"{{_binCollectionFinancialYear}}"
      },
      "EnquiryPostcodeOrStreetName":{
        "value":"{{postcode}}"
      }
    }
  },
  "objects":[
    {
      "attributes":{
        "NextGrey":{
          "value":null
        },
        "CalendarPDF":{
          "value":null
        },
        "ShowAddressResults":{
          "value":false
        },
        "UPRN":{
          "value":null
        },
        "AddressSelected":{
          "value":false
        },
        "NextMaroon":{
          "value":null
        },
        "ShowBlueBinMessage":{
          "value":false
        },
        "EnquiryPostcodeOrStreetName":{
          "value":null
        },
        "BinCollectionFinancialYear":{
          "value":null
        },
        "BlueBinSuspensionMsg":{
          "value":null
        },
        "WasteGuidePDF":{
          "value":null
        },
        "NextBlue":{
          "value":null
        },
        "EnquiryAddress":{
          "value":null
        }
      },
      "guid":"{{binCollectionGuid}}",
      "hash":"{{binCollectionHash}}",
      "objectType":"OnlineServices.OS_vmBinCollectionEnquiry"
    }
  ],
  "profiledata":{
    "{{profileDataKey}}":26
  }
}
""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 5,
				Url = $"{_baseUrl}/xas/",
				Method = "POST",
				Headers = CreateHeaders(cookiesWithOrigin, csrfToken, CreateRequestToken(4)),
				Body = requestBody,
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		else if (clientSideResponse.RequestId == 5)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var changes = jsonDoc.RootElement.GetProperty("changes");

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var change in changes.EnumerateObject())
			{
				if (!change.Value.TryGetProperty("FullAddress", out var fullAddressProperty))
				{
					continue;
				}

				var fullAddress = fullAddressProperty.GetProperty("value").GetString()!.Trim();
				var uprn = change.Value.GetProperty("UPRN").GetProperty("value").GetString()!.Trim();
				var enquiryX = change.Value.GetProperty("EnquiryX").GetProperty("value").GetString()!.Trim();
				var enquiryY = change.Value.GetProperty("EnquiryY").GetProperty("value").GetString()!.Trim();
				var siteCode = change.Value.GetProperty("SiteCode").GetProperty("value").GetString()!.Trim();
				var street = change.Value.GetProperty("Street").GetProperty("value").GetString()!.Trim();
				var associationHash = change.Value.GetProperty("OnlineServices.OS_vmGeneric_Address_OS_vmBinCollectionEnquiry").GetProperty("hash").GetString()!.Trim();

				var uid = string.Join(
					';',
					new[]
					{
						change.Name,
						uprn,
						enquiryX,
						enquiryY,
						siteCode,
						street,
						fullAddress,
						associationHash,
					}
				);

				var address = new Address
				{
					Property = fullAddress,
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
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}/link/youarebeingredirected?target=bincollectioninformation",
				Method = "GET",
				Options = new ClientSideOptions
				{
					FollowRedirects = false,
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		else if (clientSideResponse.RequestId == 1)
		{
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			var requestBody = $$"""
{
  "action":"get_session_data",
  "params":{
    "hybrid":false,
    "offline":false,
    "referrer":null,
    "profile":"",
    "timezoneoffset":0,
    "timezoneId":"UTC",
    "preferredLanguages":[
      "undefined"
    ],
    "version":2
  },
  "profiledata":{
    "{{timestamp}}-0":24
  }
}
""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseUrl}/xas/",
				Method = "POST",
				Headers = CreateHeaders(cookies, null, CreateRequestToken(1)),
				Body = requestBody,
				Options = new ClientSideOptions
				{
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
		else if (clientSideResponse.RequestId == 2)
		{
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var parsedCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);
			var existingCookies = clientSideResponse.Options.Metadata["cookie"];
			var cookies = string.IsNullOrWhiteSpace(parsedCookies) ? existingCookies : $"{existingCookies}; {parsedCookies}";
			var cookiesWithOrigin = $"{cookies}; originURI=/login.html";

			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var csrfToken = jsonDoc.RootElement.GetProperty("csrftoken").GetString()!;
			var redirectObject = jsonDoc.RootElement.GetProperty("objects");
			var redirectGuid = string.Empty;
			var redirectHash = string.Empty;

			foreach (var redirectCandidate in redirectObject.EnumerateArray())
			{
				if (redirectCandidate.GetProperty("objectType").GetString() == "Service_YouAreBeingRedirected.YouAreBeingRedirected_Redirect")
				{
					redirectGuid = redirectCandidate.GetProperty("guid").GetString()!;
					redirectHash = redirectCandidate.GetProperty("hash").GetString()!;
					break;
				}
			}

			var executeActionProfileKey = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-3";

			var requestBody = $$"""
{
  "action":"executeaction",
  "params":{
    "actionname":"Service_YouAreBeingRedirected.SUB_YouAreBeingRedirected",
    "applyto":"selection",
    "guids":[
      "{{redirectGuid}}"
    ]
  },
  "changes":{},
  "objects":[
    {
      "attributes":{},
      "guid":"{{redirectGuid}}",
      "hash":"{{redirectHash}}",
      "objectType":"Service_YouAreBeingRedirected.YouAreBeingRedirected_Redirect"
    }
  ],
  "context":[],
  "profiledata":{
    "{{executeActionProfileKey}}":17
  }
}
""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = $"{_baseUrl}/xas/",
				Method = "POST",
				Headers = CreateHeaders(cookiesWithOrigin, csrfToken, CreateRequestToken(2)),
				Body = requestBody,
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "cookie", cookiesWithOrigin },
						{ "csrfToken", csrfToken },
						{ "redirectGuid", redirectGuid },
						{ "redirectHash", redirectHash },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		else if (clientSideResponse.RequestId == 3)
		{
			var setCookieHeader = clientSideResponse.Headers.ContainsKey("set-cookie")
				? clientSideResponse.Headers["set-cookie"]
				: string.Empty;
			var parsedCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);
			var existingCookies = clientSideResponse.Options.Metadata["cookie"];
			var cookies = string.IsNullOrWhiteSpace(parsedCookies) ? existingCookies : $"{existingCookies}; {parsedCookies}";
			var cookiesWithOrigin = $"{cookies}; originURI=/login.html";
			var csrfToken = clientSideResponse.Options.Metadata["csrfToken"];
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var objects = jsonDoc.RootElement.GetProperty("objects");
			var binCollectionGuid = string.Empty;
			var binCollectionHash = string.Empty;

			foreach (var obj in objects.EnumerateArray())
			{
				if (obj.GetProperty("objectType").GetString() == "OnlineServices.OS_vmBinCollectionEnquiry")
				{
					binCollectionGuid = obj.GetProperty("guid").GetString()!;
					binCollectionHash = obj.GetProperty("hash").GetString()!;
					break;
				}
			}

			var profileDataKey = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-6";
			var requestBody = $$"""
{
  "action":"retrieve",
  "params":{
    "queryId":"zs8SBEQS7kyR5QGTZ0Yg3Q",
    "params":{},
    "options":{}
  },
  "changes":{},
  "objects":[],
  "profiledata":{
    "{{profileDataKey}}":21
  }
}
""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 4,
				Url = $"{_baseUrl}/xas/",
				Method = "POST",
				Headers = CreateHeaders(cookiesWithOrigin, csrfToken, CreateRequestToken(3)),
				Body = requestBody,
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "cookie", cookiesWithOrigin },
						{ "csrfToken", csrfToken },
						{ "binCollectionGuid", binCollectionGuid },
						{ "binCollectionHash", binCollectionHash },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		else if (clientSideResponse.RequestId == 4)
		{
			var setCookieHeader = clientSideResponse.Headers.ContainsKey("set-cookie")
				? clientSideResponse.Headers["set-cookie"]
				: string.Empty;
			var parsedCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);
			var existingCookies = clientSideResponse.Options.Metadata["cookie"];
			var cookies = string.IsNullOrWhiteSpace(parsedCookies) ? existingCookies : $"{existingCookies}; {parsedCookies}";
			var cookiesWithOrigin = $"{cookies}; originURI=/login.html";
			var csrfToken = clientSideResponse.Options.Metadata["csrfToken"];
			var binCollectionGuid = clientSideResponse.Options.Metadata["binCollectionGuid"];
			var binCollectionHash = clientSideResponse.Options.Metadata["binCollectionHash"];

			var profileDataKey = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-1";
			var requestBody = $$"""
{
  "action":"runtimeOperation",
  "operationId":"VIPoQq+TS0aN/pBsFv3LUg",
  "params":{
    "OS_MissedBinEnquiry":{
      "guid":"{{binCollectionGuid}}"
    }
  },
  "validationGuids":[
    "{{binCollectionGuid}}",
    "{{_validationGuid}}"
  ],
  "changes":{
    "{{binCollectionGuid}}":{
      "BlueBinSuspensionMsg":{
        "value":""
      },
      "ShowBlueBinMessage":{
        "value":false
      },
      "BinCollectionFinancialYear":{
        "value":"{{_binCollectionFinancialYear}}"
      },
      "EnquiryPostcodeOrStreetName":{
        "value":"{{address.Postcode}}"
      }
    }
  },
  "objects":[
    {
      "attributes":{
        "NextGrey":{
          "value":null
        },
        "CalendarPDF":{
          "value":null
        },
        "ShowAddressResults":{
          "value":false
        },
        "UPRN":{
          "value":null
        },
        "AddressSelected":{
          "value":false
        },
        "NextMaroon":{
          "value":null
        },
        "ShowBlueBinMessage":{
          "value":false
        },
        "EnquiryPostcodeOrStreetName":{
          "value":null
        },
        "BinCollectionFinancialYear":{
          "value":null
        },
        "BlueBinSuspensionMsg":{
          "value":null
        },
        "WasteGuidePDF":{
          "value":null
        },
        "NextBlue":{
          "value":null
        },
        "EnquiryAddress":{
          "value":null
        }
      },
      "guid":"{{binCollectionGuid}}",
      "hash":"{{binCollectionHash}}",
      "objectType":"OnlineServices.OS_vmBinCollectionEnquiry"
    }
  ],
  "profiledata":{
    "{{profileDataKey}}":26
  }
}
""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 5,
				Url = $"{_baseUrl}/xas/",
				Method = "POST",
				Headers = CreateHeaders(cookiesWithOrigin, csrfToken, CreateRequestToken(4)),
				Body = requestBody,
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "cookie", cookiesWithOrigin },
						{ "csrfToken", csrfToken },
						{ "binCollectionGuid", binCollectionGuid },
						{ "binCollectionHash", binCollectionHash },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		else if (clientSideResponse.RequestId == 5)
		{
			var metadata = clientSideResponse.Options.Metadata;
			var cookies = metadata["cookie"];
			var csrfToken = metadata["csrfToken"];
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var changes = jsonDoc.RootElement.GetProperty("changes");
			var objects = jsonDoc.RootElement.GetProperty("objects");

			var parts = address.Uid!.Split(';', 8);
			var uprn = parts[1];

			var addressGuid = string.Empty;
			var associationHash = string.Empty;
			var binCollectionGuid = string.Empty;
			var enquiryX = string.Empty;
			var enquiryY = string.Empty;
			var siteCode = string.Empty;
			var street = string.Empty;
			var fullAddress = string.Empty;

			foreach (var change in changes.EnumerateObject())
			{
				if (!change.Value.TryGetProperty("UPRN", out var uprnProperty))
				{
					continue;
				}

				var currentUprn = uprnProperty.GetProperty("value").GetString()!;
				if (currentUprn != uprn)
				{
					continue;
				}

				addressGuid = change.Name;
				associationHash = change.Value.GetProperty("OnlineServices.OS_vmGeneric_Address_OS_vmBinCollectionEnquiry").GetProperty("hash").GetString()!;
				binCollectionGuid = change.Value.GetProperty("OnlineServices.OS_vmGeneric_Address_OS_vmBinCollectionEnquiry").GetProperty("value").GetString()!;
				enquiryX = change.Value.GetProperty("EnquiryX").GetProperty("value").GetString()!;
				enquiryY = change.Value.GetProperty("EnquiryY").GetProperty("value").GetString()!;
				siteCode = change.Value.GetProperty("SiteCode").GetProperty("value").GetString()!;
				street = change.Value.GetProperty("Street").GetProperty("value").GetString()!;
				fullAddress = change.Value.GetProperty("FullAddress").GetProperty("value").GetString()!;

				break;
			}

			if (string.IsNullOrWhiteSpace(binCollectionGuid))
			{
				foreach (var change in changes.EnumerateObject())
				{
					if (change.Value.TryGetProperty("ShowAddressResults", out _))
					{
						binCollectionGuid = change.Name;
						break;
					}
				}
			}

			var binCollectionHash = string.Empty;
			var addressHash = string.Empty;

			foreach (var obj in objects.EnumerateArray())
			{
				var objectGuid = obj.GetProperty("guid").GetString()!;
				var objectHash = obj.GetProperty("hash").GetString()!;
				var objectType = obj.GetProperty("objectType").GetString()!;

				if (objectType == "OnlineServices.OS_vmBinCollectionEnquiry" && objectGuid == binCollectionGuid)
				{
					binCollectionHash = objectHash;
				}
				else if (objectType == "OnlineServices.OS_vmGeneric_Address" && objectGuid == addressGuid)
				{
					addressHash = objectHash;
				}
			}

			var profileDataKey = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-9";
			var requestBody = $$"""
{
  "action":"runtimeOperation",
  "operationId":"rMmaEU30U028dd2l0SMYFQ",
  "params":{
    "Generic_Address":{
      "guid":"{{addressGuid}}"
    }
  },
  "validationGuids":[
    "{{binCollectionGuid}}",
    "{{_validationGuid}}"
  ],
  "changes":{
    "{{addressGuid}}":{
      "OnlineServices.OS_vmGeneric_Address_OS_vmBinCollectionEnquiry":{
        "hash":"{{associationHash}}",
        "value":"{{binCollectionGuid}}"
      },
      "EnquiryX":{
        "value":"{{enquiryX}}"
      },
      "SiteCode":{
        "value":"{{siteCode}}"
      },
      "UPRN":{
        "value":"{{uprn}}"
      },
      "EnquiryY":{
        "value":"{{enquiryY}}"
      },
      "FullAddress":{
        "value":"{{fullAddress}}"
      },
      "Street":{
        "value":"{{street}}"
      }
    },
    "{{binCollectionGuid}}":{
      "BlueBinSuspensionMsg":{
        "value":""
      },
      "ShowBlueBinMessage":{
        "value":false
      },
      "BinCollectionFinancialYear":{
        "value":"{{_binCollectionFinancialYear}}"
      },
      "EnquiryPostcodeOrStreetName":{
        "value":"{{address.Postcode}}"
      },
      "EnquiryAddress":{
        "value":"{{fullAddress}}"
      },
      "UPRN":{
        "value":"{{uprn}}"
      },
      "ShowAddressResults":{
        "value":true
      },
      "AddressSelected":{
        "value":true
      }
    }
  },
  "objects":[
    {
      "attributes":{
        "OnlineServices.OS_vmGeneric_Address_OS_vmBinCollectionEnquiry":{
          "value":null
        },
        "Town":{
          "value":null
        },
        "EnquiryX":{
          "value":null
        },
        "SiteCode":{
          "value":null
        },
        "UPRN":{
          "value":null
        },
        "HouseNameNo":{
          "value":null
        },
        "EnquiryY":{
          "value":null
        },
        "FullAddress":{
          "value":null
        },
        "System.owner":{
          "readonly":true,
          "value":"36028797831528694"
        },
        "Street":{
          "value":null
        },
        "Postcode":{
          "value":null
        }
      },
      "guid":"{{addressGuid}}",
      "hash":"{{addressHash}}",
      "objectType":"OnlineServices.OS_vmGeneric_Address"
    },
    {
      "attributes":{
        "NextGrey":{
          "value":null
        },
        "CalendarPDF":{
          "value":null
        },
        "ShowAddressResults":{
          "value":false
        },
        "UPRN":{
          "value":null
        },
        "AddressSelected":{
          "value":false
        },
        "NextMaroon":{
          "value":null
        },
        "ShowBlueBinMessage":{
          "value":false
        },
        "EnquiryPostcodeOrStreetName":{
          "value":null
        },
        "BinCollectionFinancialYear":{
          "value":null
        },
        "BlueBinSuspensionMsg":{
          "value":null
        },
        "WasteGuidePDF":{
          "value":null
        },
        "NextBlue":{
          "value":null
        },
        "EnquiryAddress":{
          "value":null
        }
      },
      "guid":"{{binCollectionGuid}}",
      "hash":"{{binCollectionHash}}",
      "objectType":"OnlineServices.OS_vmBinCollectionEnquiry"
    }
  ],
  "profiledata":{
    "{{profileDataKey}}":49
  }
}
""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 6,
				Url = $"{_baseUrl}/xas/",
				Method = "POST",
				Headers = CreateHeaders(cookies, csrfToken, CreateRequestToken(5)),
				Body = requestBody,
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		else if (clientSideResponse.RequestId == 6)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var changes = jsonDoc.RootElement.GetProperty("changes");

			// Iterate through each change set, and create bin day entries
			var binDays = new List<BinDay>();
			foreach (var change in changes.EnumerateObject())
			{
				if (!change.Value.TryGetProperty("NextMaroon", out var maroonProperty))
				{
					continue;
				}

				var maroonDate = maroonProperty.GetProperty("value").GetString()!.Trim();
				var greyDate = change.Value.GetProperty("NextGrey").GetProperty("value").GetString()!.Trim();
				var blueDate = change.Value.GetProperty("NextBlue").GetProperty("value").GetString()!.Trim();

				var dateMappings = new List<(string DateString, string ServiceKey)>
				{
					(maroonDate, "Maroon"),
					(greyDate, "Grey"),
					(blueDate, "Blue"),
				};

				// Iterate through each bin day, and create a new bin day object
				foreach (var (dateString, serviceKey) in dateMappings)
				{
					if (string.IsNullOrWhiteSpace(dateString))
					{
						continue;
					}

					var date = DateUtilities.ParseDateExact(dateString, "dddd d/MM/yyyy");
					var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, serviceKey);

					var binDay = new BinDay
					{
						Date = date,
						Address = address,
						Bins = matchedBins,
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
	/// Creates the standard headers used for Mendix client requests.
	/// </summary>
	/// <param name="cookies">The cookies to include with the request.</param>
	/// <param name="csrfToken">The CSRF token to include with the request.</param>
	/// <param name="requestToken">The request token for the Mendix request.</param>
	/// <returns>The request headers.</returns>
	private static Dictionary<string, string> CreateHeaders(string cookies, string? csrfToken, string requestToken)
	{
		var headers = new Dictionary<string, string>
		{
			{ "user-agent", Constants.UserAgent },
			{ "content-type", Constants.ApplicationJson },
			{ "accept", "application/json" },
			{ "origin", _baseUrl },
			{ "referer", $"{_baseUrl}/index.html" },
			{ "cookie", cookies },
		};

		if (!string.IsNullOrWhiteSpace(csrfToken))
		{
			headers.Add("x-csrf-token", csrfToken!);
		}

		headers.Add("x-mx-reqtoken", requestToken);

		return headers;
	}

	/// <summary>
	/// Creates the Mendix request token used for client requests.
	/// </summary>
	/// <param name="sequence">The sequence number for the request token.</param>
	/// <returns>A formatted request token string.</returns>
	private static string CreateRequestToken(int sequence)
	{
		return $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{sequence}";
	}
}
