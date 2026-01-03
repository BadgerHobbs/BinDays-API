# Task: Implement a New Bin Day Collector from GitHub Issue

You are an AI agent tasked with implementing a new UK council bin day collector for the BinDays API project. You will use Playwright MCP to navigate the council's website, capture network requests, and then implement a C# collector based on that data.

## Input

**Council Name (from issue title):** `$ISSUE_TITLE`

**GitHub Issue Content:**
```
$ISSUE_BODY
```

**Issue Number:** `$ISSUE_NUMBER`

---

## Phase 1: Parse Issue and Setup

### 1.1 Extract Information from Issue

The **Council Name** is provided in the issue title above.

Parse the GitHub issue body to extract:
- **GOV.UK URL**: Extract the council ID from the URL (e.g., `west-devon` from `https://www.gov.uk/rubbish-collection-day/west-devon`)
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

### 2.3 Close Browser and Save Data

1. Close the Playwright browser session (this saves the HAR file automatically)

2. Clean the HAR file to reduce context:
   ```bash
   node .agent/scripts/clean-har.js .agent/playwright/out/requests.har .agent/playwright/out/{CouncilName}.cleaned.har
   ```

3. Save collector info as JSON at `.agent/playwright/out/{CouncilName}.json`:
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
namespace BinDays.Api.IntegrationTests.Collectors.Councils
{
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
}
```

---

## Phase 4: Test and Debug

### 4.1 Run Tests

```bash
dotnet test --filter "FullyQualifiedName~{CouncilName}Tests.GetBinDaysTest" --logger "console;verbosity=detailed" BinDays.Api.IntegrationTests/BinDays.Api.IntegrationTests.csproj
```

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

When tests pass successfully:

1. Verify the output shows valid bin collection dates
2. Confirm multiple bin types are correctly identified
3. Report success with the files created:
   - `BinDays.Api.Collectors/Collectors/Councils/{CouncilName}.cs`
   - `BinDays.Api.IntegrationTests/Collectors/Councils/{CouncilName}Tests.cs`

---

## Important Guidelines

- **No browser emulation in collector**: The collector must replicate HTTP requests directly, not use Playwright/Selenium
- **Preserve if/else if pattern**: Don't refactor to other patterns
- **No try/catch for parsing**: Let exceptions propagate for easier debugging
- **Use existing utilities**: `ProcessingUtilities`, `Constants.UserAgent`, etc.
- **Follow the style guide**: Check `.gemini/styleguide.md` for conventions

Begin now by parsing the issue content and navigating to the council website.
