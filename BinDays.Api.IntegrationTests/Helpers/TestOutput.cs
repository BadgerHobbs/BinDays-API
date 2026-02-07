namespace BinDays.Api.IntegrationTests.Helpers;

using BinDays.Api.Collectors.Models;
using System.Text;
using Xunit.Abstractions;

/// <summary>
/// Provides helper methods for formatting and writing test output summaries using ITestOutputHelper.
/// </summary>
internal static class TestOutput
{
	private const string _primaryIndent = "- ";
	private const string _secondaryIndent = "  - ";
	private const int _borderWidth = 54;
	private const int _maxAddressesToShow = 5;
	private const int _maxBinDaysToShow = 10;
	private static readonly string _topBottomBorder = new('=', _borderWidth);

	/// <summary>
	/// Writes a formatted summary of the test results to the xUnit test output.
	/// </summary>
	/// <param name="outputHelper">The ITestOutputHelper for the current test execution.</param>
	/// <param name="collector">The collector instance used in the test.</param>
	/// <param name="addresses">The list of addresses retrieved.</param>
	/// <param name="binDays">The list of bin days retrieved.</param>
	public static void WriteTestSummary(
		ITestOutputHelper outputHelper,
		TestCollector collector,
		IReadOnlyCollection<Address> addresses,
		IReadOnlyCollection<BinDay> binDays)
	{
		var summaryBuilder = new StringBuilder();

		AppendSummaryHeader(summaryBuilder);
		AppendCollectorDetails(summaryBuilder, collector);
		AppendAddressDetails(summaryBuilder, addresses);
		AppendBinTypesDetails(summaryBuilder, binDays);
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
		summaryBuilder.AppendLine(CreateCenteredHeader(summaryTitle, _borderWidth, '='));
		summaryBuilder.AppendLine();
	}

	/// <summary>
	/// Appends details about the collector to the StringBuilder.
	/// </summary>
	/// <param name="summaryBuilder">The StringBuilder to append to.</param>
	/// <param name="collector">The collector instance.</param>
	private static void AppendCollectorDetails(StringBuilder summaryBuilder, TestCollector collector)
	{
		const string collectorHeaderText = " Collector ";
		summaryBuilder.AppendLine(CreateCenteredHeader(collectorHeaderText, _borderWidth, '-'));
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
		var addressHeaderText = $" Addresses ({addressCount}) ";
		summaryBuilder.AppendLine(CreateCenteredHeader(addressHeaderText, _borderWidth, '-'));
		summaryBuilder.AppendLine();

		if (addresses == null || addressCount == 0)
		{
			summaryBuilder.AppendLine($"{_primaryIndent}No addresses found.");
		}
		else
		{
			foreach (var address in addresses.Take(_maxAddressesToShow))
			{
				var addressLine = string.Join(", ",
					new[] { address.Property, address.Street, address.Town, address.Postcode, address.Uid }
					.Where(part => !string.IsNullOrWhiteSpace(part)));

				summaryBuilder.AppendLine($"{_primaryIndent}{addressLine}");
			}

			if (addressCount > _maxAddressesToShow)
			{
				summaryBuilder.AppendLine($"{_primaryIndent}...");
			}
		}

		summaryBuilder.AppendLine();
	}

