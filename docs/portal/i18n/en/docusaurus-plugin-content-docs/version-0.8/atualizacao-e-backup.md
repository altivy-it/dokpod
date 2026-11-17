---
title: Updates and backup
description: Verified backup, update, and rollback of Dokpod in the 0.8 line.
sidebar_position: 6
---

## Principles

- Database migrations are **forward-only**. Rollback swaps images and never
  reverts migrations, because that would erase audit evidence.
- Rollback is direct only between versions **without a new migration**.
  Otherwise, restore the backup taken before the update.
- Backups contain personal data, the audit trail, and the Keycloak realm.
  Keep them outside the repository, with restricted access.

## Backup

1. Open a maintenance window and stop the API and the BFF, so the backup sees
   a consistent state.
2. Take a full database dump in custom format:

   ```bash
   podman exec <postgres> sh -c 'pg_dump -U "$POSTGRES_USER" -d <database> -Fc -Z 6' > dokpod.dump
   ```

3. Start the API again and then the BFF.

## Verified restore

1. Restore into a temporary database, never over the source:

   ```bash
   podman exec <postgres> sh -c 'createdb -U "$POSTGRES_USER" dokpod_restore'
   podman exec -i <postgres> sh -c 'pg_restore -U "$POSTGRES_USER" -d dokpod_restore --exit-on-error' < dokpod.dump
   ```

2. Compare the copy with the source: audit count and digest, migration
   history, and row counts of the main tables.
3. Start a temporary API pointing to the copy. It must answer 200 on
   `/health/ready`.
4. Remove the temporary API, the test database, and the exercise dump.

## Update with the installer

```bash
sh install.sh upgrade --version X.Y.Z --answers answers.yaml
```

- Only to a version higher than the installed one.
- Images are checked by digest and signature before any effect, and the
  control plane starts before the local agent.
- On a database that already has data, migrations require a backup first.
  With the PostgreSQL provisioned by the installer, it runs `pg_dump` into
  `installDir/backups/`.
- At the end, every Dokpod service must show the requested version in the
  `org.opencontainers.image.version` label.

## Manual update and rollback

With one instance of each service, the console is unavailable during the
switch (about 30 seconds in the lab).

1. Before starting, record the audit count, the environment count, and the
   identity of each agent.
2. Swap the `api`, `bff`, and `web` images and recreate the containers. On
   Podman, remove the BFF before the API.
3. Check, in this order:
   1. healthy containers, with the expected images;
   2. console responding through the edge;
   3. each agent reconnected, with an updated inventory;
   4. audit with the same count as before;
   5. no severe error in the API log.
4. To roll back, repeat the procedure with the previous version's images.

Always update the control plane before the agents.
