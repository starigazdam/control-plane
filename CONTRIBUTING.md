# Contributing

## Workflow

- One feature branch (and worktree) per GitHub issue; open the pull request against `develop`.
- Every pull request needs green CI and human approval before merge.
- Build production-intent vertical slices — implementation, tests, and docs together, not MVP shortcuts.
- Every vertical or feature slice must have an end-to-end test that verifies the full observe + act loop.
- Never describe a check or test as existing before it does.

## Local development

- Backend build: `dotnet build src/ControlPlane.slnx`
- Backend run: `dotnet run --project src/ControlPlane.Api/ControlPlane.Api.csproj`
- UI dependencies: `npm ci --prefix ui`
- UI dev server: `npm run dev --prefix ui`
- UI checks: `npm run lint --prefix ui` and `npm run build --prefix ui`
- Both dev servers on Windows: `run\run.ps1`

Put local configuration in `.env.local`; leave the tracked `.env` as placeholders.

## Quality gates

`.github/workflows/ci.yml` runs on pull requests and pushes to `develop`:

| Gate | Command |
| --- | --- |
| .NET restore | `dotnet restore src/ControlPlane.slnx` |
| .NET build | `dotnet build src/ControlPlane.slnx --configuration Release` |
| .NET test | `dotnet test src/ControlPlane.slnx` — runs only when a test project exists; the repository has none yet, so this step reports as skipped |
| UI install | `npm ci` in `ui/` |
| UI lint | `npm run lint` in `ui/` (oxlint) |
| UI build | `npm run build` in `ui/` (`tsc -b && vite build`) |

Adding the first `Microsoft.NET.Test.Sdk` project turns the .NET test gate on automatically — no workflow change needed.

## Public-repository rules

This repository is public, and everything in it (including issues, pull requests, and screenshots) is permanent and world-readable.

- No credentials, tokens, connection strings, tenant/subscription IDs, real hostnames, IPs, internal project or ticket references, or corporate usernames — in any artifact, including test fixtures and examples.
- Use generic placeholders and fabricated fixture data.
- If you find a real secret, follow [SECURITY.md](SECURITY.md): rotate it first, report it privately, and do not merely delete it.

## Do not commit

- Build output: `bin/`, `obj/`, `ui/dist/`, `node_modules/`
- Local databases and runtime data: `*.db`, `*.db-wal`, `*.db-shm`
- Test artifacts: `TestResults/`, `*.trx`, coverage files
- `.env.local` or any real value in `.env`
