# 0006. Server-sent events for streaming replies

- **Status**: Accepted
- **Date**: 2026-11-25

## Context

Model replies take seconds. Showing text as it arrives makes orbs feel alive and lets people stop a reply early.

## Options

1. Polling: simple, laggy and wasteful.
2. WebSockets (or SignalR): two-way and powerful, but needs connection management, sticky sessions behind proxies and more client code.
3. **Server-sent events over a normal HTTP response**: one-way, proxy-friendly, natively supported by ASP.NET Core (`TypedResults.ServerSentEvents`).

## Decision

`POST /api/conversations/{id}/messages` answers with `text/event-stream` and four event types: `start` (model), `delta` (text), `done` (message id), `error` (text). Validation errors (unknown conversation, no model) are returned as normal problem responses before the stream starts. The client reads the stream with `fetch` because `EventSource` cannot POST.

## Consequences

- Works through standard reverse proxies (disable response buffering for `/api` if a proxy buffers).
- Stopping a reply is a client abort; the server stops reading the provider.
- If true two-way features arrive (voice, live tool approval), WebSockets can be added for those without changing chat.
