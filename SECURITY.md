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
- **Accounts**: everyone signs in with a password, a passkey or an OpenID Connect provider. Each account only sees its own orbs, models, context and chats. Passwords are hashed with PBKDF2-SHA512; sessions are random tokens stored only as a hash, sent in an HttpOnly, SameSite cookie.
- **API token for scripts**: `Orbs__ApiToken` lets scripts call the API as the first admin.
- **Containers** run as a non-root user.
- **Supply chain**: Dependabot, CodeQL and locked dependency versions (central package management, pnpm lockfile).
- **No telemetry**: Orbs doesn't send usage data to me or anyone else.

## Hardening checklist for self-hosting

- Put Orbs behind HTTPS (a reverse proxy such as Caddy, Traefik or nginx).
- Set `Orbs__PublicUrl` to the address people open Orbs on (for example `https://orbs.example.com`), so passkeys work and are bound to it.
- Create the first account right after starting Orbs: until then anyone who reaches the server can claim it.
- Only set `Orbs__ApiToken` when a script needs it, and keep it long and secret: it acts as the first admin.
- Change the default PostgreSQL password in `.env` and do not publish the database port.
- Back up the database volume; it also holds the keys that decrypt your provider API keys.
- Web pages and tools never fetch addresses on private networks (10.x, 192.168.x, localhost, cloud metadata, ...). Allow specific internal hosts with `Orbs__Network__AllowedHosts` (comma-separated), or all with `Orbs__Network__AllowPrivateNetworks=true` on a server only you use. `Orbs__Network__BlockedHosts` blocks hosts on top. Model providers are not limited, so local runtimes such as Ollama keep working.
- Set spending limits with your model providers.
