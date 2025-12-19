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
		/// Parses a date string that lacks a year. It tries the current year first; 
		/// if the date is invalid (e.g. wrong DayOfWeek) or older than 1 month, it infers the next year.
		/// </summary>
		/// <param name="input">The date string to parse (e.g. "Friday 15 Dec").</param>
		/// <param name="format">The expected date format excluding the year (e.g. "dddd d MMM").</param>
		public static DateOnly ParseDateInferringYear(this string input, string format)
		{
			if (format.Contains('y', StringComparison.OrdinalIgnoreCase))
			{
				throw new ArgumentException(
					$"The format string '{format}' already contains a year specifier. Use DateOnly.ParseExact directly instead of ParseDateInferringYear.",
					nameof(format)
				);
			}

			var today = DateOnly.FromDateTime(DateTime.Now);
			var formatWithYear = $"{format} yyyy";

			// Try the current year first
			if (DateOnly.TryParseExact($"{input} {today.Year}", formatWithYear, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateInCurrentYear))
			{
				// Return if valid and not older than the 1-month grace period
				if (dateInCurrentYear >= today.AddMonths(-1))
				{
					return dateInCurrentYear;
				}
			}

			// Fallback to next year (handles day-of-week mismatches or dates significantly in the past)
			return DateOnly.ParseExact(
				$"{input} {today.Year + 1}",
				formatWithYear,
				CultureInfo.InvariantCulture,
				DateTimeStyles.None
			);
		}
	}
}
