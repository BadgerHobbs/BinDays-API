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

	[Theory]
	[InlineData(
		"dxp-sessionid=abc123; Max-Age=3600; Secure; HttpOnly; Path=/, __cf_bm=def456; HttpOnly; SameSite=None; Secure; Path=/; Domain=www.enfield.gov.uk; Expires=Sat, 22 Aug 2026 10:17:38 GMT",
		"dxp-sessionid=abc123; __cf_bm=def456")]
	[InlineData(
		"dxp-sessionid=abc123; Max-Age=3600; Secure; HttpOnly; Path=/\n__cf_bm=def456; HttpOnly; SameSite=None; Secure; Path=/; Domain=www.enfield.gov.uk; Expires=Sat, 22 Aug 2026 10:17:38 GMT",
		"dxp-sessionid=abc123; __cf_bm=def456")]
	public void ParseSetCookieHeaderForRequestCookie_ParsesMultipleCookies(string setCookieHeader, string expected)
	{
		var result = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

		Assert.Equal(expected, result);
	}
}
