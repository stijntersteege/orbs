# 0005. Providers and context sources as plugins

- **Status**: Accepted
- **Date**: 2026-11-25

## Context

New model APIs and new places to read context from appear all the time. Orbs must stay generic: no vendor is special, and adding one must not ripple through the codebase.

## Options

1. Enums and `switch` statements per provider in the core: simple at first, violates the open/closed principle and grows without bound.
2. A third-party AI abstraction library in the core: less code, but couples the domain to its release cycle and concepts.
3. **Own small ports** (`IModelAdapter`, `IContextConnector`) in the application layer, implemented in infrastructure and found by a string **kind**.

## Decision

Option 3. Each implementation declares a `Kind`, display name and description, plus what it needs (base URL, API key, location, input). A generic `PluginRegistry<T>` resolves kinds. Entities store the kind as a validated slug. The first adapters are an **OpenAI-compatible** adapter (which covers most providers and local runtimes) and an **Echo** demo adapter; the first connectors are **text notes** and **web pages**.

## Consequences

- Adding a kind is one class and one registration line, and the UI discovers it via `/kinds` endpoints.
- Provider presets (OpenRouter, Ollama, ...) are UI data on top of generic kinds, not backend code.
- A library such as Microsoft.Extensions.AI can still be used inside an adapter without leaking into the core.
- Unknown kinds in stored data (for example after removing a plugin) are reported as clear errors.
