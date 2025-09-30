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

Adding new councils is the most common contribution. Before you start, please familiarize yourself with the project's coding conventions and design philosophy by reading our comprehensive **[C# Style Guide](/.gemini/styleguide.md)**.

The style guide covers everything you need to know, including:

-   **Key Principles:** The core concepts behind how collectors work.
-   **File Structure:** Where to place your new collector and test files.
-   **Implementation Details:** Guidance on Naming Conventions, Collector Implementation, and our Design Philosophy.
-   **Code Examples:** Templates for both collector and integration test classes.
-   **Commit Guidelines:** How to format your commit messages.

### Quick Workflow

1.  **Prerequisites:** Ensure you have the .NET SDK and an IDE installed.
2.  **Reverse-Engineer:** Use your browser's developer tools to analyze the network traffic for the council's bin day lookup service.
3.  **Implement:** Create your collector and integration test classes, following the patterns in the **[C# Style Guide](/.gemini/styleguide.md)**.
4.  **Test:** Run your new integration test to verify that the collector works correctly.
    ```bash
    dotnet test --filter "Name~MyNewCouncil"
    ```
