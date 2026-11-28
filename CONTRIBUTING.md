# Contributing to Orbs

Thanks for wanting to help! Orbs is a personal hobby project that I work on in my spare time. Every contribution is welcome: bug reports, ideas, docs, tests, design and code. Because it's a hobby, reviews and replies can take a while, and not every idea will make it in.

By taking part you agree to follow the [Code of Conduct](CODE_OF_CONDUCT.md).

## Ways to help

- **Report a bug**: open an issue with the bug template. Steps to reproduce help most.
- **Suggest a feature**: describe the problem first in an issue or in Discussions.
- **Improve the docs**: typos, unclear steps and missing guides are all fair game.
- **Pick an issue**: look for `good first issue` or `help wanted`. Comment so nobody doubles up.
- **Add a provider or connector**: see the guides in [docs/guides](docs/guides).

For anything larger than a small fix, please open an issue first so we can agree on the approach before you spend time on it.

## Setting up

You need the .NET 10 SDK, Node.js 24 with pnpm, and optionally Docker.

```bash
git clone https://github.com/<you>/orbs.git
cd orbs
make setup
make dev-api   # terminal 1
make dev-web   # terminal 2
```

The [development guide](docs/guides/development.md) covers running against PostgreSQL, migrations and debugging.

## Making a change

1. **Branch** from `main`: `feat/orb-voice`, `fix/context-sync-timeout`, `docs/mobile-guide`.
2. **Write the test first** where you can. A bug fix starts with a failing test.
3. **Keep it small**. One pull request, one concern. Refactors go in their own PR.
4. **Run the checks**: `make lint` and `make test` must pass.
5. **Commit** using [Conventional Commits](https://www.conventionalcommits.org):
   `feat(chat): stop a reply mid-stream`, `fix(api): return 404 for unknown orbs`, `docs: explain API tokens`.
6. **Open a pull request** and fill in the template. Screenshots for UI changes, light and dark, desktop and phone.

## Code guidelines

These are the house rules. The [arc42 docs](docs/architecture/README.md) explain the why.

### Architecture

- The dependency rule is enforced by tests: `Api -> Infrastructure -> Application -> Domain`. The domain references nothing.
- **Business rules live in the domain.** Entities protect their own invariants; services orchestrate.
- **Use cases live in the application layer** as small services. They talk to the outside world only through interfaces (ports) declared in `Orbs.Application/Abstractions`.
- **Vendors are plugins.** Never add a provider-specific `if` in the domain or application layer. Add an adapter.
- Decisions that change the architecture get an [ADR](docs/adr/README.md).

### Backend (C#)

- Warnings are errors; analyzers run on build. `dotnet format` decides formatting.
- Prefer small, sealed classes, primary constructors and records for data.
- Async all the way, with a `CancellationToken` on every I/O path.
- Throw `DomainException` for broken rules, `NotFoundException` for missing things. The API maps them to problem details.

### Frontend (TypeScript)

- Strict TypeScript; no `any`.
- Server state goes through TanStack Query hooks in `src/api/queries.ts`. Components never call `fetch` directly.
- Styles use the design tokens in `src/styles/tokens.css` and CSS Modules. No hard-coded colors outside the orb palette.
- Accessible by default: real buttons and labels, visible focus, 44 px touch targets, text contrast of at least 4.5:1.
- Test behaviour the way people use it (Testing Library roles and labels), not implementation details.

### Tests

| Change | Expected tests |
| --- | --- |
| Domain rule | Unit test in `Orbs.Domain.Tests` |
| Use case | Test in `Orbs.Application.Tests` with in-memory fakes |
| Endpoint | Integration test, and a Reqnroll scenario if it is user-visible behaviour |
| UI | Vitest + Testing Library; Playwright for a critical flow |

Write Gherkin scenarios in the language of the product (orbs, sources, models), not of HTTP.

## Reviews

- I review every pull request when I have time. Expect questions; they are about the code, never about you.
- A pull request needs green CI and my approval to merge. I squash-merge with the PR title as the commit message.
- Stale PRs (no activity for 30 days) may be closed; you are always welcome to reopen.

## Releasing

1. Update [CHANGELOG.md](CHANGELOG.md): move items from *Unreleased* to the new version.
2. Bump the version in `frontend/package.json` and `backend/Directory.Build.props`, then tag: `git tag v1.0.0 && git push --tags`.
3. The release workflow publishes the image to GHCR (`1.0.0`, `1.0` and `latest`) and creates the GitHub release from the changelog section.

## License

By contributing, you agree that your contributions are licensed under the [MIT License](LICENSE).
