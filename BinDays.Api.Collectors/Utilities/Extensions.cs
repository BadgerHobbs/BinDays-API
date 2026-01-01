namespace BinDays.Api.Collectors.Utilities
{
	using System;
	using System.Globalization;

	/// <summary>
	/// Provides extension methods for various types.
	/// </summary>
	public static class Extensions
	{
		/// <summary>
		/// Parses a date string that lacks a year by checking the current, previous, and next years.
		/// It returns the valid date that is chronologically closest to today.
		/// </summary>
		/// <param name="input">The date string to parse (e.g. "Monday 29 December").</param>
		/// <param name="format">The expected date format excluding the year (e.g. "dddd d MMMM").</param>
		public static DateOnly ParseDateInferringYear(this string input, string format)
		{
			if (format.Contains('y', StringComparison.OrdinalIgnoreCase))
			{
				throw new ArgumentException(
					$"The format string '{format}' already contains a year specifier. Use DateOnly.ParseExact directly.",
					nameof(format)
				);
			}

			var today = DateOnly.FromDateTime(DateTime.Now);
			var formatWithYear = $"{format} yyyy";

			// We test the Current Year, Next Year, and Previous Year.
			// This covers crossovers like parsing "Dec 29" on "Jan 1".
			int[] yearsToTry = [today.Year, today.Year + 1, today.Year - 1];

			DateOnly? bestMatch = null;
			int minDistanceInDays = int.MaxValue;

			foreach (var year in yearsToTry)
			{
				// TryParseExact with "dddd" (day of week) will return false if the 
				// day of the week doesn't match that specific calendar year.
				if (DateOnly.TryParseExact($"{input} {year}", formatWithYear, CultureInfo.InvariantCulture, DateTimeStyles.None, out var candidate))
				{
					// Calculate how many days away this date is from today (absolute value)
					int distance = Math.Abs(candidate.DayNumber - today.DayNumber);

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

			// Fallback: If no year produced a valid date (e.g., Feb 29 on a non-leap year),
			// we call ParseExact on the current year to trigger the standard FormatException.
			return DateOnly.ParseExact($"{input} {today.Year}", formatWithYear, CultureInfo.InvariantCulture, DateTimeStyles.None);
		}
	}
}