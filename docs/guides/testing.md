# Testing

Orbs uses a test pyramid plus executable specifications. The reasoning is in [ADR 0008](../adr/0008-testing-strategy.md).

## Run

```bash
cd backend && dotnet test Orbs.slnx               # all backend suites
cd backend && dotnet test tests/Orbs.Specs        # only the Gherkin specs
cd frontend && pnpm test                          # Vitest
cd frontend && pnpm test:coverage                 # with coverage
docker compose up -d && cd frontend && E2E_BASE_URL=http://localhost:8080 pnpm test:e2e
```

## Which test do I write?

| You changed | Write |
| --- | --- |
| A rule in an entity | a unit test in `Orbs.Domain.Tests` |
| A use case | a test in `Orbs.Application.Tests` using `InMemoryStore` and the fakes in `Fakes/` |
| Adapter parsing or mapping | a unit test in `Orbs.Infrastructure.Tests` |
| An endpoint or its error mapping | a test in `Orbs.Api.IntegrationTests` with `OrbsApiFactory` and `ApiClient` |
| Behaviour a person would notice | a scenario in `Orbs.Specs/Features` |
| A component or hook | a Vitest test next to it (`*.test.tsx`) |
| A critical end-to-end flow | a Playwright test in `frontend/e2e` |

## Writing a specification

Features use product language, never HTTP details:

```gherkin
Scenario: An orb only sees the sources it is given
  Given a note "Team" saying "Sam leads design."
  And a note "Recipes" saying "Frittata takes 25 minutes."
  When I create an orb named "Pip" that knows "Team"
  Then "Pip" knows "Team"
  And "Pip" does not know "Recipes"
```

Steps live in `Orbs.Specs/Steps`, grouped by topic. Each scenario gets a fresh in-memory server through `SpecContext`, which Reqnroll injects and disposes. Reuse existing steps before adding new ones; the Reqnroll extension for your editor shows which steps exist.

## Test doubles

- `EchoModelAdapter` with `WordDelay = 0` answers instantly with "You said: ...".
- `FailingModelAdapter` (kind `failing`) streams one fragment and then throws.
- `OrbsApiFactory` hosts the real API on a private in-memory SQLite database; set `ApiToken` or `SeedDemoData` to test those paths.

## Conventions

- Test names describe behaviour: `A_failing_provider_ends_with_an_error_and_stores_no_reply`.
- Arrange, act, assert, separated by blank lines.
- Frontend tests query by role and label, as people and assistive technology do.
