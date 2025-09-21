# Project: BinDays API

This document provides instructions and guidelines for contributing to the BinDays API, specifically for adding new council collectors.

## General Instructions:

- Your primary task is to add support for a new council to the BinDays API.
- You must follow the existing coding style, conventions, and architectural patterns.
- All new collectors require a corresponding integration test.
- All HTML parsing must be done using regular expressions, as is the convention in this repository.
- **Do not modify core files.** Your changes should be limited to creating one new collector class and one new integration test file. Do not alter any other part of the existing codebase.

## Guiding Principles

- **Reference Existing Work:** Before writing any code, thoroughly review the existing council implementations in `BinDays.Api.Collectors/Collectors/Councils/`. This is crucial for maintaining consistency and identifying reusable patterns. Many councils use similar back-end systems, so you may find an existing collector that is very close to what is needed for the new council.
- **Use Playwright for Web Interaction:** All website interactions must be performed using the Playwright MCP server. Do not use direct HTTP request tools like `curl` or `view_text_website`. Your investigation should be based on browser automation with Playwright.
- **Temporary Debugging:** You are encouraged to add temporary print/debug statements (e.g., `Console.WriteLine`) to your code to output HTML, JSON, or other data to understand the responses you are working with. However, you **must** remove all such temporary statements before you consider the task complete.

## Workflow for Adding a New Council Collector:

This repository includes custom Gemini CLI commands to streamline the process of adding a new council collector.

### Step 1: Fetch Collector Data

Use the `/fetch_collector_data` command to begin the process. You will need to provide a postcode for the new council area.

Example: `/fetch_collector_data SW1A 0AA`

This command will guide you through the process of:
-   Navigating to the council's website.
-   Finding the bin collection schedule.
-   Capturing the necessary network requests for address and bin day lookups.

The command will save the captured data into the following files in the root of the repository:
-   `council_info.json`
-   `address_request.json`
-   `address_response.json`
-   `binday_request.json`
-   `binday_response.json`

### Step 2: Create the Collector

Once the data has been fetched, use the `/create_collector` command to generate the new collector files.

Example: `/create_collector`

This command will:
-   Read the data files created in the previous step.
-   Generate a new C# collector class in `BinDays.Api.Collectors/Collectors/Councils/`.
-   Generate a new integration test class in `BinDays.Api.IntegrationTests/Collectors/Councils/`.

After the files are created, you may need to manually adjust the generated regular expressions in the collector class to ensure they correctly parse the data from the saved responses.

### Step 3: Run the Integration Test

Finally, run the newly created integration test to verify that the collector is working correctly.

## Collector Implementation Template:

```c#
namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	.Text.RegularExpressions;

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
				// ...
			}

			// Throw exception for invalid request
			throw new InvalidOperationException("Invalid client-side request.");
		}
	}
}
```

## Integration Test Template:

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
		private const string _postcode = "ABCD EFG";

		private readonly ITestOutputHelper _outputHelper;

		public MyNewCouncilTests(ITestOutputHelper outputHelper)
		{
			_outputHelper = outputHelper;
		}

		[Fact]
		public async Task GetBinDaysTest()
		{
			await TestSteps.EndToEnd(
				_client,
				_collectorService,
				_collector,
				_postcode,
				_outputHelper
			);
		}
	}
}
```
