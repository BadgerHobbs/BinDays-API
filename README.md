# BinDays-API

[![Integration Tests](https://github.com/BadgerHobbs/BinDays-API/actions/workflows/integration-tests.yml/badge.svg)](https://github.com/BadgerHobbs/BinDays-API/actions/workflows/integration-tests.yml) [![Build and Push Image](https://github.com/BadgerHobbs/BinDays-API/actions/workflows/build-and-push-image.yml/badge.svg)](https://github.com/BadgerHobbs/BinDays-API/actions/workflows/build-and-push-image.yml) [![License: AGPL-3.0](https://img.shields.io/badge/License-AGPL_3.0-blue.svg)](LICENSE)

![d2(13)](https://github.com/user-attachments/assets/cfaef323-41fe-4211-9174-7694bd8ea992)

<p align="center">
  <a href="https://github.com/BadgerHobbs/BinDays-App">BinDays-App</a> •
  <a href="https://github.com/BadgerHobbs/BinDays-Client">BinDays-Client</a> •
  <a href="https://github.com/BadgerHobbs/BinDays-API">BinDays-API</a> •
  <a href="https://github.com/BadgerHobbs/BinDays-HomeAssistant">BinDays-HomeAssistant</a>
</p>

> **Have a question or a problem?** Check the [**Frequently Asked Questions**](FAQS.md) before opening an issue.

## Overview

BinDays-API is the server-side component for the BinDays project. It provides the logic and configuration for retrieving bin collection schedules from UK councils.

It works with a client application ([BinDays-App](https://github.com/BadgerHobbs/BinDays-App), [BinDays-Client](https://github.com/BadgerHobbs/BinDays-Client)) to fetch data.

### How It Works

The API doesn't scrape councils directly. Instead, it generates request configurations (URLs, headers, payloads) and sends them to a client. The client executes the HTTP request and sends the raw response back to the API for processing.

This approach has a few benefits:

- **Avoids IP Blocking:** Council requests come from the user's IP, not a server, which avoids blocks, rate-limiting, and CAPTCHAs.
- **Dynamic Updates:** Collector logic can be updated server-side without requiring a client app update.
- **Stateless & Lightweight:** The API is stateless with no external dependencies.

## Deployment

The API is containerized for deployment with Docker.

### Run the Public Image

Deploy the pre-built image from the GitHub Container Registry:

```bash
docker run -d \
    --name bindays-api \
    -p 8080:8080 \
    ghcr.io/badgerhobbs/bindays-api:latest
```

### Build from Source

Build the Docker image locally:

```bash
docker build -t bindays-api:latest .
```

## Contributing

To add a council, fix a bug, or improve the project, see the [**Contributing Guidelines**](CONTRIBUTING.md). For questions, check the [**FAQs**](FAQS.md).

## License

This project is licensed under the [AGPL-3.0 License](LICENSE).

## Support

If you find this project helpful, please consider supporting its development.

[![Buy Me A Coffee](https://img.buymeacoffee.com/button-api/?text=Buy%20me%20a%20coffee&emoji=&slug=badgerhobbs&button_colour=FFDD00&font_colour=000000&font_family=Poppins&outline_colour=000000&coffee_colour=ffffff)](https://www.buymeacoffee.com/badgerhobbs)
