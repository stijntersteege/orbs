# 8. Crosscutting concepts

## Domain model and validation

- Aggregates (such as `Orb`, `ContextSource`, `Conversation`, `Schedule`) expose behaviour (`Rename`, `LinkContext`, `MarkSynced`, `AddUserMessage`, `Attach`, `Invite`) instead of setters.
- `Guard` helpers keep invariants at the top of each method. Text is trimmed and length-limited; URLs must be absolute http(s); kinds must be slugs.
- Time is passed in (`DateTimeOffset now`); the application uses `TimeProvider`, so tests control the clock.

## Errors

| Exception | HTTP | Meaning |
| --- | --- | --- |
| `DomainException` | 400 | The request breaks a business rule. |
| `UnauthorizedException` | 401 | Not signed in, or the sign-in failed. |
| `ForbiddenException` | 403 | Signed in, but not allowed (for example, admin-only). |
| `NotFoundException` | 404 | Unknown id, or one that belongs to someone else. |
| `ConflictException` | 409 | The change clashes with existing data, such as an email that is taken. |
| `ModelUnavailableException` | 409 | The orb has no usable model yet. |
| `ExternalServiceException` | 502 | A provider or source failed. |
| rate limit | 429 | Too many sign-in attempts or webhook calls; `Retry-After` says when to try again. |

All errors are RFC 9457 problem details. The client shows `detail` to people, so messages are written for humans.

## Plugins

- Interfaces live in `Orbs.Application/Abstractions/Plugins.cs` (models, context, tools), with integrations and channels next to their use cases.
- Implementations are registered in `Orbs.Infrastructure/DependencyInjection.cs` (built-in tools that only use application services are registered in `Orbs.Application`).
- `PluginRegistry<T>` maps `Kind` to implementation and rejects duplicates at startup.
- The UI asks `/kinds` endpoints (providers, context, integrations, channels) and `/api/tools`, so new kinds appear without client changes. Friendly presets (OpenRouter, Ollama, ...) are client data on top of generic kinds.
- Tools carry a `Risk` (read or write). Write tools ask first unless the person allows them per orb; tools that only touch the orb's own visible state (memory, skills, asking another orb) are allowed by default and can be set to ask.

## Prompting

`PromptBuilder` is pure: orb identity and description, the brief, memories, then "What you know" with each linked or attached source (or the passages retrieval picked), sharing a character budget fairly; in a group chat it names the other orbs and labels their replies; then the recent messages. Tool definitions travel with the request for adapters that support tools.

## Security

- **Accounts**: passwords (PBKDF2-SHA512, 210,000 iterations), passkeys (WebAuthn) and OpenID Connect. Sessions are random tokens stored only as SHA-256 hashes; browsers get them as an `HttpOnly`, `SameSite=Lax` cookie that is `Secure` on https. The shared API token is compared in constant time and acts as the first admin.
- **Isolation**: every owned aggregate has an `OwnerId` and EF Core filters every query to the signed-in person; someone else's id answers 404. Background jobs run as their owner.
- **Secrets**: `ISecretProtector` (Data Protection, purpose `Orbs.Secrets.v1`) encrypts API keys, tokens and passwords for providers, apps, channels and MCP servers. They are never returned; a blank field on update keeps the saved one. The key ring lives in the database, so backups include it.
- **Server-side requests**: web pages, feeds, push endpoints, and providers, apps, channels and MCP servers added by people who are not admins go through `SafeHttp`, which refuses private, loopback and link-local addresses after DNS resolution.
- **Browser**: a strict Content Security Policy (no inline or third-party scripts), `X-Frame-Options: DENY`, `nosniff`, `Referrer-Policy: no-referrer` and a Permissions-Policy that only allows the microphone. Replies render Markdown without raw HTML, and pictures in replies stay links. Uploaded files that could run in a browser download as plain bytes.
- **Abuse**: sign-in, setup, passkey sign-in and password changes are rate limited per address, as are webhooks; webhook secrets are compared in constant time and bodies are capped.
- **Acting on your behalf**: tools that change things ask first; the audit trail records every call. Local (stdio) MCP servers are off unless an admin enables them.
- **Least privilege**: orbs only see linked or attached sources; providers only get what the orb sends; the container runs as non-root.

## Persistence

- One `DbContext`, which is also the `IUnitOfWork`. Repositories per aggregate.
- Enums are stored as strings; appearance as a complex type (columns on `orbs`); linked source ids as a primitive collection (array on PostgreSQL, JSON on SQLite).
- SQLite stores `DateTimeOffset` as a sortable number (converter applied only for SQLite).
- Deleting an orb cascades to its conversations; deleting a source unlinks it from orbs; deleting a provider puts pinned orbs back on Auto; deleting an account deletes everything it owns.
- Every schema change ships a migration for both PostgreSQL and SQLite; new required columns get a default so existing databases upgrade.

## API conventions

- Resource-oriented REST under `/api`, JSON in camelCase, enums as camelCase strings.
- `201 Created` with `Location` for creates, `204` for deletes.
- Streaming endpoints answer `text/event-stream` with events `start`, `delta`, `tool_call`, `tool_result`, `approval_required`, `done` and `error`.
- The OpenAPI document is generated from the endpoints; keep summaries meaningful.

## UI and accessibility

- Design tokens (CSS custom properties) for both themes; orb colors are theme independent.
- Real controls (`button`, `label`, `input`), visible focus rings, 44 px targets, `aria-live` for streaming replies, reduced motion respected.
- Server state through TanStack Query hooks only; device preferences in local storage.

## Observability

Structured logging through `Microsoft.Extensions.Logging`, `/health` with a database check, and EF Core command logging at warning level by default. OpenTelemetry export is a natural next step.
