# 2. Constraints

## Technical

| Constraint | Background |
| --- | --- |
| Self-hostable on a single small machine | Orbs must run on a home server or a cheap VPS: one container plus an optional database. |
| Web and mobile from one codebase | A small team cannot maintain three clients. See [ADR 0003](../adr/0003-react-vite-capacitor-client.md). |
| No vendor lock-in | No provider SDK in the core; providers sit behind an interface. See [ADR 0005](../adr/0005-plugin-model-for-providers-and-context.md). |
| Open standards | HTTP + JSON, OpenAPI, server-sent events, OCI images, SQL databases. |
| Long-term support runtimes | .NET 10 (LTS) and Node.js 24 (LTS) to limit forced upgrades. |

## Organizational

| Constraint | Background |
| --- | --- |
| MIT license | Permissive, so companies and individuals can adopt and extend Orbs. All dependencies must be compatible. |
| Everything as code | Infrastructure (Dockerfile, Compose), CI, docs and diagrams live in the repository. |
| Volunteer contributors | Fast local feedback, tests that run without external services, documented decisions. |

## Conventions

- Architecture docs: arc42; decisions: ADRs in MADR style.
- Commits: Conventional Commits; versions: Semantic Versioning; changelog: Keep a Changelog.
- Formatting and analyzers are enforced in CI (`dotnet format`, .NET analyzers as errors, oxlint, Prettier, strict TypeScript).
