# Gemini CLI Config

The documentation outlines how to configure Gemini CLI to automatically implement a new collector/council for the BinDays API.

### Configuration

#### Install Node.JS

Install [Node.js](https://nodejs.org/en/download/) as per the documentation for your respective operating system.

#### Install .NET 8 SDK

Install the [Microsoft .NET 8 SDK](https://learn.microsoft.com/en-us/dotnet/core/install/) as per the documentation for your respective operating system.

#### Install Playwright MCP

Install the [Playwright MCP](https://github.com/microsoft/playwright-mcp) as per the documentation, or using the following commands.

```bash
npm install -g @playwright/mcp@latest
npx playwright install-deps
npx playwright install firefox
```

#### Install Gemini CLI

Install the [Gemini CLI](https://github.com/google-gemini/gemini-cli) as per the documentation, or using the following command.

```bash
npm install -g @google/gemini-cli@latest
```

### Usage

#### Fetching Collector Data

##### Run Gemini CLI

Use the following command to run the Gemini CLI. It is reccomended to run with the additional flags to both automatically accept confirmation prompts as well as use an appropriate model.

```bash
gemini -y --model gemini-3-flash-preview
```

##### Run Custom Command

Inside the Gemini CLI, run the following command to fetch the collector network data using Playwright MCP server. Replace `<Postcode>` with a valid postocde for the collector you want to add.

```bash
/fetch-collector-data <Postcode>
```

This command will:

- Start the Playwright MCP server.
- Use the browser to navigate the collector's website and record the steps required to find the bin collection dates.
- Save the recorded steps and the scraped bin collection data into a new JSON file: `.agent/playwright/out/<CollectorName>.json`.
- Save the network requests into a HAR file: `.agent/playwright/out/<CollectorName>.cleaned.har`.

#### Implement Collector

##### Run Gemini CLI

Use the following command to run the Gemini CLI. It is reccomended to run with the additional flags to both automatically accept confirmation prompts as well as use an appropriate model.

```bash
gemini -y --model gemini-3-pro-preview
```

##### Run Custom Command

Inside the Gemini CLI, run the following command to implement the collector using the network data previously retrieved. Existing collector implementations and documentation are used as context/reference to support this.

```bash
/implement-collector <CollectorName>
```

This command will:

- Read the provided JSON and HAR files.
- Generate a new C# collector class in `BinDays.Api.Collectors/Collectors/Councils/`.
- Generate a new integration test file in `BinDays.Api.IntegrationTests/Collectors/Councils/`.
- Run the newly created integration test to verify the implementation.
