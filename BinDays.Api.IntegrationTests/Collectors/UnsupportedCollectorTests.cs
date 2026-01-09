namespace BinDays.Api.IntegrationTests.Collectors;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Exceptions;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Services;
using Xunit;

public sealed class UnsupportedCollectorTests
{
	[Fact]
	public void GetCollector_WithUnsupportedCollector_ThrowsUnsupportedCollectorExceptionContainingName()
	{
		var collectorService = new CollectorService([]);

		var clientSideResponse = new ClientSideResponse
		{
			RequestId = 1,
			StatusCode = 200,
			Headers = [],
			Content =
				"""
				<html>
					<body>
						<span class="local-authority">Example Collector</span>
						<input type="hidden" value="https://www.gov.uk/rubbish-collection-day/example-collector" />
					</body>
				</html>
				""",
			ReasonPhrase = "OK"
		};

		var exception = Assert.Throws<UnsupportedCollectorException>(
			() => GovUkCollectorBase.GetCollector(collectorService, "AB12 3CD", clientSideResponse)
		);

		Assert.Equal("example-collector", exception.GovUkId);
		Assert.Equal("Example Collector", exception.CollectorName);
	}
}
