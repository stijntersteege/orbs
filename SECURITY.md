# Security policy

## Supported versions

Orbs is young. Security fixes go into the latest release and `main`.

| Version | Supported |
| --- | --- |
| 0.1.x | ✅ |
| older | ❌ |

## Reporting a vulnerability

**Please do not open a public issue.**

Report privately through [GitHub Security Advisories](https://github.com/your-org/orbs/security/advisories/new), or email **[security email]**.

Include what you found, how to reproduce it, and the impact you expect. We aim to:

- acknowledge your report within **3 working days**,
- share an assessment and a plan within **10 working days**,
- credit you in the release notes, unless you prefer otherwise.

## How Orbs protects your data

- **Self-hosted**: your orbs, context and chats stay in your own database.
- **API keys** of model providers are encrypted at rest with ASP.NET Core Data Protection and are never returned by the API.
- **Least exposure**: an orb only sends a model the context sources you linked to it.
- **Optional API token**: set `Orbs__ApiToken` to require a bearer token on every `/api` call.
- **Containers** run as a non-root user.
- **Supply chain**: Dependabot, CodeQL and locked dependency versions (central package management, pnpm lockfile).

## Hardening checklist for self-hosting

- Put Orbs behind HTTPS (a reverse proxy such as Caddy, Traefik or nginx).
- Set `Orbs__ApiToken` before exposing Orbs to the internet. Multi-user accounts are on the roadmap.
- Change the default PostgreSQL password in `.env` and do not publish the database port.
- Back up the database volume; it also holds the keys that decrypt your provider API keys.
- The web page connector fetches URLs from the server. If your server can reach internal services, only let trusted people add sources.
