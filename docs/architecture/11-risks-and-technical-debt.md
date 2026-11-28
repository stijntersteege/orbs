# 11. Risks and technical debt

## Risks

| Risk | Impact | Mitigation |
| --- | --- | --- |
| **First-run claim**: until the first account exists, anyone who reaches the server can create it. | A stranger could become admin of a fresh install. | Create the account right after the first start; documented in SECURITY.md and the self-hosting guide. |
| **Server-side fetching** of web pages (SSRF). | A user could make the server request internal URLs. | Fetches go through `SafeHttp`: private, loopback and link-local addresses are refused at connect time (after DNS, so redirects and rebinding are covered), with an admin allow list and block list. The same guard covers push endpoints, and providers, apps, channels and MCP servers that people who are not admins add; admins may reach local runtimes such as Ollama. |
| **Prompt injection** through web pages, files, emails or tool results. | A model could be talked into calling a tool the person did not intend. | Tools that change things ask first by default, every call is shown and saved, an orb asked by another orb runs without tools, and skills are text the person saved. |
| **Brute-force sign-in.** | Guessed passwords. | Rate limits per address, strong password rules and passkeys; set `Orbs:TrustForwardedHeaders` behind a proxy so limits apply per visitor. |
| **Large context** is cut to a character budget when no embedding model is set. | Answers may miss information in big sources. | Set an embedding model to use retrieval. |
| **Provider API drift** (streaming formats, model lists). | Adapters break. | Adapters are small and isolated; unit tests for parsing; community can add native adapters. |
| **Losing the Data Protection key ring.** | Stored API keys can no longer be decrypted. | Keys live in the database, so database backups cover them. Documented in the self-hosting guide. |

## Technical debt

| Item | Why it exists | Plan |
| --- | --- | --- |
| Background jobs run inside the web process, polled from a table. | One container, no extra broker. | Fine for one instance. Running several instances would need row locking (`FOR UPDATE SKIP LOCKED`) when claiming jobs. |
| Frontend API types are written by hand. | Avoids a generator step for a small API. | Generate from `/openapi/v1.json` once the API stabilizes. |
| Retrieval scores every passage of an orb's sources in memory. | No database extension needed, and it works on SQLite and PostgreSQL alike. | Fine for thousands of passages; move to pgvector or sqlite-vec when people index far more. |
| English-only UI copy. | Focus on the core first. | Introduce i18n (message catalogs) when translators join. |
