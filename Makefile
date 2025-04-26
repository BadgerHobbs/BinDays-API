# Versioning
COMMIT_HASH = $(shell git rev-parse --short HEAD)

.PHONY: version
version:
	@echo $(COMMIT_HASH)

all: build push

# Build Docker images
build: build-bindays-api

.PHONY: build-bindays-api
build-bindays-api:
	docker build -t bindays-api:${COMMIT_HASH} -f BinDays.Api/Dockerfile .
	docker tag bindays-api:$(COMMIT_HASH) bindays-api:latest

# Push Docker images
push: push-bindays-api

.PHONY: push-bindays-api
push-bindays-api:
	docker push bindays-api:${COMMIT_HASH}
	docker push bindays-api:latest