# Shortcuts for common tasks. Every target is a plain command you can also run by hand.
.PHONY: help setup dev-api dev-web test test-backend test-frontend lint format build docker-up docker-down migration

help: ## List targets
	@grep -E '^[a-zA-Z_-]+:.*?## ' $(MAKEFILE_LIST) | awk 'BEGIN {FS = ":.*?## "}; {printf "  %-15s %s\n", $$1, $$2}'

setup: ## Install tools and dependencies
	cd backend && dotnet tool restore && dotnet restore
	cd frontend && pnpm install

dev-api: ## Run the API on http://localhost:5080 (SQLite, demo data)
	dotnet run --project backend/src/Orbs.Api --launch-profile http

dev-web: ## Run the web client on http://localhost:5173 (proxies /api)
	cd frontend && pnpm dev

test: test-backend test-frontend ## Run every test suite

test-backend: ## Unit, integration, architecture and Reqnroll specs
	cd backend && dotnet test Orbs.slnx

test-frontend: ## Vitest unit and component tests
	cd frontend && pnpm test

lint: ## Static checks for both halves
	cd backend && dotnet format Orbs.slnx --verify-no-changes
	cd frontend && pnpm lint && pnpm typecheck && pnpm format:check

format: ## Fix formatting
	cd backend && dotnet format Orbs.slnx
	cd frontend && pnpm format

build: ## Build the container image
	docker build -t orbs:local .

docker-up: ## Start Orbs and PostgreSQL on http://localhost:8080
	docker compose up -d --build

docker-down: ## Stop the stack (data is kept in a volume)
	docker compose down

migration: ## Add an EF Core migration: make migration name=AddSomething
	cd backend && dotnet ef migrations add $(name) --project src/Orbs.Infrastructure --output-dir Persistence/Migrations
