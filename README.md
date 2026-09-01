# Control Plane

A pluggable engineering workbench for operational visibility and one-click actions across Azure, Kafka, pipelines, alerts, and custom workflows.

## What it is

- One place to see project health
- One place to execute common operational actions
- Opinionated over generic
- Built for engineers, not administrators

## Stack

- Frontend: React, Vite, TypeScript, Tailwind
- Backend: ASP.NET Core
- Storage: SQLite initially

## Repo layout

- `src/` - .NET solution and backend projects
- `ui/` - React frontend
- `docs/` - engineering rules and plugin guidance
- `run/` - local startup scripts
- `.github/` - CI workflow and dependency update configuration

## Local run

1. Install dependencies: `npm ci --prefix ui`.
2. Run `run\run.ps1`.

Configuration defaults live in the tracked `.env` (placeholders only). Put local values in `.env.local`, which is gitignored.

## Architecture decisions

Significant, durable choices are recorded in [Architecture Decision Records](docs/adr/README.md).
Read the relevant ADR before changing plugin contracts, operation behavior, or
agent activation defaults.

## Quality gates

CI runs on every pull request and on pushes to `develop`:

- .NET: `dotnet restore` and `dotnet build` for `src/ControlPlane.slnx`. `dotnet test` runs only once a test project exists - there is none yet.
- UI: `npm ci`, `npm run lint`, and `npm run build` in `ui/`.

See [CONTRIBUTING.md](CONTRIBUTING.md) for the workflow and [SECURITY.md](SECURITY.md) for public-repository and vulnerability-reporting rules.

## Rules

- Every vertical or feature slice must have an end-to-end test.
- Prefer .NET Aspire for e2e coverage.
- Verify the full observe + act loop.
- Build production-intent slices, not MVP shortcuts.
- Use a separate feature branch + worktree for GitHub issues, and require human PR approval before merge.
- Keep every public artifact generic: no credentials, environment topology, or internal references.

## Plugin guidance

- Keep operations explicit and opinionated.
- Add runnable `.http` examples for plugin endpoints.
- Load configuration from `.env`; keep real secrets in `.env.local`.
