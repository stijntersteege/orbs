# 0007. Encrypt secrets with Data Protection

- **Status**: Accepted
- **Date**: 2026-11-25

## Context

Users store provider API keys in Orbs. A leaked database or backup must not reveal them, and the API must never send them back to clients.

## Options

1. Plain text in the database: unacceptable.
2. An external secret store (Vault, cloud KMS): strong, but heavy for self-hosters.
3. **ASP.NET Core Data Protection**: authenticated encryption with key rotation, built into the framework.

## Decision

`ISecretProtector` in the application layer, implemented with Data Protection (purpose `Orbs.Secrets.v1`). The key ring is persisted in the database (`DataProtectionKeys` table) so containers can be replaced freely. DTOs expose only `hasApiKey`. On update, a `null` key keeps the stored key and an empty string removes it.

## Consequences

- Keys are protected at rest; API responses and the client never contain them.
- The key ring and the encrypted keys live in the same database, so this protects against leaked API responses and logs more than against a full database dump. Self-hosters who need more can configure Data Protection to protect the key ring with a certificate, documented as a hardening step.
- Losing the database means losing the keys; backups cover both.
