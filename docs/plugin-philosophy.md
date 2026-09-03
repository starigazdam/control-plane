# Plugin Philosophy

Control Plane uses an explicit plugin model focused on engineering actions, not generic administration.

## Principles
1. Add concrete operations, not operation builders.
2. Prefer opinionated defaults over broad configurability.
3. Keep status providers tightly aligned to decisions and actions.

## How to extend
1. Implement `IControlPlanePlugin` in a plugin project (`ControlPlane.Azure`, `ControlPlane.Kafka`, etc.).
2. Register each `IStatusProvider` and `IOperation` from that plugin via `IPluginRegistration`.
3. Add UI affordances as named actions (for example, `ReplayBillingDlq`) rather than filter-driven workflows.
4. Add a runnable `.http` example for the plugin — see
   [Plugin `.http` examples](plugin-http-examples.md). This is enforced by CI.

## Non-goals
1. No generic Kafka management UI.
2. No Azure Portal replacement.
3. No low-code workflow designer.
