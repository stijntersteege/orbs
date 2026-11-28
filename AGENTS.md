# Notes for AI coding assistants

This file helps AI assistants (and humans) contribute consistently. The full rules are in CONTRIBUTING.md and docs/architecture.

- Backend: .NET 10 in `backend/` (solution `Orbs.slnx`). Clean Architecture; the dependency rule is enforced by `Orbs.Architecture.Tests`.
- Frontend: React + TypeScript + Vite in `frontend/`, pnpm. Server state only through hooks in `src/api/queries.ts`.
- New model providers implement `IModelAdapter`; new context sources implement `IContextConnector`; new tools implement `ITool` (or `IToolSource` for a set of tools). Register them in `Orbs.Infrastructure/DependencyInjection.cs`. Never add vendor-specific branches to Domain or Application.
- Before finishing: `cd backend && dotnet format Orbs.slnx && dotnet test Orbs.slnx`, and `cd frontend && pnpm lint && pnpm typecheck && pnpm test`.
- API contract changes also go into the in-browser demo API in `frontend/src/demo/server.ts`.
- User-visible behaviour gets a Reqnroll scenario in `backend/tests/Orbs.Specs/Features`.
- Never commit secrets, `.env` files or database files.
