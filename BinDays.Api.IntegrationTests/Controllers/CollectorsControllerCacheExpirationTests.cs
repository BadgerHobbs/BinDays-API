namespace BinDays.Api.IntegrationTests.Controllers;

using BinDays.Api.Collectors.Models;
using BinDays.Api.Controllers;
using System;
using Xunit;

/// <summary>
/// Tests that <see cref="CollectorsController.GetBinDaysCacheExpiration"/> caches bin days until the day after the
/// earliest collection, and refreshes responses fetched overnight once council sites have rolled forward.
/// </summary>
public sealed class CollectorsControllerCacheExpirationTests
{
	[Theory]
	[InlineData("2026-10-15T12:00:00Z", "2026-10-21", "2026-10-22T00:00:00Z")] // Daytime lookup
	[InlineData("2026-10-15T07:00:00Z", "2026-10-21", "2026-10-22T00:00:00Z")] // 08:00 BST, no longer overnight
	[InlineData("2026-10-15T04:15:00Z", "2026-10-21", "2026-10-15T07:00:00Z")] // Overnight during BST, refreshed at 08:00 BST
	[InlineData("2026-11-12T03:00:00Z", "2026-11-18", "2026-11-12T08:00:00Z")] // Overnight during GMT, refreshed at 08:00 GMT
	[InlineData("2026-10-25T00:30:00Z", "2026-10-28", "2026-10-25T08:00:00Z")] // Overnight as the clocks go back
	[InlineData("2026-10-15T23:30:00Z", "2026-10-15", "2026-10-16T00:00:00Z")] // Overnight, but already expires before 08:00
	public void GetBinDaysCacheExpiration_ReturnsExpectedExpiration(string utcNow, string earliestBinDayDate, string expectedExpiration)
	{
		var address = new Address { Postcode = "AB1 2CD", Uid = "1" };
		var earliestDate = DateOnly.Parse(earliestBinDayDate);

		BinDay[] binDays =
		[
			new() { Date = earliestDate.AddDays(7), Address = address, Bins = [] },
			new() { Date = earliestDate, Address = address, Bins = [] },
		];

		var result = CollectorsController.GetBinDaysCacheExpiration(binDays, DateTimeOffset.Parse(utcNow));

		Assert.Equal(DateTimeOffset.Parse(expectedExpiration), result);
	}
}
