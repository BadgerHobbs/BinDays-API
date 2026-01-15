# Task: Implement a New Bin Day Collector from GitHub Issue

You are an AI agent tasked with implementing a new UK council bin day collector for the BinDays API project. You will use Playwright MCP to navigate the council's website, capture network requests, and then implement a C# collector based on that data.

**This is a one-shot, end-to-end implementation.** You must complete all phases autonomously from start to finish without requiring additional user input. This includes:

- Parsing the issue and extracting council information
- Using Playwright to navigate the website and capture requests
- Implementing the collector class in C#
- Creating and running integration tests
- Debugging and fixing any issues until tests pass
- Reporting completion when all tests pass successfully

Do not stop or ask for approval between phases - execute the entire workflow in one continuous operation.

## Input

**Council Name (from issue title):** `$ISSUE_TITLE`

**GitHub Issue Content:**

```
$ISSUE_BODY
```

**Issue Number:** `$ISSUE_NUMBER`

---

## Phase 0: Pre-flight Verification

Before beginning implementation, verify that all required tools are installed and working correctly.

### 0.1 Verify .NET Build

Test that the .NET SDK is installed and can build the project:

```bash
dotnet --version
```

Verify the version is displayed (should be .NET 8.0 or higher).

Build the solution to ensure it compiles:

```bash
dotnet build
```

The build must complete successfully with no errors. If the build fails, report the error and stop - do not proceed with implementation.

### 0.2 Verify Playwright MCP

Verify that Playwright MCP server is available and responding:

1. Check that the Playwright MCP server is configured and accessible
2. Test a simple navigation to verify Playwright is working correctly
3. Ensure the browser can launch and navigate to a basic URL (e.g., `https://bindays.app`)

If Playwright MCP is not available or fails to navigate, report the error and stop - do not proceed with implementation.

### 0.3 Verify Required Scripts

Check that required helper scripts exist:

```bash
test -f .agent/scripts/clean-har.js && echo "clean-har.js found" || echo "ERROR: clean-har.js not found"
```

If any required scripts are missing, report the error and stop.

**Only proceed to Phase 1 if all pre-flight checks pass successfully.**

---

## Phase 1: Parse Issue and Setup

### 1.1 Extract Information from Issue

The **Council Name** is provided in the issue title above.

Parse the GitHub issue body to extract:

- **GOV.UK URL**: Extract the council ID from the URL (e.g. `west-devon` from `https://www.gov.uk/rubbish-collection-day/west-devon`)
- **Council Website**: The main council website URL
- **Bin Collection Page**: The direct link to look up bin collections
- **Example Postcode**: A valid postcode for testing
- **Bin Types & Colours**: List of bins and their colours (for reference)

### 1.2 Create PascalCase Name

Convert the council name to PascalCase for use in filenames and class names:

- "West Devon Borough Council" → "WestDevonBoroughCouncil"
- "Bristol City Council" → "BristolCityCouncil"

### 1.3 Setup Output Directory

Ensure the output directory exists:

```bash
mkdir -p .agent/playwright/out
```

Delete any existing HAR file:

```bash
rm -f .agent/playwright/out/requests.har
```

---

## Phase 2: Navigate Council Website with Playwright MCP

### 2.1 Navigate and Record

Use the Playwright MCP server to:

1. Navigate to the **Bin Collection Page** URL from the issue
2. Handle any cookie consent dialogs (accept/dismiss)
3. Fill in the **Example Postcode** from the issue
4. Submit the form and select an address if prompted
5. Navigate to the page showing bin collection dates

**Record each interaction step** for later reference:

- `goto` - Initial navigation
- `click` - Button clicks, cookie accepts
- `fill` - Form field inputs
- `select` - Dropdown selections

### 2.2 Scrape Bin Collection Data

On the final page showing bin collections:

1. Analyze the page structure (tables, divs, lists)
2. Extract for each collection:
   - Date of collection
   - Bin names/descriptions
   - Bin colours (if visible)
   - Container types (bin, box, bag, caddy)

**IMPORTANT:** Only record the actual collection dates shown on the page. Do NOT calculate or infer additional dates based on intervals, even if:

- The pattern suggests regular intervals (e.g. every 2 weeks)
- The website states "and every other week" or similar
- You can deduce a schedule from the visible dates

Collectors must return ONLY the true data provided by the council, not computed projections.

### 2.3 Close Browser and Save Data

1. **Take a screenshot** of the final bin collections page showing the collection dates:

   - Use Playwright's screenshot functionality to capture the page
   - Save as `.agent/playwright/out/{CouncilName}-screenshot.png`
   - This screenshot will be included in the pull request to help with verification/validation

2. Close the Playwright browser session (this saves the HAR file automatically)

3. Clean the HAR file to reduce context:

   ```bash
   node .agent/scripts/clean-har.js .agent/playwright/out/requests.har .agent/playwright/out/{CouncilName}.cleaned.har
   ```

4. Save collector info as JSON at `.agent/playwright/out/{CouncilName}.json`:
   ```json
   {
     "postcode": "...",
     "councilName": "...",
     "councilWebsite": "...",
     "govUkId": "...",
     "harFilePath": "./.agent/playwright/out/{CouncilName}.cleaned.har",
     "steps": [...],
     "collections": [...]
   }
   ```

---

## Phase 3: Implement the Collector

### 3.1 Study Existing Patterns

