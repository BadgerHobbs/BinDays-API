# BinDays-API

[![Integration Tests](https://github.com/BadgerHobbs/BinDays-API/actions/workflows/integration-tests.yml/badge.svg)](https://github.com/BadgerHobbs/BinDays-API/actions/workflows/integration-tests.yml) [![Build and Push Image](https://github.com/BadgerHobbs/BinDays-API/actions/workflows/build-and-push-image.yml/badge.svg)](https://github.com/BadgerHobbs/BinDays-API/actions/workflows/build-and-push-image.yml) [![Deploy to DigitalOcean](https://github.com/BadgerHobbs/BinDays-API/actions/workflows/deploy-to-digital-ocean.yml/badge.svg)](https://github.com/BadgerHobbs/BinDays-API/actions/workflows/deploy-to-digital-ocean.yml)

API for BinDays mobile app designed to provide configuration for and process responses from client-side requests.

Run via CLI at address [http://localhost:5042](http://localhost:5042)

```bash
dotnet run --project BinDays.Api\BinDays.Api.csproj
```

Docker build

```bash
docker build -t bindays-api -f ./BinDays.Api/Dockerfile .
```

Docker run

```bash
docker run -d \
    --name bindays-api \
    -p 9976:8080 \
    --restart on-failure \
    bindays-api
```
