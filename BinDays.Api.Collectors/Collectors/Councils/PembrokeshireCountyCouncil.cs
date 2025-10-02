namespace BinDays.Api.Collectors.Collectors.Councils
{
    using BinDays.Api.Collectors.Models;
    using BinDays.Api.Collectors.Utilities;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Collector implementation for Pembrokeshire County Council.
    /// </summary>
    internal sealed partial class PembrokeshireCountyCouncil : GovUkCollectorBase, ICollector
    {
        /// <inheritdoc/>
        public string Name => "Pembrokeshire County Council";

        /// <inheritdoc/>
        public Uri WebsiteUrl => new("https://www.pembrokeshire.gov.uk/waste-and-recycling");

        /// <inheritdoc/>
        public override string GovUkId => "pembrokeshire";

        /// <summary>
        /// The list of bin types for this collector.
        /// </summary>
        private readonly ReadOnlyCollection<Bin> binTypes = new List<Bin>()
        {
            new()
            {
                Name = "Food Waste",
                Colour = "Green",
                Type = "Caddy",
                Keys = new List<string>() { "Green Food Waste Caddy" }.AsReadOnly()
            },
            new()
            {
                Name = "Paper",
                Colour = "Blue",
                Type = "Box",
                Keys = new List<string>() { "Blue Box" }.AsReadOnly()
            },
            new()
            {
                Name = "Glass",
                Colour = "Green",
                Type = "Box",
                Keys = new List<string>() { "Green Box" }.AsReadOnly()
            },
            new()
            {
                Name = "Card and Cardboard",
                Colour = "Blue",
                Type = "Bag",
                Keys = new List<string>() { "Blue Bag" }.AsReadOnly()
            },
            new()
            {
                Name = "Metal Packaging, Plastic packaging and cartons",
                Colour = "Red",
                Type = "Bag",
                Keys = new List<string>() { "Red Bag" }.AsReadOnly()
            },
            new()
            {
                Name = "Residual Waste",
                Colour = "Black",
                Type = "Bag",
                Keys = new List<string>() { "Black/Grey Bag" }.AsReadOnly()
            },
        }.AsReadOnly();

        /// <summary>
        /// Regex for the addresses from the options elements.
        /// </summary>
        [GeneratedRegex(@"<option value=""(?<uid>\d+)"">(?<address>.*?)<\/option>")]
        private static partial Regex AddressesRegex();

        /// <summary>
        /// Regex for the bin days from the page elements.
        /// </summary>
        [GeneratedRegex(@"(?s)<div class=""col-6 col-md-4 text-center mb-3"">\s*<img[^>]*>\s*<br />\s*(?<binType>.+?)\s*<br />\s*<strong>(?<date>\d{2}\/\d{2}\/\d{4})<\/strong>")]
        private static partial Regex BinDaysRegex();

        /// <inheritdoc/>
        public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
        {
            // Prepare client-side request for getting addresses
            if (clientSideResponse == null)
            {
                var clientSideRequest = new ClientSideRequest()
                {
                    RequestId = 1,
                    Url = $"https://nearest.pembrokeshire.gov.uk/search/?query={postcode}",
                    Method = "GET",
                    Headers = new Dictionary<string, string>() {
                        {"user-agent", Constants.UserAgent},
                    },
                    Body = string.Empty,
                };

                var getAddressesResponse = new GetAddressesResponse()
                {
                    Addresses = null,
                    NextClientSideRequest = clientSideRequest
                };

                return getAddressesResponse;
            }
            // Process addresses from response
            else if (clientSideResponse.RequestId == 1)
            {
                var rawAddresses = AddressesRegex().Matches(clientSideResponse.Content);
                var addresses = new List<Address>();

                foreach (Match rawAddress in rawAddresses)
                {
                    addresses.Add(new Address()
                    {
                        Property = rawAddress.Groups["address"].Value.Trim(),
                        Street = string.Empty,
                        Town = string.Empty,
                        Postcode = postcode,
                        Uid = rawAddress.Groups["uid"].Value,
                    });
                }

                var getAddressesResponse = new GetAddressesResponse()
                {
                    Addresses = addresses.AsReadOnly(),
                    NextClientSideRequest = null
                };

                return getAddressesResponse;
            }

            throw new InvalidOperationException("Invalid client-side request.");
        }

        /// <inheritdoc/>
        public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
        {
            // Prepare client-side request for getting bin days
            if (clientSideResponse == null)
            {
                var clientSideRequest = new ClientSideRequest()
                {
                    RequestId = 1,
                    Url = $"https://nearest.pembrokeshire.gov.uk/property/{address.Uid}",
                    Method = "GET",
                    Headers = new Dictionary<string, string>() {
                        {"user-agent", Constants.UserAgent},
                    },
                    Body = string.Empty,
                };

                var getBinDaysResponse = new GetBinDaysResponse()
                {
                    BinDays = null,
                    NextClientSideRequest = clientSideRequest
                };

                return getBinDaysResponse;
            }
            // Process bin days from response
            else if (clientSideResponse.RequestId == 1)
            {
                var rawBinDays = BinDaysRegex().Matches(clientSideResponse.Content);
                var binDays = new List<BinDay>();

                foreach (Match rawBinDay in rawBinDays)
                {
                    var binType = rawBinDay.Groups["binType"].Value;
                    var date = rawBinDay.Groups["date"].Value;

                    var collectionDate = DateOnly.ParseExact(
                        date,
                        "dd/MM/yyyy",
                        CultureInfo.InvariantCulture
                    );

                    var matchedBinTypes = binTypes.Where(b => b.Keys.Any(k => binType.Contains(k, StringComparison.InvariantCultureIgnoreCase)));

                    binDays.Add(new BinDay()
                    {
                        Date = collectionDate,
                        Address = address,
                        Bins = matchedBinTypes.ToList().AsReadOnly()
                    });
                }

                var getBinDaysResponse = new GetBinDaysResponse()
                {
                    BinDays = ProcessingUtilities.ProcessBinDays(binDays),
                    NextClientSideRequest = null
                };

                return getBinDaysResponse;
            }

            throw new InvalidOperationException("Invalid client-side request.");
        }
    }
}