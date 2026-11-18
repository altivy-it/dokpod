---
title: Installation
description: Install Dokpod with the guided installer on Linux with Docker or rootless Podman.
sidebar_position: 3
---

The guided installer takes a Linux host with Docker Engine or Podman in
**rootless** mode from scratch to a running Dokpod. It is idempotent: running
it again reconciles the installation instead of recreating it.

## Before you start

- Check the [installer requirements](requisitos.md#guided-installer).
- Download `install.sh` and `release-manifest.json` from the release, or the
  `--offline` package. `install.sh` checks the digest and signature of every
  image before any effect and stops with exit code 5 when something does not
  match.

## Scenarios

| Scenario | Answers | What the installer brings up |
| --- | --- | --- |
| Everything new | `postgres.mode: provision`, `keycloak.mode: provision`, `edge.mode: provision` | PostgreSQL, Keycloak, nginx edge, API, BFF, web, and the local agent |
| Existing PostgreSQL and/or nginx | `postgres.mode: existing-create` or `existing-prepared`; `edge.mode: existing` | the same, without the database and/or the edge |
| Existing Keycloak | `keycloak.mode: existing` | the same, without Keycloak and its database |

The combinations are valid. What is already yours (`existing`) is never
recreated or removed.

## Run

Without `--answers`, guided mode asks everything in the terminal. With an
answers file, nothing is asked, and a missing required answer ends with exit
code 2.

```bash
sh install.sh plan --answers answers.yaml        # shows the plan, no effect
sh install.sh install --answers answers.yaml --yes
```

Secrets never go into the answers file: each password is
`{ file: <path> }` or `{ env: <variable> }`. The administrative credentials of
an existing PostgreSQL or Keycloak can be left out; in that case, the terminal
asks for them, and they are used only in that run.

`--yes` confirms only the normal plan. Migrations, backup, overlapping SQL, and
data removal have their own confirmation.

## DBA-reviewed migrations

1. `sh install.sh migrations plan` lists the pending migrations without
   touching the database.
2. `sh install.sh migrations export --out pending.sql` generates the exact SQL.
   The DBA reviews it and applies it with
   `psql -v ON_ERROR_STOP=1 -f pending.sql`.
3. `install` or `upgrade` with `--manual-migrations` pauses at that step with
   exit code 10. After the SQL is applied, the same command resumes and checks
   the schema.

## Certificates

`sh install.sh certificates check` lists the validity of the CA, the edge, the
API, and each agent. Close to expiry (30 days by default), the installer
suggests the action: `recreate pki:<item>` with the local CA, a new package for
a remote host, or just the warning for an organization certificate.

## Uninstall

The installer does not remove data on its own. To uninstall:

1. Take the [backup](atualizacao-e-backup.md) of the database and Keycloak.
2. Stop and remove the containers, keeping the volumes:
   `docker compose -f <installDir>/compose.yaml -p dokpod down`.
3. Only if you want to delete the data, remove the Dokpod volumes.
4. Delete `installDir` and `secretsDir`. `secretsDir` holds the CA key, the
   passwords, and the connections.

PostgreSQL, Keycloak, and nginx that were already yours are not touched.
