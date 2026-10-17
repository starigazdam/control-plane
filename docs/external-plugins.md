# External Plugins

Control Plane consumes **external plugins** as separately deployed HTTP and JSON
services. The host never spawns, supervises, or restarts a plugin process, and it
does not ship an in-process dynamic assembly loader: the host loads the
`ControlPlane.ExternalPlugins` bridge from its own assemblies, and the bridge
talks to the configured plugin processes over HTTP.

The wire contract is published as OpenAPI in
[`external-plugins.openapi.json`](external-plugins.openapi.json). The fabricated
plugin the automated tests use is
[`tests/ControlPlane.ExternalPlugins.Fixture`](../tests/ControlPlane.ExternalPlugins.Fixture/Program.cs),
with a runnable example in
[`external-plugins-fixture.http`](../tests/ControlPlane.ExternalPlugins.Fixture/external-plugins-fixture.http).

## Trusted network only (until #57)

This slice adds **no authentication**. The host forwards no actor identity,
correlation id, credentials, `traceparent`, or `tracestate` to a plugin, and it
does not authenticate an external call. Any reachable plugin can therefore serve
arbitrary capabilities. Run external plugins on a trusted network only until
#57.

## Configuration

External plugins are read from a static list at startup. A restart applies a
change; there is no hot reload.

```json
{
  "ExternalPlugins": {
    "Plugins": [
      { "Name": "sample", "BaseUrl": "http://localhost:5099" }
    ]
  }
}
```

The equivalent environment keys are
`ExternalPlugins__Plugins__0__Name` and `ExternalPlugins__Plugins__0__BaseUrl`.
No `ExternalPlugins` configuration placeholder is committed. When the
`ExternalPlugins` section is absent the bridge registers no plugins and makes no
request; add the section through environment variables or a local, gitignored
override.

- `Name` must be 1-32 lowercase ASCII letters, digits, or hyphens and start with
  a letter. It is the namespace for every exposed id.
- `BaseUrl` must be an absolute `http` or `https` URI with no query, fragment, or
  userinfo. Any path in the base URL is preserved and the endpoint path is
  appended with exactly one `/`.
- A missing or invalid `Name` or `BaseUrl`, or a duplicate `Name`, fails startup
  with a clear error that names the setting.

## Wire contract

Each plugin serves these endpoints relative to its `BaseUrl`:

| Method | Path | Response |
| --- | --- | --- |
| `GET` | `/capabilities` | `statusProviderIds` and `operations` |
| `GET` | `/status/{id}?projectId={projectId}` | a status snapshot |
| `POST` | `/operations/{id}/validate` | an array of validation error strings |
| `POST` | `/operations/{id}/execute` | an execution result |

Payloads map the Control Plane Core records directly in camelCase;
`StatusLevel` and the execution status serialize as strings. The bridge sets the
execution timestamps (`startedAtUtc`, `completedAtUtc`) from its own call timing
and ignores remote timestamps.

The bridge fetches `GET /capabilities` **once per plugin at startup**, each call
bounded by a fixed 10-second timeout. It uses the remote id captured at discovery
(percent-encoded as one path segment) on every subsequent call, and it
percent-encodes `projectId`.

## Ids and namespacing

The host exposes each remote status provider and operation as
`{name}.{remoteId}` and always registers one status provider `{name}.bridge` per
configured plugin. A remote id must be 1-64 ASCII letters, digits, dots,
underscores, or hyphens and start with a letter or digit. The status provider id
`bridge` is reserved (compared case-insensitively) because it would collide with
`{name}.bridge`.

The host excludes a capability that violates the id grammar or is a duplicate
(ordinal case-insensitive), and excludes a whole operation that carries an
invalid required or optional field or parameter. Each exclusion is reported as a
`Warning` signal on `{name}.bridge`, and the remaining capabilities still
register.

> **Collision with built-in ids.** A plugin `name` equal to a built-in id prefix
> can collide with a built-in id. For example, a plugin named `http-endpoint`
> that exposes a remote id that composes to an existing built-in id fails
> startup. Choose names that do not shadow a built-in id.

The host fails startup when two registered operations, or two registered status
providers — built-in or external — expose ids that are equal under ordinal
case-insensitive comparison.

## Failure behavior

The host stays available when a plugin is unreachable or returns invalid
capabilities at startup. A failed plugin registers no operations and no remote
status providers, and its `{name}.bridge` provider reports `Warning` with the
fixed signal id `unreachable` or `invalid-capabilities`.

Every external call is bounded by a fixed 10-second timeout and is **not
retried**. A failed status call returns a `Warning` snapshot with the fixed
signal id `status-call-failed` instead of throwing. A failed execute call is
recorded in operation history with a stable bridge error code:

- `external_operation_timeout` — the bridge timed out; the remote outcome is
  unknown;
- `external_operation_transport_failed` — a connection or protocol failure;
- `external_operation_unexpected_status` — a non-2xx response;
- `external_operation_invalid_response` — an unparseable body, a missing field,
  an unknown status, a remote `errorCode` beginning with `external_operation_`,
  or a non-string `output` value.

A failed validate call returns one error string `"<code>: <detail>"` using the
same mapping, so the workbench rejects the execution with
`operation_validation_failed` and never calls execute. Caller cancellation
propagates as `OperationCanceledException`; only the bridge's own timeout maps to
`external_operation_timeout`.

`requiresConfirmation` is carried through to `OperationDefinition` as metadata
only. The host does not enforce confirmation, authentication, or authorization in
this slice.
