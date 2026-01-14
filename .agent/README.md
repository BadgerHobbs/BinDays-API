# Agent Automation

This directory contains resources for AI-powered automation of collector implementations using Codex CLI.

## Overview

The automation workflow allows new bin collection councils to be implemented automatically from GitHub issues. When a maintainer comments `/implement` on an issue with the "New Collector" label, the workflow:

1. Parses the council information from the issue
2. Uses Playwright MCP to navigate the council's website and capture HTTP requests as HAR
3. Uses Codex CLI to analyze the HAR and implement a new collector
4. Runs integration tests and fixes issues until passing
5. Creates a pull request with the implementation

## Directory Structure

```
.agent/
├── README.md                    # This file
├── playwright/
│   ├── config.template.json     # Template for Playwright MCP configuration
│   └── out/                     # Output directory for HAR files (gitignored)
├── prompts/
│   └── implement-collector.md   # Single prompt for full collector implementation
└── scripts/
    └── clean-har.js             # Script to clean HAR files for reduced context
```

**Note:** Debugging documentation is in `DEBUGGING.md` at the repository root.

## Required GitHub Secrets

The workflow requires the following secrets to be configured in the repository:

| Secret                       | Description                                                                                                                                                                                                                                                                                                                                     |
| ---------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `AZURE_OPENAI_API_KEY`       | Azure OpenAI API key for Codex CLI                                                                                                                                                                                                                                                                                                              |
| `CODEX_CONFIG`               | Codex TOML configuration                                                                                                                                                                                                                                                                                                                        |
| `GH_PAT_IMPLEMENT_COLLECTOR` | **Personal Access Token (Fine-grained)** used to create Pull Requests.<br>Required to trigger the "Integration Tests" workflow which does not run when PRs are created by the default `GITHUB_TOKEN`.<br><br>**Required Repository Permissions:**<br>- `Pull requests`: Read and write<br>- `Issues`: Read and write<br>- `Contents`: Read-only |

### Example CODEX_CONFIG

```toml
model_provider = "azure-foundry"
model_reasoning_effort = "high"
model_reasoning_sumary = "detailed"

model = "gpt-5.1-codex"

[model_providers.azure-foundry]
name = "Azure Foundry"
base_url = "https://your-resource.cognitiveservices.azure.com/openai"
env_key = "AZURE_OPENAI_API_KEY"
query_params = { api-version = "2025-04-01-preview" }
wire_api = "responses"
```

## Playwright MCP Configuration

The workflow uses Playwright MCP with the following configuration:

- **Headless mode**: Runs without a display (required for CI)
- **HAR recording**: Captures all network requests for analysis
- **Trace saving**: Saves Playwright traces for debugging failed runs

Traces can be viewed at [trace.playwright.dev](https://trace.playwright.dev/) by uploading the `.zip` files from the workflow artifacts.

## Usage

### Triggering the Workflow

1. Create or find an issue with the "New Collector" label
2. Ensure the issue title is the council name (e.g. "West Devon Borough Council")
3. Ensure the issue follows the council-request template with all required fields filled
4. Comment `/implement` on the issue
5. The workflow will run and create a PR if successful

### Required Issue Fields

The issue must contain:

- **GOV.UK ID**: URL from gov.uk/rubbish-collection-day (e.g. `https://www.gov.uk/rubbish-collection-day/west-devon`)
- **Council Name**: Full name of the council
- **Bin Collection Page**: Direct link to the council's bin collection lookup page
- **Example Postcode**: A valid postcode in the council's area

### Manual Debugging

If the automated implementation fails, you can debug manually:

1. Download the `collector-data` artifact from the failed workflow run
2. Review the `.json` and `.har` files
3. Enable HTTP logging and run tests locally:
   ```bash
   export BINDAYS_ENABLE_HTTP_LOGGING=true
   dotnet test --filter "FullyQualifiedName~CouncilNameTests"
   ```

## Prompts

### implement-collector.md

This is a single unified prompt that takes the GitHub issue body as input and instructs Codex to:

1. **Parse the issue** to extract council information (name, postcode, URLs, etc.)
2. **Navigate the council website** using Playwright MCP to capture network requests as HAR
3. **Scrape bin collection data** from the results page
4. **Clean the HAR file** to reduce context size
5. **Analyze existing collectors** to understand project patterns
6. **Implement the collector class** following project conventions
7. **Create integration tests** with the example postcode
8. **Run and debug tests** until passing

## Scripts

### clean-har.js

Cleans HAR files to reduce context size by:

- Removing static assets (images, CSS, fonts, JavaScript)
- Removing analytics/tracking requests
- Stripping unnecessary headers
- Removing timing information

Usage:

```bash
node .agent/scripts/clean-har.js input.har output.cleaned.har
```

## Extending

### Adding New Vendor Base Classes

If you encounter a council using a common third-party vendor system, consider:

1. Analyzing multiple councils using the same vendor
2. Creating a new base class in `BinDays.Api.Collectors/Collectors/Vendors/`
3. Updating the `implement-collector.md` prompt to reference the new base class

### Customizing Prompts

The prompts can be modified to handle specific edge cases or improve success rates. After modifying prompts, test them locally with Codex CLI before relying on the automated workflow.
