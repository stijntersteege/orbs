# 1. Introduction and goals

## What Orbs is

Orbs is an open-source, self-hosted home for AI companions. An **orb** combines:

- **a model** from any connected provider,
- **its context**: the sources it may read,
- **a face**: a customizable mascot that makes each orb recognizable.

People create orbs in a web or mobile app, link the context each orb may use, and chat with them. Replies stream in as they are written.

## Requirements overview

| ID | Requirement |
| --- | --- |
| F1 | Create, change and delete orbs: name, description, brief, model choice, context and appearance. |
| F2 | Connect model providers of different kinds; list their models; pick a default model. |
| F3 | Connect context sources of different kinds; sync them; share each with chosen orbs. |
| F4 | Chat with an orb; stream the reply; keep conversation history per orb. |
| F5 | Work in a desktop browser and on phones (responsive web, installable, native shells). |
| F6 | Run on infrastructure the user controls, with one command. |

## Quality goals

The top three, in priority order. Section 10 refines them into scenarios.

| Priority | Quality | Motivation |
| --- | --- | --- |
| 1 | **Extensibility** | New models and context sources appear constantly. Adding one must not touch the core. |
| 2 | **Maintainability** | An open-source project lives on contributions. The code must be easy to understand, test and change. |
| 3 | **Privacy and security** | Users connect their documents and API keys. Data stays on their server; secrets are protected. |

Usability (a calm, accessible, playful UI) and operability (one container, sane defaults) follow closely.

## Stakeholders

| Role | Expectations |
| --- | --- |
| **Self-hosters** | Easy install, low resource use, safe defaults, upgrades without surprises. |
| **Everyday users** | A friendly app that works on any device, with any model they can access. |
| **Contributors** | Clear structure, fast tests, documented decisions, a welcoming process. |
| **Integrators** | A documented API (OpenAPI) and small extension points for providers and connectors. |
