# 11. Risks and technical debt

## Risks

| Risk | Impact | Mitigation |
| --- | --- | --- |
| **First-run claim**: until the first account exists, anyone who reaches the server can create it. | A stranger could become admin of a fresh install. | Create the account right after the first start; documented in SECURITY.md and the self-hosting guide. |
| **Server-side fetching** of web pages (SSRF). | A user could make the server request internal URLs. | Fetches go through `SafeHttp`: private, loopback and link-local addresses are refused at connect time (after DNS, so redirects and rebinding are covered), with an admin allow list and block list. Model provider URLs are exempt so local runtimes work. |
| **Large context** is cut to a character budget. | Answers may miss information in big sources. | Retrieval with embeddings behind the `PromptBuilder` seam. |
| **Provider API drift** (streaming formats, model lists). | Adapters break. | Adapters are small and isolated; unit tests for parsing; community can add native adapters. |
| **Losing the Data Protection key ring.** | Stored API keys can no longer be decrypted. | Keys live in the database, so database backups cover them. Documented in the self-hosting guide. |

## Technical debt

| Item | Why it exists | Plan |
| --- | --- | --- |
| Conversations are loaded with all messages. | Simple and fine for typical chat sizes. | Paginate messages when histories grow. |
| Context sync is on demand only. | Keeps the first version simple. | The job runner exists now; scheduled sync builds on it. |
| Background jobs run inside the web process, polled from a table. | One container, no extra broker. | Fine for one instance. Running several instances would need row locking (`FOR UPDATE SKIP LOCKED`) when claiming jobs. |
| Frontend API types are written by hand. | Avoids a generator step for a small API. | Generate from `/openapi/v1.json` once the API stabilizes. |
| English-only UI copy. | Focus on the core first. | Introduce i18n (message catalogs) when translators join. |
| Only two provider adapters. | The OpenAI-compatible adapter covers most providers. | Add native adapters where a provider has no compatible API. |
