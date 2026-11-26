# 6. Runtime view

## Chatting with an orb

```mermaid
sequenceDiagram
  autonumber
  participant C as Client
  participant E as Conversation endpoint
  participant S as ChatService
  participant R as Repositories / DB
  participant A as IModelAdapter
  participant P as Provider

  C->>E: POST /api/conversations/{id}/messages {content}
  E->>S: SendAsync(id, content)
  S->>R: load conversation, orb, providers, linked context
  S->>S: ModelResolver picks provider + model
  Note over S: no usable model? -> 409 before anything is stored
  S->>R: add user message, save
  S-->>E: IAsyncEnumerable<ChatEvent>
  E-->>C: 200 text/event-stream
  E-->>C: event: start {model}
  S->>S: PromptBuilder builds system prompt + history
  S->>A: StreamChatAsync(connection, request)
  A->>P: POST /chat/completions (stream)
  loop every fragment
    P-->>A: data: {delta}
    A-->>S: text
    S-->>C: event: delta {text}
  end
  S->>R: add assistant message, save
  S-->>C: event: done {messageId}
```

If the provider fails mid-reply, the stream ends with `event: error {text}` and no assistant message is stored; the user message stays so nothing typed is lost.

## Connecting a context source

1. `POST /api/context-sources {name, kind, location | input}`.
2. `ContextService` resolves the connector for `kind` and validates what it needs (a location, or input).
3. The connector reads the source right away. Success stores the text and marks it `Ready`; failure is recorded as `Failed` with the error, and the request still succeeds so the user can fix and retry (`POST /{id}/sync`).
4. The user links the source to orbs in the orb editor.

## First start

1. The API applies migrations (PostgreSQL) or creates the schema (SQLite).
2. With `Orbs__SeedDemoData=true` and an empty database, `DemoSeeder` adds the Echo provider, a welcome note and Pip.
3. The web app is served from `wwwroot`; unknown paths fall back to `index.html` for client-side routing.
