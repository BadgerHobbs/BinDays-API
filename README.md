# BinDays-API

[![Integration Tests](https://github.com/BadgerHobbs/BinDays-API/actions/workflows/integration-tests.yml/badge.svg)](https://github.com/BadgerHobbs/BinDays-API/actions/workflows/integration-tests.yml) [![Build and Push Image](https://github.com/BadgerHobbs/BinDays-API/actions/workflows/build-and-push-image.yml/badge.svg)](https://github.com/BadgerHobbs/BinDays-API/actions/workflows/build-and-push-image.yml) [![License: AGPL-3.0](https://img.shields.io/badge/License-AGPL_3.0-blue.svg)](LICENSE)

![d2](https://github.com/user-attachments/assets/bf4dde68-eb07-4470-a713-fd878f941956)

<p align="center">
  <a href="https://github.com/BadgerHobbs/BinDays-App">BinDays-App</a> •
  <a href="https://github.com/BadgerHobbs/BinDays-Client">BinDays-Client</a> •
  <a href="https://github.com/BadgerHobbs/BinDays-API">BinDays-API</a>
</p>

## Welcome!

Have questions? Want to add a council, or report an issue? Check the [FAQs](FAQS.md).

## Overview

You are currently viewing the repository for the BinDays-API, with is the server-side component of the BinDays project that provides both the requests that the clients must make plus the processing of their responses.

### High-Level Design

At a high-level, all the BinDays-API does is enable requests for bin collection councils to be configured and processed server-side, while executed client-side.

The main advantages of this approaches are as follows:

- New councils can be added and existing councils can be updated without requiring changes client-side, such as in the BinDays app.
- Avoids rate-limiting, captchas, and IP blocking often caused by making many requests from a single source on a non-residential IP address.

### Low-Level Design

At a low-level, the BinDays-API is structured around five core requests/methods which are used across all collector implementations.

| Request                | Description                                                                                   |
| ---------------------- | --------------------------------------------------------------------------------------------- |
| `ClientSideRequest`    | A request to be made by the client.                                                           |
| `ClientSideResponse`   | A response returned by the client.                                                            |
| `GetCollectorResponse` | A response containing the next request to be made (if required) and the collector (if found). |
| `GetAddressesResponse` | A response containing the next request to be made (if required) and the addresses (if found). |
| `GetBinDaysResponse`   | A response containing the next request to be made (if required) and the bin days (if found).  |

For the above requests/responses, each collector implements `ICollector` and `GovUkCollectorBase` which contain three main methods.

| Method         | Description                                                                                                                                       |
| -------------- | ------------------------------------------------------------------------------------------------------------------------------------------------- |
| `GetCollector` | Takes in the postcode and optional client-side response. It returns either the collector or the next request to make depending on internal logic. |
| `GetAddresses` | Takes in the postcode and optional client-side response. It returns either the addresses or the next request to make depending on internal logic. |
| `GetBinDays`   | Takes in the address and optional client-side response. It returns either the bin days or the next request to make depending on internal logic.   |

While it is generally standard that the client makes two requests to each endpoint, one for the initial next request and the other to send the response for processing, for some collectors this can be more if they require data such as cookies or tokens.

## Contributing

If you would like to contribute to the development of the BinDays-API, read the [contribution guidelines](CONTRIBUTING.md), then fork the repository and submit a pull request.

## License

The code and documentation in this project are released under the [AGPL-3.0 License](LICENSE).
