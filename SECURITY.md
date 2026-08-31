# Security Policy

## Reporting a vulnerability

- Report privately through this repository's **Security → Report a vulnerability** (GitHub private vulnerability reporting).
- Do not open a public issue, pull request, or discussion for a suspected vulnerability.
- Include impact and reproduction steps. Describe affected configuration in generic terms; never paste real credentials or environment details into a report.
- Wait for a maintainer acknowledgement before discussing the issue publicly.

## What must never be public

This repository is public. None of the following may appear in code, tests, fixtures, commit messages, branch names, issues, pull requests, docs, `.http` examples, CI logs, or screenshots:

- **Credentials**: passwords, tokens, personal access tokens, connection strings, client secrets, certificates, SSH keys.
- **Identity and tenancy identifiers**: tenant IDs, subscription IDs, client/application IDs, object IDs, account names.
- **Deployment topology**: real hostnames, FQDNs, IP addresses, ports, cluster or namespace names, storage accounts, and the queue, topic, or consumer-group names actually in use.
- **Internal references**: internal project, team, or system names, internal ticket IDs, internal URLs, employee names or corporate usernames.

Use generic placeholders instead — `https://dev.azure.com/your-org`, `example.com`, `<tenant-id>`, `orders-dlq`. Redact screenshots before attaching them, and prefer fabricated fixture data over captures of a real environment.

## If a real secret is committed or discovered

Deleting or hiding the value is **not** a fix: history, forks, clones, and caches keep it.

1. Treat the secret as compromised.
2. Rotate or revoke it in the source system first.
3. Report it privately (see above) so the exposure can be recorded.
4. Only then replace the value with a placeholder in the working tree.
5. Rewriting history is a separate, coordinated maintainer action and never a substitute for rotation.

## Configuration handling

- `.env` is tracked and contains placeholder defaults only.
- Real values belong in `.env.local` (gitignored) or the host's secret store, and are never committed.
- Build output, local databases (`*.db`), and other runtime data are ignored and must not be committed.
- Runtime endpoints, identities, and deployment topology stay outside this repository.

## Maintainer checklist

Repository settings that back the rules above (verify these in GitHub, they are not defined in source):

- Secret scanning with push protection enabled.
- Dependabot alerts and security updates enabled (update schedule lives in `.github/dependabot.yml`).
- Branch protection on `develop`: pull request required, CI status checks required, human approval required.
