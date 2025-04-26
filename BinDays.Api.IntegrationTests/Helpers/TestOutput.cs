namespace BinDays.Api.IntegrationTests.Helpers
{
    using System.Text;
    using BinDays.Api.Collectors.Collectors;
    using BinDays.Api.Collectors.Models;
    using Xunit.Abstractions;

    /// <summary>
    /// Provides helper methods for formatting and writing test output summaries using ITestOutputHelper.
    /// </summary>
    internal static class TestOutput
    {
        private const string PrimaryIndent = "- ";
        private const string SecondaryIndent = "  - ";
        private const int BorderWidth = 54;
        private const int MaxAddressesToShow = 5;
        private const int MaxBinDaysToShow = 10;
        private static readonly string TopBottomBorder = new('=', BorderWidth);

        /// <summary>
        /// Writes a formatted summary of the test results to the xUnit test output.
        /// </summary>
        /// <param name="outputHelper">The ITestOutputHelper for the current test execution.</param>
        /// <param name="collector">The collector instance used in the test.</param>
        /// <param name="addresses">The list of addresses retrieved.</param>
        /// <param name="binDays">The list of bin days retrieved.</param>
        public static void WriteTestSummary(
            ITestOutputHelper outputHelper,
            ICollector collector,
            IReadOnlyCollection<Address> addresses,
            IReadOnlyCollection<BinDay> binDays)
        {
            var summaryBuilder = new StringBuilder();

            AppendSummaryHeader(summaryBuilder);
            AppendCollectorDetails(summaryBuilder, collector);
            AppendAddressDetails(summaryBuilder, addresses);
            AppendBinDayDetails(summaryBuilder, binDays);
            AppendSummaryFooter(summaryBuilder);

            outputHelper.WriteLine(summaryBuilder.ToString());
        }

        /// <summary>
        /// Appends the main summary header to the StringBuilder.
        /// </summary>
        /// <param name="summaryBuilder">The StringBuilder to append to.</param>
        private static void AppendSummaryHeader(StringBuilder summaryBuilder)
        {
            summaryBuilder.AppendLine();
            const string summaryTitle = " Test Summary ";
            summaryBuilder.AppendLine(CreateCenteredHeader(summaryTitle, BorderWidth, '='));
            summaryBuilder.AppendLine();
        }

        /// <summary>
        /// Appends details about the collector to the StringBuilder.
        /// </summary>
        /// <param name="summaryBuilder">The StringBuilder to append to.</param>
        /// <param name="collector">The collector instance.</param>
        private static void AppendCollectorDetails(StringBuilder summaryBuilder, ICollector collector)
        {
            const string collectorHeaderText = " Collector ";
            summaryBuilder.AppendLine(CreateCenteredHeader(collectorHeaderText, BorderWidth, '-'));
            summaryBuilder.AppendLine();
            summaryBuilder.AppendLine($"{collector?.Name ?? "Unknown"}");
            summaryBuilder.AppendLine();
        }

        /// <summary>
        /// Appends details about the addresses found to the StringBuilder, showing a limited number.
        /// </summary>
        /// <param name="summaryBuilder">The StringBuilder to append to.</param>
        /// <param name="addresses">The collection of addresses.</param>
        private static void AppendAddressDetails(StringBuilder summaryBuilder, IReadOnlyCollection<Address> addresses)
        {
            var addressCount = addresses?.Count ?? 0;
            string addressHeaderText = $" Addresses ({addressCount}) ";
            summaryBuilder.AppendLine(CreateCenteredHeader(addressHeaderText, BorderWidth, '-'));
            summaryBuilder.AppendLine();

            if (addresses == null || addressCount == 0)
            {
                summaryBuilder.AppendLine($"{PrimaryIndent}No addresses found.");
            }
            else
            {
                foreach (var address in addresses.Take(MaxAddressesToShow))
                {
                    string addressLine = string.Join(", ",
                        new[] { address.Property, address.Street, address.Town, address.Postcode, address.Uid }
                        .Where(part => !string.IsNullOrWhiteSpace(part)));

                    summaryBuilder.AppendLine($"{PrimaryIndent}{addressLine}");
                }

                if (addressCount > MaxAddressesToShow)
                {
                    summaryBuilder.AppendLine($"{PrimaryIndent}...");
                }
            }

            summaryBuilder.AppendLine();
        }

        /// <summary>
        /// Appends details about the bin days found to the StringBuilder, showing a limited number and their associated bins.
        /// </summary>
        /// <param name="summaryBuilder">The StringBuilder to append to.</param>
        /// <param name="binDays">The collection of bin days.</param>
        private static void AppendBinDayDetails(StringBuilder summaryBuilder, IReadOnlyCollection<BinDay> binDays)
        {
            var binDaysCount = binDays?.Count ?? 0;
            string binDaysHeaderText = $" Bin Days ({binDaysCount}) ";
            summaryBuilder.AppendLine(CreateCenteredHeader(binDaysHeaderText, BorderWidth, '-'));
            summaryBuilder.AppendLine();

            if (binDays == null || binDaysCount == 0)
            {
                summaryBuilder.AppendLine($"{PrimaryIndent}No bin days found.");
            }
            else
            {
                var orderedBinDays = binDays.OrderBy(bd => bd.Date).ToList();
                AppendLimitedBinDays(summaryBuilder, orderedBinDays);
                AppendMoreBinDaysIndicator(summaryBuilder, binDaysCount);
            }
        }

        /// <summary>
        /// Appends a limited number of bin day entries with their bins to the StringBuilder.
        /// </summary>
        /// <param name="summaryBuilder">The StringBuilder to append to.</param>
        /// <param name="orderedBinDays">The list of bin days, ordered by date.</param>
        private static void AppendLimitedBinDays(StringBuilder summaryBuilder, List<BinDay> orderedBinDays)
        {
            foreach (var binDay in orderedBinDays.Take(MaxBinDaysToShow))
            {
                int binsOnThisDay = binDay.Bins?.Count ?? 0;
                summaryBuilder.AppendLine($"{PrimaryIndent}{binDay.Date:dd/MM/yyyy} ({binsOnThisDay} bins):");

                if (binDay.Bins != null && binsOnThisDay > 0)
                {
                    foreach (var bin in binDay.Bins)
                    {
                        summaryBuilder.AppendLine($"{SecondaryIndent}{bin.Name ?? "Unnamed Bin"}");
                    }
                }
                else
                {
                     summaryBuilder.AppendLine($"{SecondaryIndent}No bins listed for this day.");
                }

                summaryBuilder.AppendLine();
            }
        }

        /// <summary>
        /// Appends an indicator to the StringBuilder if more bin days were found than the display limit.
        /// </summary>
        /// <param name="summaryBuilder">The StringBuilder to append to.</param>
        /// <param name="binDaysCount">The total count of bin days found.</param>
        private static void AppendMoreBinDaysIndicator(StringBuilder summaryBuilder, int binDaysCount)
        {
            if (binDaysCount > MaxBinDaysToShow)
            {
                // Remove the last blank line added in the loop if we are adding the 'more' line
                if (summaryBuilder.Length >= Environment.NewLine.Length * 2 &&
                    summaryBuilder.ToString(summaryBuilder.Length - Environment.NewLine.Length * 2, Environment.NewLine.Length * 2) == Environment.NewLine + Environment.NewLine)
                {
                     summaryBuilder.Length -= Environment.NewLine.Length;
                }
                summaryBuilder.AppendLine($"{PrimaryIndent}...");
                summaryBuilder.AppendLine();
            }
        }

        /// <summary>
        /// Appends the main summary footer to the StringBuilder.
        /// </summary>
        /// <param name="summaryBuilder">The StringBuilder to append to.</param>
        private static void AppendSummaryFooter(StringBuilder summaryBuilder)
        {
            summaryBuilder.AppendLine(TopBottomBorder);
        }

        /// <summary>
        /// Creates a string with text centered between padding characters.
        /// </summary>
        /// <param name="text">The text to center.</param>
        /// <param name="totalWidth">The total desired width of the resulting string.</param>
        /// <param name="padChar">The character used for padding on either side of the text.</param>
        /// <returns>A string with the text centered using the specified padding character.</returns>
        private static string CreateCenteredHeader(string text, int totalWidth, char padChar)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new string(padChar, totalWidth);
            }

            if (text.Length >= totalWidth)
            {
                return text;
            }

            int paddingNeeded = totalWidth - text.Length;
            int leftPadding = paddingNeeded / 2;
            int rightPadding = paddingNeeded - leftPadding;

            var headerBuilder = new StringBuilder(totalWidth);
            headerBuilder.Append(padChar, leftPadding);
            headerBuilder.Append(text);
            headerBuilder.Append(padChar, rightPadding);
            return headerBuilder.ToString();
        }
    }
}
