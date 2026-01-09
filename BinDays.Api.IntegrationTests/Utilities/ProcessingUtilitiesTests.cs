namespace BinDays.Api.IntegrationTests.Utilities;

using BinDays.Api.Collectors.Utilities;
using Xunit;

public class ProcessingUtilitiesTests
{
	[Theory]
	[InlineData("SW1A 0AA", "SW1A 0AA")]
	[InlineData("SW1A0AA", "SW1A 0AA")]
	[InlineData("sw1a 0aa", "SW1A 0AA")]
	[InlineData("SW1A  0AA", "SW1A 0AA")]
	[InlineData(" SW1A 0AA ", "SW1A 0AA")]
	[InlineData("SW1A\t0AA", "SW1A 0AA")] // Tab
	[InlineData("SW1A\u00A00AA", "SW1A 0AA")] // Non-breaking space
	public void FormatPostcode_CleansSpacesCorrectly(string input, string expected)
	{
		var result = ProcessingUtilities.FormatPostcode(input);
		Assert.Equal(expected, result);
	}
}
