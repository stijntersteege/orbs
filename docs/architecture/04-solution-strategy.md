# 4. Solution strategy

| Goal | Approach |
| --- | --- |
| Extensibility | **Ports and adapters.** The application layer defines `IModelAdapter` and `IContextConnector`. Each provider or source kind is one class, registered in the container and found by its `Kind` key through a `PluginRegistry`. Data stores the kind as a string, so a new kind needs no schema or domain change. |
| Maintainability | **Clean Architecture** with four projects and the dependency rule enforced by architecture tests. **Rich domain model**: entities guard their invariants. **Small use-case services** per feature instead of a generic mediator. KISS over cleverness. |
| Privacy and security | Self-hosted by design. API keys encrypted with ASP.NET Core Data Protection, keys stored in the database, never serialized to clients. Orbs only send linked context to models. Optional API token. Non-root container. |
| Usability | One React codebase for web and mobile, responsive layout, design tokens for light and dark themes, accessible components, streaming replies so the orb feels alive. |
| Operability | One OCI image serves API and web app. SQLite for zero setup; PostgreSQL with migrations for production. Health endpoint, OpenAPI document, structured logs. |
| Testability | Pure functions for prompt building and model resolution; in-memory fakes for ports; the real API hosted in memory for integration tests and executable Gherkin specifications. |

## Technology choices

| Area | Choice | Why (details in the ADRs) |
| --- | --- | --- |
| Backend | .NET 10, ASP.NET Core Minimal APIs | LTS, fast, first-class OpenAPI and SSE, strong typing, great test tooling. [ADR 0002](../adr/0002-dotnet-clean-architecture-backend.md) |
| Persistence | EF Core; PostgreSQL or SQLite | One model, two databases: zero-setup and production-grade. [ADR 0004](../adr/0004-postgresql-and-sqlite-with-ef-core.md) |
| Streaming | Server-sent events | Simple, HTTP-native, one-way is all a reply needs. [ADR 0006](../adr/0006-server-sent-events-for-streaming.md) |
| Client | React, TypeScript, Vite, TanStack Query, Capacitor | Huge ecosystem, one codebase for web, PWA and native. [ADR 0003](../adr/0003-react-vite-capacitor-client.md) |
| Tests | xUnit, Shouldly, Reqnroll, NetArchTest, Vitest, Playwright | [ADR 0008](../adr/0008-testing-strategy.md) |
| Delivery | Docker, Compose, GitHub Actions, GHCR | [ADR 0009](../adr/0009-single-container-delivery.md) |
