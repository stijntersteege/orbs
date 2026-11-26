# Security policy

Orbs is a personal hobby project that I maintain in my spare time. I take security reports seriously, but I can't promise response or fix times. I'll get back to you as soon as I can.

## Supported versions

Security fixes go into the latest release and `main`. If you run an older version, please update before reporting.

| Version | Supported |
| --- | --- |
| 0.1.x | ✅ |
| older | ❌ |

## Reporting a vulnerability

**Please do not open a public issue.**

Report it privately through [GitHub Security Advisories](https://github.com/stijntersteege/orbs/security/advisories/new), or email me at [mail@stijntersteege.com](mailto:mail@stijntersteege.com).

It helps if you include:

- the Orbs version and how you run it (Docker, from source),
- steps to reproduce, and a proof of concept if you have one,
- what an attacker could do with it,
- whether and how you'd like to be credited.

What you can expect from me:

- I'll confirm that I've seen your report and keep you posted while I work on it.
- I'll agree on a disclosure moment with you once a fix is out.
- I'll credit you in the release notes or the advisory, unless you'd rather not be named.

Please give me reasonable time to fix the problem before you share details publicly, and don't access or change other people's data while you investigate.

## How Orbs protects your data

- **Self-hosted**: your orbs, context and chats stay in your own database.
- **API keys** of model providers are encrypted at rest with ASP.NET Core Data Protection and are never returned by the API.
- **Least exposure**: an orb only sends a model the context sources you linked to it.
- **Optional API token**: set `Orbs__ApiToken` to require a bearer token on every `/api` call.
- **Containers** run as a non-root user.
- **Supply chain**: Dependabot, CodeQL and locked dependency versions (central package management, pnpm lockfile).
- **No telemetry**: Orbs doesn't send usage data to me or anyone else.

## Hardening checklist for self-hosting

- Put Orbs behind HTTPS (a reverse proxy such as Caddy, Traefik or nginx).
- Set `Orbs__ApiToken` before exposing Orbs to the internet. Orbs has no user accounts yet.
- Change the default PostgreSQL password in `.env` and do not publish the database port.
- Back up the database volume; it also holds the keys that decrypt your provider API keys.
- The web page connector fetches URLs from the server. If your server can reach internal services, only let trusted people add sources.
- Set spending limits with your model providers.
