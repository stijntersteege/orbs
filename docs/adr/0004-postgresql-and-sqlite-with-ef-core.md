# 0004. PostgreSQL and SQLite with EF Core

- **Status**: Accepted
- **Date**: 2026-11-25

## Context

Self-hosters range from one person on a laptop to a team on a server. The first group wants zero setup; the second wants a robust database with backups and migrations.

## Options

1. PostgreSQL only: robust, but every contributor and every small install needs a database server.
2. SQLite only: zero setup, but limited for concurrent multi-user use.
3. **Both through EF Core**: one model; SQLite by default, PostgreSQL when configured.

## Decision

EF Core with a provider switch (`Database:Provider` = `Sqlite` or `Postgres`). PostgreSQL uses EF migrations (in `Infrastructure/Persistence/Migrations`). SQLite creates its schema from the model, which also powers fast in-memory integration tests. The Compose file uses PostgreSQL.

## Consequences

- Contributors run the API with no database server; tests need no containers.
- Mappings must stay portable: enums as strings, primitive collections, and a `DateTimeOffset` converter for SQLite only.
- Migrations target PostgreSQL. A SQLite install that needs a schema change in future versions will need a migration path; we will add SQLite migrations before the first such change ships.
