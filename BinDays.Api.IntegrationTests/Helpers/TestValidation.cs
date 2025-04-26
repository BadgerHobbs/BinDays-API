namespace BinDays.Api.IntegrationTests.Helpers
{
	using BinDays.Api.Collectors.Collectors;
	using BinDays.Api.Collectors.Models;

	/// <summary>
	/// Provides common validation helper methods for collector integration tests, using TestContext for logging.
	/// </summary>
	// Renamed Class
	internal static class TestValidation
	{
		/// <summary>
		/// Validates the result of a GetCollector operation.
		/// </summary>
		/// <param name="collector">The collector returned by the operation.</param>
		/// <param name="expectedType">The expected concrete type of the collector.</param>
		/// <param name="expectedGovUkId">The expected GOV.UK ID of the collector.</param>
		public static void ValidateCollectorResult(
			ICollector? collector,
			Type expectedType,
			string expectedGovUkId)
		{
			Assert.IsNotNull(collector, "Collector should not be null.");
			Assert.IsInstanceOfType(collector, expectedType, $"Collector should be of type {expectedType.Name}.");
			Assert.AreEqual(expectedGovUkId, collector.GovUkId, "Collector GovUkId does not match.");
		}

		/// <summary>
		/// Validates the result of a GetAddresses operation.
		/// </summary>
		/// <param name="addresses">The collection of addresses returned by the operation.</param>
		/// <param name="expectedPostcode">The postcode that was searched for.</param>
		/// <param name="expectedMinCount">The minimum number of addresses expected.</param>
		/// <param name="ensureUidPresent">If true, asserts that all addresses have a non-empty Uid.</param>
		/// <param name="expectedUidToContain">Optional: An specific Uid that must be present in the results.</param>
		public static void ValidateAddressesResult(
			IReadOnlyCollection<Address>? addresses,
			string expectedPostcode,
			int expectedMinCount = 1,
			bool ensureUidPresent = true,
			string? expectedUidToContain = null)
		{
			Assert.IsNotNull(addresses, "Addresses collection should not be null.");
			Assert.IsTrue(addresses.Count >= expectedMinCount, $"Should retrieve at least {expectedMinCount} address(es). Found {addresses.Count}.");

			if (ensureUidPresent)
			{
				Assert.IsTrue(addresses.All(a => !string.IsNullOrWhiteSpace(a.Uid)), "All addresses should have a non-whitespace Uid.");
			}

			Assert.IsTrue(addresses.All(a => expectedPostcode.Equals(a.Postcode, StringComparison.OrdinalIgnoreCase)), "All addresses should have the correct postcode.");

			if (!string.IsNullOrWhiteSpace(expectedUidToContain))
			{
				Assert.IsTrue(addresses.Any(a => expectedUidToContain.Equals(a.Uid, StringComparison.OrdinalIgnoreCase)), $"Expected Uid '{expectedUidToContain}' not found in results.");
			}
		}

		/// <summary>
		/// Validates the result of a GetBinDays operation.
		/// </summary>
		/// <param name="binDays">The collection of bin days returned by the operation.</param>
		/// <param name="expectedMinCount">The minimum number of bin days expected.</param>
		/// <param name="ensureBinsPresent">If true, asserts that all bin days have associated bins.</param>
		/// <param name="ensureFutureDates">If true, asserts that all bin days are today or in the future.</param>
		public static void ValidateBinDaysResult(
			IReadOnlyCollection<BinDay>? binDays,
			int expectedMinCount = 1,
			bool ensureBinsPresent = true,
			bool ensureFutureDates = true)
		{
			Assert.IsNotNull(binDays, "BinDays collection should not be null.");
			Assert.IsTrue(binDays.Count >= expectedMinCount, $"Should retrieve at least {expectedMinCount} bin day(s). Found {binDays.Count}.");

			if (ensureBinsPresent)
			{
				Assert.IsTrue(binDays.All(bd => bd.Bins != null && bd.Bins.Count > 0), "All bin days should have at least one bin type associated.");
			}

			if (ensureFutureDates)
			{
				DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
				Assert.IsTrue(binDays.All(bd => bd.Date >= today), "All bin dates should be today or in the future.");
			}
		}
	}
}