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
- `src/ControlPlane.AppHost/` - .NET Aspire local orchestration and dashboard entry point
- `src/ControlPlane.ServiceDefaults/` - shared health, resilience, service discovery, and OpenTelemetry defaults
- `run/` - compatibility startup wrapper
- `.github/` - CI workflow and dependency update configuration

## Local run

Aspire is the canonical local entry point. It starts the API, Vite UI, PostgreSQL,
Azure Service Bus Emulator, and the Aspire dashboard as one distributed application:

```bash
dotnet run --project src/ControlPlane.AppHost
```

The AppHost requires Docker for PostgreSQL and the Azure Service Bus Emulator. The
Dashboard URL is printed by Aspire when startup completes. For the first run, Aspire
may ask Docker to pull the required images.

The PowerShell wrapper at `run/run.ps1` remains as a compatibility entry point and
forwards to the AppHost; it no longer launches separate API/UI processes.

Configuration defaults live in the tracked `.env` (placeholders only). Put local values in `.env.local`, which is gitignored.

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