Before writing code, analyze:

- The style guide in `.gemini/styleguide.md`
- Existing collectors in `BinDays.Api.Collectors/Collectors/Councils/`
- Base classes in `BinDays.Api.Collectors/Collectors/Vendors/`
- The cleaned HAR file to understand the HTTP request flow

### 3.2 Choose Base Class

Determine the appropriate base class:

- `GovUkCollectorBase` - Most common, for custom implementations
- `FccCollectorBase` - For councils using FCC Environment services
- `ITouchVisionCollectorBase` - For councils using iTouchVision
- `BinzoneCollectorBase` - For councils using Binzone

If unsure, use `GovUkCollectorBase`.

### 3.3 Implement Collector Class

Create `BinDays.Api.Collectors/Collectors/Councils/{CouncilName}.cs`:

Key implementation details:

- **Stateless design**: Each request step is independent
- **Use `if/else if` pattern**: Based on `clientSideResponse.RequestId`
- **Extract tokens/cookies**: From HTML using `[GeneratedRegex]` attributes
- **Define `_binTypes`**: Map bin identifiers to human-readable names and colours
- **Use `ProcessingUtilities`**: For cookie parsing, form data, bin matching
- **Parse dates**: Use `DateOnly.ParseExact` with `CultureInfo.InvariantCulture`

### 3.4 Create Integration Test

Create `BinDays.Api.IntegrationTests/Collectors/Councils/{CouncilName}Tests.cs`:

```csharp
namespace BinDays.Api.IntegrationTests.Collectors.Councils;

using BinDays.Api.Collectors.Collectors;
using BinDays.Api.Collectors.Collectors.Councils;
using BinDays.Api.Collectors.Services;
using BinDays.Api.IntegrationTests.Helpers;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

public class {CouncilName}Tests
{
    private readonly IntegrationTestClient _client;
    private static readonly ICollector _collector = new {CouncilName}();
    private readonly CollectorService _collectorService = new([_collector]);
    private readonly ITestOutputHelper _outputHelper;

    public {CouncilName}Tests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _client = new IntegrationTestClient(outputHelper);
    }

    [Theory]
    [InlineData("{postcode}")]
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
```

---

## Phase 4: Test and Debug

**This phase requires persistence.** Continue debugging and fixing issues until all tests pass. Do not report failure - iterate until successful.

### 4.1 Run Tests

```bash
dotnet test --filter "FullyQualifiedName~{CouncilName}Tests.GetBinDaysTest" --logger "console;verbosity=detailed" BinDays.Api.IntegrationTests/BinDays.Api.IntegrationTests.csproj
```

The test output will include:

- Collector name
- Addresses found
- **Bin Types**: A summary of all unique bin types with their names, colours, and container types
- Bin Days: Collection dates with associated bins

### 4.2 Debug Failures

If tests fail, enable HTTP logging:

```bash
export BINDAYS_ENABLE_HTTP_LOGGING=true
dotnet test --filter "FullyQualifiedName~{CouncilName}Tests.GetBinDaysTest" --logger "console;verbosity=detailed" BinDays.Api.IntegrationTests/BinDays.Api.IntegrationTests.csproj
```

Compare the logged requests against the HAR file:

- Check URLs match exactly
- Verify headers (especially cookies, content-type, CSRF tokens)
- Compare request body format and content
- Check for missing or incorrect form fields

See `DEBUGGING.md` for detailed debugging instructions.

### 4.3 Fix and Retry

Common issues:

- **Missing cookies**: Extract from `set-cookie` header using `ProcessingUtilities.ParseSetCookieHeaderForRequestCookie`
- **Missing CSRF token**: Extract from HTML with regex
- **Wrong date format**: Check the exact format in responses and adjust `ParseExact` pattern
- **Regex not matching**: Test regex against actual response content

Continue fixing and re-running tests until they pass.

---

## Phase 5: Completion

**Only reach this phase when all tests pass successfully.** Do not report completion with failing tests or unresolved issues.

When tests pass successfully:

1. Verify the output shows valid bin collection dates
2. Confirm bin types are correctly identified with proper names, colours, and container types
3. Check that the test summary shows all expected bin types for the council
4. Report success with the files created:
   - `BinDays.Api.Collectors/Collectors/Councils/{CouncilName}.cs`
   - `BinDays.Api.IntegrationTests/Collectors/Councils/{CouncilName}Tests.cs`
   - `.agent/playwright/out/{CouncilName}-screenshot.png`

**This marks the end of the one-shot implementation.** The collector is now ready for review and pull request creation.

---

## Important Guidelines

- **No browser emulation in collector**: The collector must replicate HTTP requests directly, not use Playwright/Selenium
- **Preserve if/else if pattern**: Don't refactor to other patterns
- **No try/catch for parsing**: Let exceptions propagate for easier debugging
- **Use existing utilities**: `ProcessingUtilities`, `Constants.UserAgent`, etc.
- **Follow the style guide**: Check `.gemini/styleguide.md` for conventions
- **Return only actual data**: Collectors must return ONLY the collection dates explicitly provided by the council website. Never calculate or infer additional dates based on intervals, patterns, or statements like "and every other week"
- **Include screenshot in PR**: The screenshot of the bin collections page taken during Phase 2.3 must be included in the pull request to assist with verification and validation

Begin now by parsing the issue content and navigating to the council website.
