# Architecture decision records

An ADR captures one significant decision: the context, the options, what was chosen and what follows from it. We use a light version of [MADR](https://adr.github.io/madr/).

| # | Title | Status |
| --- | --- | --- |
| [0001](0001-record-architecture-decisions.md) | Record architecture decisions | Accepted |
| [0002](0002-dotnet-clean-architecture-backend.md) | .NET backend with Clean Architecture and Minimal APIs | Accepted |
| [0003](0003-react-vite-capacitor-client.md) | One React client for web and mobile | Accepted |
| [0004](0004-postgresql-and-sqlite-with-ef-core.md) | PostgreSQL and SQLite with EF Core | Accepted |
| [0005](0005-plugin-model-for-providers-and-context.md) | Providers and context sources as plugins | Accepted |
| [0006](0006-server-sent-events-for-streaming.md) | Server-sent events for streaming replies | Accepted |
| [0007](0007-encrypt-secrets-with-data-protection.md) | Encrypt secrets with Data Protection | Accepted |
| [0008](0008-testing-strategy.md) | Testing strategy | Accepted |
| [0009](0009-single-container-delivery.md) | Single container delivery | Accepted |

## Writing a new ADR

1. Copy [template.md](template.md) to `NNNN-short-title.md` with the next number.
2. Open a pull request; discussion happens there.
3. Once merged, the status is *Accepted*. To change a decision, write a new ADR that supersedes the old one, and update the old one's status.
