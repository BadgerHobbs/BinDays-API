namespace BinDays.Api.Collectors.Collectors.Councils
{
    using BinDays.Api.Collectors.Models;
    using BinDays.Api.Collectors.Utilities;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Collector implementation for Vale of White Horse District Council.
    /// </summary>
    internal sealed partial class ValeOfWhiteHorseDistrictCouncil : GovUkCollectorBase, ICollector
    {
        /// <inheritdoc/>
        public string Name => "Vale of White Horse District Council";

        /// <inheritdoc/>
        public Uri WebsiteUrl => new("https://www.whitehorsedc.gov.uk/java/support/formcall.jsp?F=BINZONE_DESKTOP");

        /// <inheritdoc/>
        public override string GovUkId => "vale-of-white-horse";

        /// <summary>
        /// The list of bin types for this collector.
        /// </summary>
        private readonly ReadOnlyCollection<Bin> _binTypes = new List<Bin>()
        {
            new()
            {
                Name = "Rubbish",
                Colour = BinColour.Black,
                Keys = new List<string>() { "grey bin" }.AsReadOnly(),
            },
            new()
            {
                Name = "Recycling",
                Colour = BinColour.Green,
                Keys = new List<string>() { "green bin" }.AsReadOnly(),
            },
            new()
            {
                Name = "Food Waste",
                Colour = BinColour.Green,
                Keys = new List<string>() { "food bin" }.AsReadOnly(),
                Type = BinType.Caddy,
            },
            new()
            {
                Name = "Garden Waste",
                Colour = BinColour.Brown,
                Keys = new List<string>() { "garden waste bin" }.AsReadOnly(),
            },
            new()
            {
                Name = "Small Electrical Items",
                Colour = BinColour.Grey,
                Keys = new List<string>() { "small electrical items" }.AsReadOnly(),
                Type = BinType.Bag,
            },
            new()
            {
                Name = "Textiles",
                Colour = BinColour.Grey,
                Keys = new List<string>() { "textiles" }.AsReadOnly(),
                Type = BinType.Bag,
            },
        }.AsReadOnly();

        /// <summary>
        /// Regex for the ebz/ebs token value from a URL.
        /// </summary>
        [GeneratedRegex("ebz=([^&]+)")]
        private static partial Regex EbzRegex();

        /// <summary>
        /// Regex for extracting addresses and their corresponding control IDs from the HTML table.
        /// </summary>
        [GeneratedRegex(@"(?s)class=""[^""]*eb-58-fieldHyperlink[^""]*""[^>]*>\s*(?<address>[^<]+)\s*</a>.*?name=""(?<uid>CTRL:63:_:D:\d+)""")]
        private static partial Regex AddressRegex();

        /// <summary>
        /// Regex for extracting the HID:inputs value from the form.
        /// </summary>
        [GeneratedRegex(@"name=""HID:inputs"" value=""(?<value>[^""]+)""")]
        private static partial Regex HidInputsRegex();

        /// <summary>
        /// Regex for extracting date and bin information from the bin details slider.
        /// Uses [\s\S]+? to match across newlines and include HTML tags (like anchors) within the description.
        /// </summary>
        [GeneratedRegex(@"class=""binextra"">\s*(?<date>[^<]+?)\s*-<br>\s*(?<bins>[\s\S]+?)<br>")]
        private static partial Regex BinDetailsRegex();

        /// <summary>
        /// Regex for removing HTML tags from the bin description.
        /// </summary>
        [GeneratedRegex("<.*?>")]
        private static partial Regex HtmlTagRegex();

        /// <inheritdoc/>
        public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
        {
            // Prepare client-side request for getting the initial session redirect
            if (clientSideResponse == null)
            {
                var clientSideRequest = new ClientSideRequest()
                {
                    RequestId = 1,
                    Url = "https://eform.whitehorsedc.gov.uk/ebase/ufsmain?formid=BINZONE_DESKTOP&SOVA_TAG=VALE",
                    Method = "GET",
                    Options = new ClientSideOptions()
                    {
                        // We need to trap the 302 to get the Location header
                        FollowRedirects = false,
                    },
                };

                var getAddressesResponse = new GetAddressesResponse()
                {
                    NextClientSideRequest = clientSideRequest,
                };

                return getAddressesResponse;
            }
            // Prepare client-side request for initializing the session (GET)
            else if (clientSideResponse.RequestId == 1)
            {
                var cookie = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(
                    clientSideResponse.Headers["set-cookie"]
                );

                var relativeLocation = clientSideResponse.Headers["location"];
                var fullRedirectUrl = $"https://eform.whitehorsedc.gov.uk/ebase/{relativeLocation}";

                var clientSideRequest = new ClientSideRequest()
                {
                    RequestId = 2,
                    Url = fullRedirectUrl,
                    Method = "GET",
                    Headers = new Dictionary<string, string>
                    {
                        { "cookie", cookie },
                    },
                    Options = new ClientSideOptions
                    {
                        Metadata =
                        {
                            { "cookie", cookie },
                            { "referer", fullRedirectUrl },
                        }
                    }
                };

                var getAddressesResponse = new GetAddressesResponse()
                {
                    NextClientSideRequest = clientSideRequest,
                };

                return getAddressesResponse;
            }
            // Prepare client-side request for performing the search (POST)
            else if (clientSideResponse.RequestId == 2)
            {
                var cookie = clientSideResponse.Options.Metadata["cookie"];
                var refererUrl = clientSideResponse.Options.Metadata["referer"];
                var ebs = EbzRegex().Match(refererUrl).Groups[1].Value;

                var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new Dictionary<string, string>()
                {
                    { "formid", "/Forms/BINZONE_DESKTOP" },
                    { "ebs", ebs },
                    { "formstack", "BINZONE_DESKTOP:f267e852-5fff-456e-96d7-83cd429c5109" },
                    { "pageSeq", "1" },
                    { "pageId", "WHERE_DO_YOU_LIVE" },
                    { "formStateId", "1" },
                    { "CTRL:2:_:A", postcode },
                    { "CTRL:20:_", "Search" },
                    { "HID:inputs", "ICTRL:2:_:A,ACTRL:20:_,ACTRL:24:_,ICTRL:70:_:A,ICTRL:31:_:A,ICTRL:32:_:A,APAGE:E.h,APAGE:B.h,APAGE:N.h,APAGE:S.h,APAGE:R.h" },
                });

                var clientSideRequest = new ClientSideRequest()
                {
                    RequestId = 3,
                    Url = $"https://eform.whitehorsedc.gov.uk/ebase/BINZONE_DESKTOP.eb?ebz={ebs}",
                    Method = "POST",
                    Body = requestBody,
                    Headers = new Dictionary<string, string>
                    {
                        { "cookie", cookie },
                        { "Content-Type", "application/x-www-form-urlencoded" },
                    },
                };

                var getAddressesResponse = new GetAddressesResponse()
                {
                    NextClientSideRequest = clientSideRequest,
                };

                return getAddressesResponse;
            }
            // Process addresses from response
            else if (clientSideResponse.RequestId == 3)
            {
                var addresses = new List<Address>();
                var matches = AddressRegex().Matches(clientSideResponse.Content);

                foreach (Match match in matches)
                {
                    var uid = match.Groups["uid"].Value;
                    var addressText = match.Groups["address"].Value.Trim();

                    addresses.Add(new Address()
                    {
                        Property = addressText,
                        Postcode = postcode,
                        Uid = uid,
                    });
                }

                var getAddressesResponse = new GetAddressesResponse()
                {
                    Addresses = addresses.AsReadOnly(),
                };

                return getAddressesResponse;
            }

            // Throw exception for invalid request
            throw new InvalidOperationException("Invalid client-side request.");
        }

        /// <inheritdoc/>
        public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
        {
            // Prepare client-side request for getting the initial session redirect
            if (clientSideResponse == null)
            {
                var clientSideRequest = new ClientSideRequest()
                {
                    RequestId = 1,
                    Url = "https://eform.whitehorsedc.gov.uk/ebase/ufsmain?formid=BINZONE_DESKTOP&SOVA_TAG=VALE",
                    Method = "GET",
                    Options = new ClientSideOptions()
                    {
                        FollowRedirects = false,
                    },
                };

                var getBinDaysResponse = new GetBinDaysResponse()
                {
                    NextClientSideRequest = clientSideRequest,
                };

                return getBinDaysResponse;
            }
            // Prepare client-side request for initializing the session (GET)
            else if (clientSideResponse.RequestId == 1)
            {
                var cookie = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(
                    clientSideResponse.Headers["set-cookie"]
                );

                var relativeLocation = clientSideResponse.Headers["location"];
                var fullRedirectUrl = $"https://eform.whitehorsedc.gov.uk/ebase/{relativeLocation}";

                var clientSideRequest = new ClientSideRequest()
                {
                    RequestId = 2,
                    Url = fullRedirectUrl,
                    Method = "GET",
                    Headers = new Dictionary<string, string>
                    {
                        { "cookie", cookie },
                    },
                    Options = new ClientSideOptions
                    {
                        Metadata =
                        {
                            { "cookie", cookie },
                            { "referer", fullRedirectUrl },
                        }
                    }
                };

                var getBinDaysResponse = new GetBinDaysResponse()
                {
                    NextClientSideRequest = clientSideRequest,
                };

                return getBinDaysResponse;
            }
            // Prepare client-side request for performing the search (POST)
            else if (clientSideResponse.RequestId == 2)
            {
                var cookie = clientSideResponse.Options.Metadata["cookie"];
                var refererUrl = clientSideResponse.Options.Metadata["referer"];
                var ebs = EbzRegex().Match(refererUrl).Groups[1].Value;

                // Save EBS for next step
                clientSideResponse.Options.Metadata.Add("ebs", ebs);

                var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new Dictionary<string, string>()
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

                var clientSideRequest = new ClientSideRequest()
                {
                    RequestId = 3,
                    Url = $"https://eform.whitehorsedc.gov.uk/ebase/BINZONE_DESKTOP.eb?ebz={ebs}",
                    Method = "POST",
                    Body = requestBody,
                    Headers = new Dictionary<string, string>
                    {
                        { "cookie", cookie },
                        { "Content-Type", "application/x-www-form-urlencoded" },
                    },
                    Options = clientSideResponse.Options,
                };

                var getBinDaysResponse = new GetBinDaysResponse()
                {
                    NextClientSideRequest = clientSideRequest,
                };

                return getBinDaysResponse;
            }
            // Prepare client-side request to select the address (POST)
            else if (clientSideResponse.RequestId == 3)
            {
                var cookie = clientSideResponse.Options.Metadata["cookie"];
                var ebs = clientSideResponse.Options.Metadata["ebs"];

                // Scrape the dynamic HID:inputs value from the response
                var hidInputs = HidInputsRegex().Match(clientSideResponse.Content).Groups["value"].Value;

                // Construct the payload to click the specific address button
                // The address.Uid contains the control name (e.g., "CTRL:63:_:D:17")
                var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new Dictionary<string, string>()
                {
                    { "formid", "/Forms/BINZONE_DESKTOP" },
                    { "ebs", ebs },
                    { "formstack", "BINZONE_DESKTOP:f267e852-5fff-456e-96d7-83cd429c5109" },
                    { "pageSeq", "2" },
                    { "pageId", "_ADDRESS" },
                    { "formStateId", "1" },
                    { $"{address.Uid}.x", "10" },
                    { $"{address.Uid}.y", "10" },
                    { "HID:inputs", hidInputs },
                });

                var clientSideRequest = new ClientSideRequest()
                {
                    RequestId = 4,
                    Url = $"https://eform.whitehorsedc.gov.uk/ebase/BINZONE_DESKTOP.eb?ebz={ebs}",
                    Method = "POST",
                    Body = requestBody,
                    Headers = new Dictionary<string, string>
                    {
                        { "cookie", cookie },
                        { "Content-Type", "application/x-www-form-urlencoded" },
                    },
                };

                var getBinDaysResponse = new GetBinDaysResponse()
                {
                    NextClientSideRequest = clientSideRequest,
                };

                return getBinDaysResponse;
            }
            // Process bin days from response
            else if (clientSideResponse.RequestId == 4)
            {
                var matches = BinDetailsRegex().Matches(clientSideResponse.Content);
                var binDays = new List<BinDay>();

                foreach (Match match in matches)
                {
                    var dateString = match.Groups["date"].Value.Trim();
                    var binString = match.Groups["bins"].Value;

                    // Remove HTML tags (e.g. hyperlinks for garden waste)
                    binString = HtmlTagRegex().Replace(binString, "");

                    // Parse the date (e.g. "Thursday 27 November")
                    var date = DateOnly.ParseExact(
                        dateString,
                        "dddd d MMMM",
                        CultureInfo.InvariantCulture
                    );

                    // If the parsed date is in a month that has already passed this year, assume it's for next year
                    if (date.Month < DateTime.Now.Month)
                    {
                        date = date.AddYears(1);
                    }

                    // Split bin description by comma or "and" to get individual bins
                    var bins = binString.Split([",", " and "], StringSplitOptions.RemoveEmptyEntries);

                    foreach (var binName in bins)
                    {
                        var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, binName);

                        var binDay = new BinDay()
                        {
                            Date = date,
                            Address = address,
                            Bins = matchedBins,
                        };
                        binDays.Add(binDay);
                    }
                }

                var getBinDaysResponse = new GetBinDaysResponse()
                {
                    BinDays = ProcessingUtilities.ProcessBinDays(binDays),
                };

                return getBinDaysResponse;
            }

            // Throw exception for invalid request
            throw new InvalidOperationException("Invalid client-side request.");
        }
    }
}