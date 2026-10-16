# OpenSpec

OpenSpec is the spec-driven workflow this repository uses to plan changes before
implementation. It keeps baseline behavior, proposed deltas, and execution
tracking separate.

## Where artifacts live

- `openspec/specs/` holds baseline specs. A baseline spec describes behavior
  that already exists in the repository.
- `openspec/changes/` holds proposed changes. A change carries a delta spec,
  design notes, and tasks. `openspec/changes/archive/` holds completed changes.
- `docs/adr/` holds architecture rationale. ADRs explain durable decisions and
  trade-offs; specs state required behavior.
- GitHub holds issues, pull requests, CI, and merge approval. Those track work
  in progress and completion.

Baseline specs describe existing behavior only. Proposed behavior belongs in a
change. Passing validation is structural; it does not prove safety, correctness,
or that an implementation exists.

## Setup

This repository pins the CLI at version `1.14.2`. Do not install it globally.
Run it through `npm exec` from the repository root. Set `OPENSPEC_TELEMETRY=0`
and `DO_NOT_TRACK=1` on every invocation.

```bash
export OPENSPEC_TELEMETRY=0
export DO_NOT_TRACK=1
```

The shared skills in `.agents/skills` show bare `openspec <args>` examples.
Read each one as the full pinned command from the repository root, with those
opt-outs set:

```bash
npm exec --yes --package=@fission-ai/openspec@1.14.2 -- openspec <args>
```

For example, check the pinned version:

```bash
npm exec --yes --package=@fission-ai/openspec@1.14.2 -- openspec --version
```

Expected output: `1.14.2`.

Some agent clients only run commands that match a permission allowlist. A
`Bash(openspec:*)` permission does not authorize `npm exec`, so those clients
may need an entry for the exact pinned invocation above. Keep your client's
approval boundaries: do not add an `npm` wildcard, change permissions
automatically, install globally, or wrap the command.

The shared skills live in `.agents/skills`. Not every client discovers that
directory automatically. If yours does not, open the named shared skill file
directly. This guide does not configure every client, and it does not change
global discovery or trust settings.

## Common commands

List baseline specs:

```bash
npm exec --yes --package=@fission-ai/openspec@1.14.2 -- openspec list --specs
```

List active changes:

```bash
npm exec --yes --package=@fission-ai/openspec@1.14.2 -- openspec list
```

Validate every artifact in strict mode:

```bash
npm exec --yes --package=@fission-ai/openspec@1.14.2 -- openspec validate --all --strict --no-interactive
```

Start a new change:

```bash
npm exec --yes --package=@fission-ai/openspec@1.14.2 -- openspec new change "your-change-name"
```

## Workflow

Plan first, review the plan, then implement. [`.hermes.md`](../.hermes.md) is
the canonical rule; this section adds the OpenSpec-specific detail.

1. Explore and refine the problem. Use the `openspec-explore` skill to think
   through options and constraints.
2. Write the change artifacts: a proposal, a spec delta, a design, and tasks.
   Use the `openspec-propose` skill. Baseline specs keep describing existing
   behavior only.
3. Get an independent high-capability review of those artifacts. Use the named
   reviewer when the maintainer selects one, and do not pin a specific model
   globally. Confirm the actual model and provider, and bind the approval to
   the reviewed revision. Resolve blocking findings; re-review a materially
   changed specification.
4. Get explicit human approval of the reviewed design before any
   implementation.
5. Implement only under a separate, explicit authorization. A general
   start/build request, an issue selection, an approved proposal, or merged
   planning artifacts does not authorize apply. Never combine initial proposal
   creation and implementation in one worker run. Add tests with the
   implementation.
6. Require exact-head CI, independent final review, and human merge approval
   before merge. Sync and archive only after evidence-backed completion. A
   specification review does not approve unimplemented code, deployment,
   merge, or archive.

A proposal-only pull request does not close a feature issue. Planning artifacts
are not implementation. Applying a change, archiving it, and merging it are
separate actions that each need their own authorization and normal review, CI,
and human approval gates.

## Baseline specs

- [plugin-registration](../openspec/specs/plugin-registration/spec.md) covers assembly-driven
  plugin discovery and registration.
- [operation-execution](../openspec/specs/operation-execution/spec.md) covers the request,
  validation, execution, and persistence path.

## Refinement queue

This queue names work that still needs refinement. It is not a backlog copy and
does not authorize implementation.

Facts about the current code:

- The operation execution path records a client-supplied `RequestedBy` value, or
  the literal fallback `local-engineer`, and does not verify identity.
- The inspected path has no authentication, authorization, confirmation, or
  immutable audit.
- Plugins are discovered from assemblies supplied by the host, not by a dynamic
  loader.

Unimplemented goals (need a change before any code):

- An authenticated identity for operation requests.
- Authorization checks for operations.
- A confirmation step for guarded operations.
- Redaction and audit for persisted history.

Open decisions (not decided here):

- The authentication technology, SDK choice, and transport for identity.
- The operation schema and any agent-facing projection.
- Whether agent identity is per-user GitHub/Copilot identity or server-side
  identity, and the authentication and licensing implications of each.

### Foundation refinement (#14)

The foundation gaps above - authenticated identity, authorization,
confirmation, and redaction/audit - need refinement before an implementation
proposal. This document does not choose an auth technology, an SDK, or a
transport.

### Agent parity refinement (#48)

Agent parity remains a DRAFT. It is subject to high-tier refinement and human
approval before any builder work on that epic. Preserve its prior-art and
provider/subscription-neutrality gates. Two decisions remain unresolved:
identity and subscription handling, and chat-confirmation equivalence. No
active change artifacts are created for it here, and it is not accepted.
