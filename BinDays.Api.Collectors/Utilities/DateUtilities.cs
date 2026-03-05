namespace BinDays.Api.Collectors.Utilities;

using System;
using System.Globalization;

/// <summary>
/// Provides utility methods for parsing dates.
/// </summary>
public static class DateUtilities
{
	/// <summary>
	/// Parses a date string using the given format with <see cref="CultureInfo.InvariantCulture"/>.
	/// </summary>
	/// <param name="input">The date string to parse.</param>
	/// <param name="format">The expected date format (e.g. "yyyy-MM-dd").</param>
	public static DateOnly ParseDateExact(string input, string format)
	{
		if (!format.Contains('y', StringComparison.OrdinalIgnoreCase))
		{
			throw new ArgumentException(
				$"The format string '{format}' does not contain a year specifier. Use ParseDateInferringYear instead.",
				nameof(format)
			);
		}

		return DateOnly.ParseExact(input, format, CultureInfo.InvariantCulture, DateTimeStyles.None);
	}

	/// <summary>
	/// Resolves "Today" and "Tomorrow" to their corresponding <see cref="DateOnly"/> values,
	/// or delegates to <see cref="ParseDateExact"/> for all other date strings.
	/// </summary>
	/// <param name="input">The date string to parse (e.g. "Today", "Tomorrow", or "Monday 29 December 2025").</param>
	/// <param name="format">The expected date format including the year, passed to <see cref="ParseDateExact"/> when needed.</param>
	public static DateOnly ParseRelativeDateOrExact(string input, string format)
	{
		if (!format.Contains('y', StringComparison.OrdinalIgnoreCase))
		{
			throw new ArgumentException(
				$"The format string '{format}' does not contain a year specifier. Use ParseRelativeDateOrInferYear instead.",
				nameof(format)
			);
		}

		if (input.Equals("Today", StringComparison.OrdinalIgnoreCase))
		{
			return DateOnly.FromDateTime(DateTime.Today);
		}

		if (input.Equals("Tomorrow", StringComparison.OrdinalIgnoreCase))
		{
			return DateOnly.FromDateTime(DateTime.Today.AddDays(1));
		}

		return ParseDateExact(input, format);
	}

	/// <summary>
	/// Resolves "Today" and "Tomorrow" to their corresponding <see cref="DateOnly"/> values,
	/// or delegates to <see cref="ParseDateInferringYear"/> for all other date strings.
	/// </summary>
	/// <param name="input">The date string to parse (e.g. "Today", "Tomorrow", or "Monday 29 December").</param>
	/// <param name="format">The expected date format excluding the year, passed to <see cref="ParseDateInferringYear"/> when needed.</param>
	public static DateOnly ParseRelativeDateOrInferYear(string input, string format)
	{
		if (format.Contains('y', StringComparison.OrdinalIgnoreCase))
		{
			throw new ArgumentException(
				$"The format string '{format}' already contains a year specifier. Use ParseRelativeDateOrExact instead.",
				nameof(format)
			);
		}

		if (input.Equals("Today", StringComparison.OrdinalIgnoreCase))
		{
			return DateOnly.FromDateTime(DateTime.Today);
		}

		if (input.Equals("Tomorrow", StringComparison.OrdinalIgnoreCase))
		{
			return DateOnly.FromDateTime(DateTime.Today.AddDays(1));
		}

		return ParseDateInferringYear(input, format);
	}

	/// <summary>
	/// Parses a date string that lacks a year by checking the current, previous, and next years.
	/// It returns the valid date that is chronologically closest to today.
	/// </summary>
	/// <param name="input">The date string to parse (e.g. "Monday 29 December").</param>
	/// <param name="format">The expected date format excluding the year (e.g. "dddd d MMMM").</param>
	public static DateOnly ParseDateInferringYear(string input, string format)
	{
		if (format.Contains('y', StringComparison.OrdinalIgnoreCase))
		{
			throw new ArgumentException(
				$"The format string '{format}' already contains a year specifier. Use ParseDateExact instead.",
				nameof(format)
			);
		}

		var today = DateOnly.FromDateTime(DateTime.Now);
		var formatWithYear = $"{format} yyyy";

		// We test the Current Year, Next Year, and Previous Year.
		// This covers crossovers like parsing "Dec 29" on "Jan 1".
		int[] yearsToTry = [today.Year, today.Year + 1, today.Year - 1];

		DateOnly? bestMatch = null;
		var minDistanceInDays = int.MaxValue;

		foreach (var year in yearsToTry)
		{
			// TryParseExact with "dddd" (day of week) will return false if the
			// day of the week doesn't match that specific calendar year.
			if (DateOnly.TryParseExact($"{input} {year}", formatWithYear, CultureInfo.InvariantCulture, DateTimeStyles.None, out var candidate))
			{
				// Calculate how many days away this date is from today (absolute value)
				var distance = Math.Abs(candidate.DayNumber - today.DayNumber);

				if (distance < minDistanceInDays)
				{
					minDistanceInDays = distance;
					bestMatch = candidate;
				}
			}
		}

		if (bestMatch.HasValue)
		{
			return bestMatch.Value;
		}

		// Fallback: If no year produced a valid date (e.g. Feb 29 on a non-leap year),
		// we call ParseExact on the current year to trigger the standard FormatException.
		return DateOnly.ParseExact($"{input} {today.Year}", formatWithYear, CultureInfo.InvariantCulture, DateTimeStyles.None);
	}
}
