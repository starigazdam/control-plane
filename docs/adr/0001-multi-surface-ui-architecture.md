# ADR 0001: Architecture for Multiple UI Surfaces

- Status: Proposed
- Related: #10

## Context

Control Plane today has exactly one UI surface: the React/Vite app in `ui/`.
It talks to `ControlPlane.Api` (ASP.NET Core), which is backed by
`ControlPlane.Core` (domain concepts: `Project`, `StatusSnapshot`,
`StatusSignal`, `StatusLevel`; interfaces: `IOperation`, `IStatusProvider`)
and plugin projects (`ControlPlane.Azure`, `ControlPlane.Kafka`,
`ControlPlane.ServiceBus`, `ControlPlane.DevOps`) that each implement
`IControlPlanePlugin` and register operations/status providers via
`IPluginRegistration` (see `docs/plugin-philosophy.md`).

We want to add a second surface — a VS Code extension is the concrete
driver — without duplicating backend logic per surface. This ADR reviews
what's reusable today, what's coupled to the web app, and what needs to
change before a second surface is practical.

### What's already surface-agnostic (good news)

- The API is already opinionated and action-oriented rather than a generic
  CRUD/admin API: `OperationsController` exposes `POST /api/operations/execute`
  and `GET /api/operations/history`; `ProjectsController` and
  `OverviewController` expose read models (`ProjectDetailsResponse`,
  `OverviewResponse`). This matches the plugin philosophy's "named actions,
  not filter-driven workflows" — a good fit for a VS Code command palette,
  not just a web dashboard.
- `OperationDefinition` (id, displayName, description, parameters) is
  self-describing. A surface can render available operations without
  hardcoding per-plugin UI, in principle.

### What's coupled to the web app today

- **No shared contract package.** The DTOs in `src/ControlPlane.Api/Contracts`
  (`OverviewResponse`, `ProjectDetailsResponse`, `OperationHistoryEntry`,
  `ExecuteOperationRequest`) are hand-mirrored as ad hoc TypeScript types
  inline in `ui/src/App.tsx` (`StatusSignal`, `StatusSnapshot`,
  `OperationDefinition`, `ProjectDetailsResponse`, `OperationHistoryEntry`).
  There's no generated client and no single source of truth — a second
  surface would either re-duplicate these types by hand or fork them,
  and the two copies will drift.
- **No API client layer.** `ui/src/App.tsx` is a single component that owns
  page routing state (`PageKey`), fetches, and rendering together. There is
  no extracted `fetchOverview()` / `fetchProject()` / `executeOperation()`
  module a non-React surface could import.
- **No auth model.** Nothing in `ControlPlane.Api` (`Program.cs`,
  controllers) wires up authentication or `[Authorize]`. That's fine for a
  same-origin browser SPA behind whatever network boundary exists today, but
  a VS Code extension talks to the API from a separate process/host and will
  need an explicit credential (e.g. a token the user pastes or an OAuth
  device flow) — this is a blocking gap, not a nice-to-have.
- **Single flat package, not a workspace.** `ui/` is a standalone npm
  package at the repo root of the frontend; there's no workspace tooling
  (npm/pnpm workspaces) to host a second JS/TS package (like a VS Code
  extension) alongside it with shared internal dependencies.
- **Styling/rendering is web-only by construction**, which is expected and
  fine — Tailwind + DOM markup in `App.tsx`/`App.css` has no reuse value for
  a VS Code webview or any non-browser surface. The reusable part is the
  *data fetching and shaping* above it, not the rendering.

## Decision

Split frontend code into two layers, and adopt a workspace layout that can
host more than one surface:

1. **Extract a surface-agnostic core package**: `ui/packages/core` (or
   `packages/control-plane-client` at repo root — naming TBD in
   implementation) containing:
   - TypeScript types generated from (or kept in lockstep with) the
     `ControlPlane.Api.Contracts` DTOs — prefer generating these from the
     API's OpenAPI schema over hand-mirroring, so drift becomes a build
     failure instead of a runtime bug.
   - A thin HTTP client (`getOverview`, `getProject`, `listOperations`,
     `executeOperation`, `getOperationHistory`) with no React/DOM
     dependency, parameterized by base URL and an auth token/header.
   - No UI framework dependency — plain TS, usable from a React app, a VS
     Code extension host (Node), or a CLI.

2. **Convert `ui/` into an npm/pnpm workspace root** with the existing app
   moved to `ui/apps/web` (or similar) and the new core package at
   `ui/packages/core`, so `apps/web` depends on `packages/core` via a
   workspace reference. A future `ui/apps/vscode-extension` would depend on
   the same `packages/core`. This keeps the change scoped to `ui/` rather
   than restructuring the whole repo.

3. **Add token-based auth to `ControlPlane.Api`** before building the VS
   Code extension surface — minimally, an API key or bearer token checked
   via `[Authorize]`, since a VS Code extension has no browser session/cookie
   to rely on. Exact scheme (static token vs. OAuth device flow) is a
   separate decision; this ADR only asserts it's a prerequisite, not
   optional polish.

4. **Keep operations self-describing.** Continue exposing operations via
   `OperationDefinition` (id/displayName/description/parameters) rather than
   surface-specific hardcoding, so a VS Code command palette or web page can
   both render the same operation list from the same API response.

## Consequences

- Adding a VS Code extension later becomes: new `apps/vscode-extension`
  package + auth token flow + VS Code-specific UI (webview or native
  QuickPick/TreeView) on top of the existing `packages/core` client — not a
  rewrite of data-fetching logic.
- `ui/src/App.tsx`'s inline types and fetch calls need to be extracted and
  replaced with imports from `packages/core` — a refactor of the existing
  web app, done once, up front.
- Backend contract changes now have one place (`packages/core` types) to
  update on the frontend side instead of N ad hoc copies, at the cost of
  needing a step (codegen or manual sync) to keep it aligned with
  `ControlPlane.Api.Contracts`.
- Auth work is pulled forward: it's needed for the second surface even
  though the current single-surface web app works without it.

## Non-goals

- Building the VS Code extension itself (tracked separately once this
  lands, per #10).
- Choosing the exact auth scheme (API key vs. OAuth) — follow-up decision.
- Introducing a repo-wide monorepo tool (Nx/Turborepo) — plain npm/pnpm
  workspaces scoped to `ui/` are sufficient for two JS/TS packages.
