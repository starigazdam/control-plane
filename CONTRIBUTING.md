# Contributing

## Workflow

- One feature branch (and worktree) per GitHub issue; open the pull request against `develop`.
- Every pull request needs green CI and human approval before merge.
- Build production-intent vertical slices — implementation, tests, and docs together, not MVP shortcuts.
- Every vertical or feature slice must have an end-to-end test that verifies the full observe + act loop.
- Never describe a check or test as existing before it does.

## Local development

- Backend build: `dotnet build src/ControlPlane.slnx`
- Aspire local run: `dotnet run --project src/ControlPlane.AppHost`
- Aspire integration tests: `dotnet test tests/ControlPlane.AppHost.Tests/ControlPlane.AppHost.Tests.csproj`
- UI checks: `npm ci --prefix ui`, `npm run lint --prefix ui`, `npm test --prefix ui`, and `npm run build --prefix ui`
- PowerShell compatibility wrapper: `run\run.ps1`
- Remote Docker sandbox workflow: [`docs/development-docker-sandbox.md`](docs/development-docker-sandbox.md)
  with `run/sandbox-preflight.sh` and `run/sandbox-run.sh`. Aspire/DCP must run
  beside the sandbox's native Docker socket; do not use a forwarded TCP Docker endpoint.

Put local configuration in `.env.local`; leave the tracked `.env` as placeholders. Because `.env` is tracked, `.gitignore` cannot protect it — confirm the repository has **secret scanning with push protection** enabled (see [SECURITY.md](SECURITY.md)) so a real value pushed into `.env` is blocked.

## Quality gates

`.github/workflows/ci.yml` runs on pull requests and pushes to `develop`:

| Gate | Command |
| --- | --- |
| .NET restore | `dotnet restore src/ControlPlane.slnx` |
| .NET build | `dotnet build src/ControlPlane.slnx --configuration Release` |
| .NET test | `dotnet test tests/ControlPlane.AppHost.Tests/ControlPlane.AppHost.Tests.csproj` — boots the AppHost and verifies the API health endpoint through Aspire resources |
| UI install | `npm ci` in `ui/` |
| UI lint | `npm run lint` in `ui/` (oxlint) |
| UI test | `npm test` in `ui/` (vitest) |
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
