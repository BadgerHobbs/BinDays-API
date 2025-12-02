# Implementing a New Bin Day Collector

This document outlines two methods for implementing a new bin day collector for a council: running the process locally using the Gemini CLI, or automating it with a GitHub Action.

## Method 1: Local Implementation with Gemini CLI

This method allows you to run the collector implementation process on your local machine.

### Prerequisites

1.  **Gemini CLI**: Ensure you have the [Gemini CLI](https://github.com/google-gemini/gemini-cli) installed and configured.
2.  **Node.js**: The Playwright MCP server requires Node.js (version 18 or newer).
3.  **.NET SDK**: You need the .NET SDK (version 8.0 or newer) to build the project and run tests.
4.  **Repository**: Clone this repository to your local machine.

### Configuration

Before running the commands, you need to configure the Playwright MCP server for the Gemini CLI.

1.  Create a `settings.json` file in the `.gemini` directory at the root of the project (`.gemini/settings.json`).
2.  Add the following configuration to the file. This tells the Gemini CLI how to start the Playwright server.

    ```json
    {
      "mcpServers": {
        "playwright": {
          "command": "npx",
          "args": [
            "@playwright/mcp@latest",
            "--config",
            "./playwright-config.json"
          ]
        }
      },
      "context": {
        "fileFiltering": {
          "respectGitIgnore": false,
          "enableRecursiveFileSearch": true
        }
      }
    }
    ```

### Step-by-Step Guide

1.  **Start the Gemini CLI**

    Open your terminal and start the Gemini CLI in interactive mode. You can use the `-y` flag to automatically accept any confirmation prompts.

    ```bash
    gemini -y
    ```

2.  **Fetch Collector Data**

    Once inside the Gemini CLI, run the following command, replacing `<postcode>` with a valid postcode for the council you want to add:

    ```
    /fetch-collector-data <postcode>
    ```

    This command will:

    - Start the Playwright MCP server.
    - Use the browser to navigate the council's website and record the steps required to find the bin collection dates.
    - Save the recorded steps and the scraped bin collection data into a new JSON file: `.gemini/out/<CouncilName>.json`.
    - Save the network requests into a HAR file: `.gemini/out/<CouncilName>.cleaned.har`.

3.  **Implement the Collector**

    After the data fetching is complete, run the following command inside the same Gemini CLI session, using the file path from the previous step:

    ```
    /implement-collector .gemini/out/<CouncilName>.json
    ```

    This command will:

    - Read the provided JSON and HAR files.
    - Generate a new C# collector class in `BinDays.Api.Collectors/Collectors/Councils/`.
    - Generate a new integration test file in `BinDays.Api.IntegrationTests/Collectors/Councils/`.
    - Run the newly created integration test to verify the implementation.

You can then type `/exit` to close the Gemini CLI.

## Method 2: Automated Implementation with GitHub Actions

The `implement-collector.yml` workflow automates the entire process using a self-hosted runner.

### Prerequisites

1.  **Self-Hosted Runner**: You must have a self-hosted runner configured for your repository. The runner needs to have the following installed:
    - Node.js (version 18 or newer)
    - .NET SDK (version 8.0 or newer)
    - `jq` (for parsing JSON in the workflow)
    - `gh` CLI (for creating pull requests)
2.  **GitHub Secrets**: A repository secret named `GEMINI_API_KEY` must be created with your Gemini API key.

### How to Use the Workflow

1.  Navigate to the **Actions** tab in your GitHub repository.
2.  In the left sidebar, click on the **Implement New Collector** workflow.
3.  Click the **Run workflow** dropdown button.
4.  Enter the following inputs:
    - **Postcode**: The postcode for the council you want to add.
    - **Branch name**: The name for the new branch that will be created (e.g., `feat/add-council-name-collector`).
5.  Click the **Run workflow** button.

### What the Workflow Does

The workflow consists of two main jobs:

1.  **`fetch_data`**:

    - Sets up the runner environment.
    - Runs the `/fetch-collector-data` Gemini command using the `gemini-2.5-pro` model.
    - Uploads the generated `.json` and `.har` files as a workflow artifact.

2.  **`implement_collector`**:
    - Waits for the `fetch_data` job to complete.
    - Downloads the artifact.
    - Runs the `/implement-collector` Gemini command using the `gemini-2.5-pro` model.
    - Creates a new branch.
    - Commits the new collector and test files.
    - Opens a pull request with the changes.
