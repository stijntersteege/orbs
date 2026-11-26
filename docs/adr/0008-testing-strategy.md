# 0008. Testing strategy

- **Status**: Accepted
- **Date**: 2026-11-25

## Context

Contributors change code they did not write. Tests must catch regressions, document behaviour, run fast without external services, and protect the architecture.

## Decision

A test pyramid plus executable specifications:

| Level | Tool | Scope |
| --- | --- | --- |
| Domain unit | xUnit + Shouldly | Invariants of aggregates and value objects. |
| Application unit | xUnit + in-memory fakes | Use cases, prompt building, model resolution. Hand-written fakes instead of mocking libraries: clearer and closer to real behaviour. |
| Infrastructure unit | xUnit | Pure parts of adapters and connectors (stream parsing, URL building, HTML to text). |
| API integration | `WebApplicationFactory` + in-memory SQLite | The real HTTP pipeline, serialization, error mapping, streaming. |
| Specifications (BDD) | **Reqnroll** + Gherkin | User-visible behaviour in product language, run against the in-memory API. |
| Architecture | NetArchTest | The dependency rule and plugin visibility. |
| Frontend unit and component | Vitest + Testing Library | Logic (SSE reader, chat reducer) and components through roles and labels. |
| End to end | Playwright (desktop and phone) | Critical flows against the real container in CI. |

Test doubles for providers (`EchoModelAdapter` without delay, `FailingModelAdapter`) replace real models in all automated tests.

## Consequences

- The full backend suite runs in seconds on a laptop with no database or network.
- Gherkin features double as living documentation of what Orbs does.
- E2E tests are few and focused on flows that cross the whole stack.
