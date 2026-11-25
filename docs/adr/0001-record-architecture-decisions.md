# 0001. Record architecture decisions

- **Status**: Accepted
- **Date**: 2026-11-25

## Context

Orbs is built by volunteers who join and leave over time. Without a record, the reasons behind important choices get lost, and the same debates repeat.

## Decision

We record significant decisions as ADRs in `docs/adr`, one Markdown file per decision, reviewed in pull requests. The arc42 documentation links to them from section 9.

## Consequences

- New contributors can learn why things are the way they are.
- Changing a decision means writing a new ADR, which makes the change visible.
- Small cost per decision; we only write ADRs for choices that are hard to reverse or affect many parts.
