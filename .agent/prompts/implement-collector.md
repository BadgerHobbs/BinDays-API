# Task: Implement a New Bin Day Collector from GitHub Issue

You are an AI agent tasked with implementing a new UK council bin day collector for the BinDays API project. You will use Playwright MCP to navigate the council's website, capture network requests, and then implement a C# collector based on that data.

**This is a one-shot, end-to-end implementation.** You must complete all phases autonomously from start to finish without requiring additional user input. This includes:

- Parsing the issue and extracting council information
- Using Playwright to navigate the website and capture requests
- **Reading and following the complete style guide (provided in Phase 3.1 below)**
- Implementing the collector class in C# with **strict style guide compliance**
- Creating and running integration tests
- Debugging and fixing any issues until tests pass
- **Verifying style guide compliance before reporting completion**
- Reporting completion when all tests pass successfully AND code follows all style guide conventions

Do not stop or ask for approval between phases - execute the entire workflow in one continuous operation.

**CRITICAL: The complete style guide is embedded in this prompt at Phase 3.1. You must read and follow it precisely. Non-compliant code is not acceptable.**

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

### 3.1 Read the Complete Style Guide

**CRITICAL: The full style guide has been provided below. Read it completely before writing any code.**

---

$STYLE_GUIDE

---

**You must follow EVERY convention documented in the style guide above. Non-compliant code will be rejected.**

### 3.2 Study Existing Patterns

Before writing code:

1. **Study 2-3 existing collectors** in `BinDays.Api.Collectors/Collectors/Councils/`
   - Look for patterns that match your use case (JSON parsing, HTML regex, multi-step flows)
   - Use them as references for implementation details

2. **Review base classes** in `BinDays.Api.Collectors/Collectors/Vendors/`
   - Understand which base class to use and how to inherit from it

3. **Analyze the cleaned HAR file** to understand the HTTP request flow

### 3.3 Choose Base Class

Determine the appropriate base class:

- `GovUkCollectorBase` - Most common, for custom implementations
- `FccCollectorBase` - For councils using FCC Environment services
- `ITouchVisionCollectorBase` - For councils using iTouchVision
- `BinzoneCollectorBase` - For councils using Binzone

If unsure, use `GovUkCollectorBase`.

### 3.4 Implement Collector Class

Create `BinDays.Api.Collectors/Collectors/Councils/{CouncilName}.cs`:

**YOU MUST STRICTLY FOLLOW ALL CONVENTIONS FROM THE STYLE GUIDE PROVIDED ABOVE.**

Refer back to the style guide sections for:
- Class structure and modifiers
- Property and method syntax
- Member ordering
- Collection and object initialization patterns
- RequestId handling patterns
- State management with Metadata
- Date parsing and data cleaning
- Regex usage
- Utility method usage

**If you are unsure about ANY convention, refer back to the style guide before proceeding.**

### 3.5 Create Integration Test

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

### 5.1 Final Style Guide Compliance Check

Before reporting completion, perform a final review:

1. **Re-read the style guide in Phase 3.1** and verify your code matches ALL conventions
2. **Compare against existing collectors** - your code should look stylistically identical
3. **Verify all key requirements from the style guide are met** (class structure, property syntax, member ordering, collection expressions, object initialization, RequestId pattern, date parsing, utility usage, etc.)

**If any style guide violation is found, fix it before proceeding.**

### 5.2 Report Success

When tests pass AND style guide compliance is verified:

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

### HIGHEST PRIORITY: Style Guide Compliance

**THE COMPLETE STYLE GUIDE HAS BEEN PROVIDED IN PHASE 3.1 ABOVE. IT IS THE SINGLE SOURCE OF TRUTH FOR ALL CODE CONVENTIONS.**

- Read the ENTIRE style guide in Phase 3.1 before writing any code
- Follow EVERY convention documented without exception
- Non-compliance will result in rejected code
- When in doubt about ANY implementation detail, refer back to the style guide in Phase 3.1

### Critical Implementation Rules

- **No browser emulation in collector**: The collector must replicate HTTP requests directly, not use Playwright/Selenium
- **Return only actual data**: Collectors must return ONLY the collection dates explicitly provided by the council website. Never calculate or infer additional dates based on intervals, patterns, or statements like "and every other week"
- **Include screenshot in PR**: The screenshot of the bin collections page taken during Phase 2.3 must be included in the pull request to assist with verification and validation

All other implementation rules are documented in the style guide provided in Phase 3.1.

Begin now by parsing the issue content and navigating to the council website.
