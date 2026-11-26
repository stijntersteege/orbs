# 0002. .NET backend with Clean Architecture and Minimal APIs

- **Status**: Accepted
- **Date**: 2026-11-25

## Context

The backend stores orbs, talks to many model providers, streams replies and must stay easy to extend and test for years. It runs on small self-hosted machines.

## Options

1. **Node.js (TypeScript) with Fastify/NestJS**: one language across the stack; weaker compile-time guarantees, more runtime dependencies.
2. **Python (FastAPI)**: closest to the AI ecosystem; dynamic typing and packaging make long-term maintenance harder for a public project.
3. **.NET 10 (ASP.NET Core)**: LTS, fast and lean, strong typing and analyzers, built-in OpenAPI and server-sent events, mature EF Core, excellent test tooling including Reqnroll for BDD.

For the internal structure:

- a single project (simple, but boundaries erode),
- **Clean Architecture** in four projects (clear dependency rule, testable core),
- vertical slices with a mediator library (flexible, but more ceremony and a licensed dependency in some cases).

## Decision

.NET 10 with ASP.NET Core **Minimal APIs**, structured as **Clean Architecture**: `Domain`, `Application`, `Infrastructure`, `Api`. Use cases are small services per feature, called directly from endpoints, without a mediator. The dependency rule is enforced by architecture tests.

## Consequences

- The domain is plain C# and trivially unit-testable; infrastructure can change without touching rules.
- Contributors need to know where code goes; the README, CONTRIBUTING and arc42 section 5 explain it.
- Minimal APIs keep endpoints short; if an endpoint grows logic, that logic belongs in a use case.
- Two languages in the repository (C# and TypeScript). The API contract (OpenAPI) is the boundary.
