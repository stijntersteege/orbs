# 8. Crosscutting concepts

## Domain model and validation

- Aggregates (`Orb`, `ModelProvider`, `ContextSource`, `Conversation`) expose behaviour (`Rename`, `LinkContext`, `MarkSynced`, `AddUserMessage`) instead of setters.
- `Guard` helpers keep invariants at the top of each method. Text is trimmed and length-limited; URLs must be absolute http(s); kinds must be slugs.
- Time is passed in (`DateTimeOffset now`); the application uses `TimeProvider`, so tests control the clock.

## Errors

| Exception | HTTP | Meaning |
| --- | --- | --- |
| `DomainException` | 400 | The request breaks a business rule. |
| `NotFoundException` | 404 | Unknown id. |
| `ModelUnavailableException` | 409 | The orb has no usable model yet. |
| `ExternalServiceException` | 502 | A provider or source failed. |

All errors are RFC 9457 problem details. The client shows `detail` to people, so messages are written for humans.

## Plugins (providers and connectors)

- Interfaces live in `Orbs.Application/Abstractions/Plugins.cs`.
- Implementations are `internal` (enforced by an architecture test) and registered in `Orbs.Infrastructure/DependencyInjection.cs`.
- `PluginRegistry<T>` maps `Kind` to implementation and rejects duplicates at startup.
- The UI asks `/api/providers/kinds` and `/api/context-sources/kinds`, so new kinds appear without client changes. Friendly presets (OpenRouter, Ollama, ...) are client data on top of generic kinds.

## Prompting

`PromptBuilder` is pure: orb identity and description, the brief, then "What you know" with each linked, ready source, sharing a character budget fairly; then the last 40 messages. A retrieval-based builder can replace it behind the same call.

## Security

- **Secrets**: `ISecretProtector` (Data Protection, purpose `Orbs.Secrets.v1`). Keys ring persisted in the database (`DataProtectionKeys`), so backups include it.
- **API access**: optional shared bearer token, compared in constant time; CORS only for configured origins.
- **Least privilege**: orbs only see linked sources; providers only get what the orb sends; the container runs as non-root.

## Persistence

- One `DbContext`, which is also the `IUnitOfWork`. Repositories per aggregate.
- Enums are stored as strings; appearance as a complex type (columns on `orbs`); linked source ids as a primitive collection (array on PostgreSQL, JSON on SQLite).
- SQLite stores `DateTimeOffset` as a sortable number (converter applied only for SQLite).
- Deleting an orb cascades to its conversations; deleting a source unlinks it from orbs; deleting a provider puts pinned orbs back on Auto.

## API conventions

- Resource-oriented REST under `/api`, JSON in camelCase, enums as camelCase strings.
- `201 Created` with `Location` for creates, `204` for deletes.
- Streaming endpoints answer `text/event-stream` with events `start`, `delta`, `done`, `error`.
- The OpenAPI document is generated from the endpoints; keep summaries meaningful.

## UI and accessibility

- Design tokens (CSS custom properties) for both themes; orb colors are theme independent.
- Real controls (`button`, `label`, `input`), visible focus rings, 44 px targets, `aria-live` for streaming replies, reduced motion respected.
- Server state through TanStack Query hooks only; device preferences in local storage.

## Observability

Structured logging through `Microsoft.Extensions.Logging`, `/health` with a database check, and EF Core command logging at warning level by default. OpenTelemetry export is a natural next step.
