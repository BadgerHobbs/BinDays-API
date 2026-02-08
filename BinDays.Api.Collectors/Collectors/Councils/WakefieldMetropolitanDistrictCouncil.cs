namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Wakefield Metropolitan District Council.
/// </summary>
internal sealed partial class WakefieldMetropolitanDistrictCouncil : GovUkCollectorBase, ICollector
{
    /// <inheritdoc/>
    public string Name => "Wakefield Metropolitan District Council";

    /// <inheritdoc/>
    public Uri WebsiteUrl => new("https://www.wakefield.gov.uk/");

    /// <inheritdoc/>
    public override string GovUkId => "wakefield";

    /// <summary>
    /// The list of bin types for this collector.
    /// </summary>
    private readonly IReadOnlyCollection<Bin> _binTypes =
    [
        new()
        {
            Name = "Household waste",
            Colour = BinColour.Green,
            Keys = [ "Household waste" ],
        },
        new()
        {
            Name = "Mixed recycling",
            Colour = BinColour.Brown,
            Keys = [ "Mixed recycling" ],
        },
        new()
        {
            Name = "Garden waste recycling",
            Colour = BinColour.Brown,
            Keys = [ "Garden waste recycling" ],
        },
    ];

    /// <summary>
    /// Regex for extracting addresses from the pick-your-address page.
    /// </summary>
    [GeneratedRegex(@"<a[^>]*href=""https://www\.wakefield\.gov\.uk/where-i-live\?uprn=(?<uid>[^&""]+)&amp;a=(?<addressParam>[^&""]+)[^""]*""[^>]*>(?<address>[^<]+)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex AddressRegex();

    /// <summary>
    /// Regex for extracting bin sections from the bin days page.
    /// </summary>
    [GeneratedRegex(@"<div class=""u-mb-4""><strong>(?<service>[^<]+)</strong></div>.*?<div class=""u-mb-2"">Last collection - (?<last>[^<]+)</div>\s*<div class=""u-mb-2"">Next collection - (?<next>[^<]+)</div>.*?<div class=""colldates"">(?<future>.*?)</div>", RegexOptions.Singleline)]
    private static partial Regex BinSectionRegex();

    /// <summary>
    /// Regex for extracting future collection dates.
    /// </summary>
    [GeneratedRegex(@"<li>\s*(?<date>[^<]+)\s*</li>", RegexOptions.Singleline)]
    private static partial Regex FutureDateRegex();

    /// <inheritdoc/>
    public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
    {
        // Prepare client-side request for getting addresses
        if (clientSideResponse == null)
        {
            var clientSideRequest = new ClientSideRequest
            {
                RequestId = 1,
                Url = $"https://www.wakefield.gov.uk/pick-your-address?where-i-live={postcode}",
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
        else if (clientSideResponse.RequestId == 1)
        {
            var rawAddresses = AddressRegex().Matches(clientSideResponse.Content)!;

            // Iterate through each address, and create a new address object
            var addresses = new List<Address>();
            foreach (Match rawAddress in rawAddresses)
            {
                var uid = rawAddress.Groups["uid"].Value.Trim();
                var addressParam = rawAddress.Groups["addressParam"].Value.Trim();
                var addressText = rawAddress.Groups["address"].Value.Trim();

                if (string.IsNullOrWhiteSpace(uid))
                {
                    continue;
                }

                var property = string.IsNullOrWhiteSpace(addressText) ? addressParam : addressText;

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

        throw new InvalidOperationException("Invalid client-side request.");
    }

    /// <inheritdoc/>
    public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
    {
        var postcode = address.Postcode!;
        var uid = address.Uid!;

        // Prepare client-side request for getting bin days
        if (clientSideResponse == null)
        {
            var clientSideRequest = new ClientSideRequest
            {
                RequestId = 1,
                Url = $"https://www.wakefield.gov.uk/pick-your-address?where-i-live={postcode}",
                Method = "GET",
                Headers = new()
                {
                    { "user-agent", Constants.UserAgent },
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
            var rawAddresses = AddressRegex().Matches(clientSideResponse.Content)!;

            string? addressParam = null;

            // Iterate through each address, and find the matching address parameter
            foreach (Match rawAddress in rawAddresses)
            {
                if (rawAddress.Groups["uid"].Value.Trim() != uid)
                {
                    continue;
                }

                addressParam = rawAddress.Groups["addressParam"].Value.Trim();
                break;
            }

            if (string.IsNullOrWhiteSpace(addressParam))
            {
                throw new InvalidOperationException("Unable to find address parameter for bin days request.");
            }

            var clientSideRequest = new ClientSideRequest
            {
                RequestId = 2,
                Url = $"https://www.wakefield.gov.uk/where-i-live?uprn={uid}&a={addressParam}&p={postcode}",
                Method = "GET",
                Headers = new()
                {
                    { "user-agent", Constants.UserAgent },
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
            var binSections = BinSectionRegex().Matches(clientSideResponse.Content)!;

            // Iterate through each bin collection section, and create bin day entries
            var binDays = new List<BinDay>();
            foreach (Match binSection in binSections)
            {
                var service = binSection.Groups["service"].Value.Trim();
                var lastCollection = binSection.Groups["last"].Value.Trim();
                var nextCollection = binSection.Groups["next"].Value.Trim();
                var futureContent = binSection.Groups["future"].Value;

                var seenDates = new HashSet<DateOnly>();
                var dates = new List<DateOnly>();

                void AddDate(string dateText)
                {
                    if (string.Equals(dateText, "n/a", StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    var parsedDate = DateOnly.ParseExact(
                        dateText,
                        "dddd, d MMMM yyyy",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None
                    );

                    if (seenDates.Add(parsedDate))
                    {
                        dates.Add(parsedDate);
                    }
                }

                AddDate(lastCollection);
                AddDate(nextCollection);

                var futureDates = FutureDateRegex().Matches(futureContent)!;

                // Iterate through each future collection date, and add it to the list
                foreach (Match futureDate in futureDates)
                {
                    AddDate(futureDate.Groups["date"].Value.Trim());
                }

                var bins = ProcessingUtilities.GetMatchingBins(_binTypes, service);

                foreach (var date in dates)
                {
                    var binDay = new BinDay
                    {
                        Date = date,
                        Address = address,
                        Bins = bins,
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
