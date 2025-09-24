# Contributing to BinDays-API

Contributions are welcome. This document provides guidelines for contributing.

## Project Structure

The BinDays project is split into three repositories:

- **[BinDays-API](https://github.com/BadgerHobbs/BinDays-API):** (This repository) The back-end service that contains the logic for scraping council websites.
- **[BinDays-Client](https://github.com/BadgerHobbs/BinDays-Client):** A Dart library that communicates with the API.
- **[BinDays-App](https://github.com/BadgerHobbs/BinDays-App):** The Flutter mobile application that uses the Client.

If your issue is with council data, you are in the right place. For app or client issues, please visit the respective repository.

## How to Contribute

- **Bug Reports:** If a collector is broken, [**create a bug report**](https://github.com/BadgerHobbs/BinDays-API/issues/new?template=bug-report.md).
- **Council Requests:** To request a new council, [**submit a council request**](https://github.com/BadgerHobbs/BinDays-API/issues/new?template=council-request.md).

Search existing issues first to avoid duplicates.

## Adding a New Council Collector

Adding new councils is the most common contribution. Each council website is different, so this requires reverse-engineering their web traffic.

**Guiding Principles:**

- **No Browser Emulation:** Don't use tools like Selenium. Replicate the necessary HTTP requests directly.
- **Follow Existing Patterns:** New collectors should follow the style of existing ones.
- **Integration Tests are Required:** Every new collector needs an integration test.

### Development Workflow

1.  **Prerequisites:**

    - .NET SDK
    - An IDE like Visual Studio, Rider, or VSCode

2.  **Create Collector Files:**

    - **Collector Class:** Create a `.cs` file in `BinDays.Api.Collectors/Collectors/Councils/`.
    - **Integration Test:** Create a test file in `BinDays.Api.IntegrationTests/Collectors/Councils/`.

3.  **Implement the Collector:**

    - Use your browser's developer tools to analyze the council website's network traffic for bin day lookups.
    - Replicate these requests in your collector's `GetAddresses` and `GetBinDays` methods.
    - Parse the HTML or JSON responses to get the required data.

4.  **Write the Integration Test:**

    - The test should verify that your collector can retrieve bin days for a test postcode.
    - Use the `TestSteps.EndToEnd` helper.

5.  **Run Tests:**
    - Run tests from your IDE or the command line:
      ```bash
      dotnet test --filter "Name~MyNewCouncil"
      ```

### Collector Implementation Template

```c#
namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Collector implementation for My New Council.
	/// </summary>
	internal sealed partial class MyNewCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "My New Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://www.mynewcouncil.gov.uk/");

		/// <inheritdoc/>
		public override string GovUkId => "my-new-council";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly ReadOnlyCollection<Bin> binTypes = new List<Bin>()
		{
			new()
			{
				Name = "Human Readable Bin Name",
				Colour = "Grey",
				Keys = new List<string>() { "BIN_ID_IN_DATA" }.AsReadOnly(),
			},
		}.AsReadOnly();

		/// <summary>
		/// Regex for the addresses from the data.
		/// </summary>
		[GeneratedRegex(@"")]
		private static partial Regex AddressRegex();

		/// <summary>
		/// Regex for the bin days from the data.
		/// </summary>
		[GeneratedRegex(@"")]
		private static partial Regex BinDaysRegex();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting addresses
			if (clientSideResponse.RequestId == null)
			{
				// ...
			}
			// Process addresses from response
			else if (clientSideResponse.RequestId == 2)
			{
				// ...
			}

			// Throw exception for invalid request
			throw new InvalidOperationException("Invalid client-side request.");
		}

		/// <inheritdoc/>
		public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting bin days
			if (clientSideResponse.RequestId == null)
			{
				// ...
			}
			// Process bin days from response
			else if (clientSideResponse.RequestId == 2)
			{
				var binDays = new List<BinDay>();

				// ...

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					BinDays = ProcessingUtilities.ProcessBinDays(binDays),
					NextClientSideRequest = null
				};

				return getBinDaysResponse;
			}

			// Throw exception for invalid request
			throw new InvalidOperationException("Invalid client-side request.");
		}
	}
}
```

### Integration Test Template

```c#
namespace BinDays.Api.IntegrationTests.Collectors.Councils
{
	using BinDays.Api.Collectors.Collectors;
	using BinDays.Api.Collectors.Collectors.Councils;
	using BinDays.Api.Collectors.Services;
	using BinDays.Api.IntegrationTests.Helpers;
	using System.Threading.Tasks;
	using Xunit;
	using Xunit.Abstractions;

	public class MyNewCouncilTests
	{
		private readonly IntegrationTestClient _client = new();
		private static readonly ICollector _collector = new MyNewCouncil();
		private readonly CollectorService _collectorService = new([_collector]);
		private readonly ITestOutputHelper _outputHelper;

		public MyNewCouncilTests(ITestOutputHelper outputHelper)
		{
			_outputHelper = outputHelper;
		}

		[Theory]
		[InlineData("ABCD EFG")]
		public async Task GetBinDaysTest(string postcode)
		{
			await TestSteps.EndToEnd(
				_client,
				_collectorService,
				_collector,
				postcode,
				_outputHelper
			);
		}
	}
}
```

## Code Style and Commits

- **Code Style:** Match the existing C# conventions and formatting.
- **Commit Messages:** Be descriptive. Explain what you changed and why.
