# Project: BinDays API

This document provides instructions and guidelines for contributing to the BinDays API. As an AI agent, you are expected to follow these instructions meticulously.

## General Instructions

- Your primary task is to add support for a new council to the BinDays API.
- You must follow the existing coding style, conventions, and architectural patterns.
- All new collectors require a corresponding integration test.
- **Do not modify core files.** Your changes should be limited to creating one new collector class and one new integration test file. Do not alter any other part of the existing codebase.

## Coding Style & Conventions

- **HTML Parsing:** All HTML parsing must be done using regular expressions, as is the convention in this repository.
- **Temporary Debugging:** You are encouraged to add temporary print/debug statements (e.g., `Console.WriteLine`) to your code to understand the data you are working with. However, you **must** remove all such temporary statements before you consider the task complete.

## Guiding Principles & Best Practices

- **Reference Existing Work:** Before writing any code, thoroughly review the existing council implementations in `BinDays.Api.Collectors/Collectors/Councils/`. This is crucial for maintaining consistency and identifying reusable patterns.
- **Use Playwright for Web Interaction:** All website interactions must be performed using the Playwright MCP server. Do not use direct HTTP request tools like `curl` or `view_text_website`. Your investigation must be based on browser automation with Playwright.

## Workflow for Adding a New Council Collector

This repository includes custom Gemini CLI commands and a utility script to streamline the process of adding a new council collector.

### Step 1: Fetch Collector Data

Use the `/fetch_collector_data` command to begin the process. This will generate a HAR file in the `tmp_collector_data` directory containing all network traffic from the user journey.

**Example:** `/fetch_collector_data SW1A 0AA`

### Step 2: Filter the HAR File

The generated `requests.har` file contains a lot of irrelevant data (e.g., CSS, images). Run the `FilterHar.csx` script to remove this noise and create a smaller, more focused `filtered_requests.har` file.

**Example:** `dotnet script scripts/FilterHar.csx tmp_collector_data/requests.har tmp_collector_data/filtered_requests.har`

### Step 3: Create the Collector

Once the filtered HAR file has been created, use the `/create_collector` command to generate the new collector class and integration test.

**Example:** `/create_collector`

This command will read the `filtered_requests.har` and `council_info.json` files to generate the necessary C# files. After the files are created, you may need to manually adjust the generated regular expressions in the collector class to ensure they correctly parse the data.

### Step 4: Run the Integration Test

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