	/// <summary>
	/// Appends a summary of all unique bin types found across all bin days.
	/// </summary>
	/// <param name="summaryBuilder">The StringBuilder to append to.</param>
	/// <param name="binDays">The collection of bin days.</param>
	private static void AppendBinTypesDetails(StringBuilder summaryBuilder, IReadOnlyCollection<BinDay> binDays)
	{
		const string binTypesHeaderText = " Bin Types ";
		summaryBuilder.AppendLine(CreateCenteredHeader(binTypesHeaderText, _borderWidth, '-'));
		summaryBuilder.AppendLine();

		if (binDays == null || binDays.Count == 0)
		{
			summaryBuilder.AppendLine($"{_primaryIndent}No bins found.");
		}
		else
		{
			var uniqueBins = binDays
				.Where(bd => bd.Bins != null)
				.SelectMany(bd => bd.Bins)
				.GroupBy(b => new { b.Name, b.Colour, b.Type })
				.Select(g => g.First())
				.OrderBy(b => b.Name)
				.ToList();

			if (uniqueBins.Count == 0)
			{
				summaryBuilder.AppendLine($"{_primaryIndent}No bins found.");
			}
			else
			{
				foreach (var bin in uniqueBins)
				{
					var binDetails = FormatBinDetails(bin);
					summaryBuilder.AppendLine($"{_primaryIndent}{binDetails}");
				}
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
		var binDaysHeaderText = $" Bin Days ({binDaysCount}) ";
		summaryBuilder.AppendLine(CreateCenteredHeader(binDaysHeaderText, _borderWidth, '-'));
		summaryBuilder.AppendLine();

		if (binDays == null || binDaysCount == 0)
		{
			summaryBuilder.AppendLine($"{_primaryIndent}No bin days found.");
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
		foreach (var binDay in orderedBinDays.Take(_maxBinDaysToShow))
		{
			var binsOnThisDay = binDay.Bins?.Count ?? 0;
			summaryBuilder.AppendLine($"{_primaryIndent}{binDay.Date:dd/MM/yyyy} ({binsOnThisDay} bins):");

			if (binDay.Bins != null && binsOnThisDay > 0)
			{
				foreach (var bin in binDay.Bins)
				{
					var binDetails = FormatBinDetails(bin);
					summaryBuilder.AppendLine($"{_secondaryIndent}{binDetails}");
				}
			}
			else
			{
				summaryBuilder.AppendLine($"{_secondaryIndent}No bins listed for this day.");
			}

			summaryBuilder.AppendLine();
		}
	}

	/// <summary>
	/// Formats bin details including name, colour, and type.
	/// </summary>
	/// <param name="bin">The bin to format.</param>
	/// <returns>A formatted string with bin details.</returns>
	private static string FormatBinDetails(Bin bin)
	{
		var name = bin.Name ?? "Unnamed Bin";
		var colour = FormatEnumWithSpaces(bin.Colour.ToString());
		var type = bin.Type.HasValue ? FormatEnumWithSpaces(bin.Type.Value.ToString()) : null;

		if (type != null)
		{
			return $"{name} ({colour} {type})";
		}
		else
		{
			return $"{name} ({colour})";
		}
	}

	/// <summary>
	/// Formats an enum value by adding spaces between Pascal case words.
	/// </summary>
	/// <param name="value">The enum value as a string.</param>
	/// <returns>A formatted string with spaces between words.</returns>
	private static string FormatEnumWithSpaces(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return value;
		}

		var result = new StringBuilder();
		for (var i = 0; i < value.Length; i++)
		{
			if (i > 0 && char.IsUpper(value[i]))
			{
				result.Append(' ');
			}
			result.Append(value[i]);
		}
		return result.ToString();
	}

	/// <summary>
	/// Appends an indicator to the StringBuilder if more bin days were found than the display limit.
	/// </summary>
	/// <param name="summaryBuilder">The StringBuilder to append to.</param>
	/// <param name="binDaysCount">The total count of bin days found.</param>
	private static void AppendMoreBinDaysIndicator(StringBuilder summaryBuilder, int binDaysCount)
	{
		if (binDaysCount > _maxBinDaysToShow)
		{
			// Remove the last blank line added in the loop if we are adding the 'more' line
			if (summaryBuilder.Length >= Environment.NewLine.Length * 2 &&
				summaryBuilder.ToString(summaryBuilder.Length - Environment.NewLine.Length * 2, Environment.NewLine.Length * 2) == Environment.NewLine + Environment.NewLine)
			{
				summaryBuilder.Length -= Environment.NewLine.Length;
			}
			summaryBuilder.AppendLine($"{_primaryIndent}...");
			summaryBuilder.AppendLine();
		}
	}

	/// <summary>
	/// Appends the main summary footer to the StringBuilder.
	/// </summary>
	/// <param name="summaryBuilder">The StringBuilder to append to.</param>
	private static void AppendSummaryFooter(StringBuilder summaryBuilder)
	{
		summaryBuilder.AppendLine(_topBottomBorder);
	}

	/// <summary>
	/// Creates a string with text centered between padding characters.
	/// </summary>
	/// <param name="text">The text to center.</param>
	/// <param name="totalWidth">The total desired width of the resulting string.</param>
	/// <param name="padChar">The character used for padding on either side of the text.</param>
	/// <returns>A string with the text centered using the specified padding character.</returns>
	internal static string CreateCenteredHeader(string text, int totalWidth, char padChar)
	{
		if (string.IsNullOrEmpty(text))
		{
			return new string(padChar, totalWidth);
		}

		if (text.Length >= totalWidth)
		{
			return text;
		}

		var paddingNeeded = totalWidth - text.Length;
		var leftPadding = paddingNeeded / 2;
		var rightPadding = paddingNeeded - leftPadding;

		var headerBuilder = new StringBuilder(totalWidth);
		headerBuilder.Append(padChar, leftPadding);
		headerBuilder.Append(text);
		headerBuilder.Append(padChar, rightPadding);
		return headerBuilder.ToString();
	}
}
