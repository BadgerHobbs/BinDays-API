namespace BinDays.Api.IntegrationTests.Collectors;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Exceptions;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>
/// Tests that a gov.uk response with no usable collector id is reported as a missing
/// gov.uk id rather than surfacing as an unexpected error.
/// </summary>
public sealed class GovUkIdNotFoundTests
{
	[Fact]
	public void GetCollector_WithNoGovUkIdInResponse_ThrowsGovUkIdNotFoundException()
	{
		var collectorService = new CollectorService([], NullLogger<CollectorService>.Instance);

		var clientSideResponse = new ClientSideResponse
		{
			RequestId = 1,
			StatusCode = 200,
			Headers = [],
			Content =
				"""
				<html>
					<body>
						<div>Nothing useful in here.</div>
					</body>
				</html>
				""",
			ReasonPhrase = "OK",
		};

		var exception = Assert.Throws<GovUkIdNotFoundException>(
			() => GovUkCollectorBase.GetCollector(collectorService, "LS1 4DY", clientSideResponse)
		);

		Assert.Equal("LS1 4DY", exception.Postcode);
	}

	[Fact]
	public void GetCollector_WithCollectorNameButNoGovUkId_ThrowsGovUkIdNotFoundException()
	{
		var collectorService = new CollectorService([], NullLogger<CollectorService>.Instance);

		var clientSideResponse = new ClientSideResponse
		{
			RequestId = 1,
			StatusCode = 200,
			Headers = [],
			Content =
				"""
				<html>
					<body>
						<span class="local-authority">Leeds City Council</span>
					</body>
				</html>
				""",
			ReasonPhrase = "OK",
		};

		Assert.Throws<GovUkIdNotFoundException>(
			() => GovUkCollectorBase.GetCollector(collectorService, "LS1 4DY", clientSideResponse)
		);
	}
}
