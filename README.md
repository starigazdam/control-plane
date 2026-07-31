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

## Local run

1. Install dependencies in `ui/`.
2. Run `run\run.ps1`.

## Rules

- Every vertical or feature slice must have an end-to-end test.
- Prefer .NET Aspire for e2e coverage.
- Verify the full observe + act loop.
- Build production-intent slices, not MVP shortcuts.
- Use a separate feature branch + worktree for GitHub issues, and require human PR approval before merge.

## Plugin guidance

- Keep operations explicit and opinionated.
- Add runnable `.http` examples for plugin endpoints.
- Load secrets from `.env`.
