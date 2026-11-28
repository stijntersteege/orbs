# 3. Context and scope

## Business context

```mermaid
flowchart LR
  U((Person)) -->|creates orbs, chats| O[Orbs]
  O -->|prompts with context| P[Model providers<br/>hosted or local]
  O -->|reads| S[Context sources<br/>notes, web pages, ...]
  A((Self-hoster)) -->|installs, configures| O
```

| Partner | Exchanged |
| --- | --- |
| Person | Orb definitions, messages in; streamed replies, history out. |
| Model providers | Chat requests (system prompt with linked context + history) out; text fragments in. Model lists in. |
| Context sources | Content in (text of a note, a web page). Orbs never write to sources. |
| Self-hoster | Configuration (environment variables), backups, upgrades. |

## Technical context

```mermaid
flowchart LR
  subgraph Device
    B[Browser / PWA]
    N[Capacitor app]
  end
  B & N -- "HTTPS: REST + JSON<br/>SSE for replies" --> S[Orbs server<br/>ASP.NET Core]
  S -- "SQL" --> D[(PostgreSQL or SQLite)]
  S -- "HTTPS: OpenAI chat API" --> P[Providers]
  S -- "HTTPS GET" --> W[Web pages]
```

| Channel | Protocol | Notes |
| --- | --- | --- |
| Client to server | HTTPS, JSON, `text/event-stream` | Optional bearer token (`Orbs__ApiToken`). |
| Server to database | Npgsql or SQLite | EF Core; migrations for PostgreSQL. |
| Server to providers | HTTPS, OpenAI Chat Completions with streaming | Adapter per kind; keys decrypted only for the call. |
| Server to web pages | HTTPS GET | 30 s timeout; HTML reduced to text. |

## Out of scope

Billing and payments, phone calls, a browser-automation sidecar and app-store builds are deliberately out of scope for 1.0. Accounts, tools, voice, retrieval, channels and schedules, once listed here, all shipped in 1.0.
