---
title: Introduction
description: What Dokpod is, how it works, and the status of the 0.7 line.
sidebar_position: 1
slug: /
---

**Dokpod** is a self-hosted web console to discover, observe, and operate
**Docker** and **Podman** containers across many **Linux** and **Windows**
servers, without Kubernetes and without a vendor cloud.

:::warning 0.7 line in preview
0.7 is a **preview** release. The console was validated end to end with
Docker and rootless Podman on Linux, but there is no stable release and no
production installation package yet. Use it for evaluation only.
:::

## What Dokpod does

**Container operations**

- Start, stop, pause, restart, and remove containers, including in bulk.
- Inspection, live logs with masked secrets, and an interactive terminal in
  the browser, with the session audited.
- Guided container creation and image pulls from allowed registries.
- Volumes, networks, and Podman pods, with usage checks before removal.
- CPU and memory metrics per host and per container, and real-time engine
  events.

**Governance and security**

- Permissions per environment and per action, decided by Keycloak on every
  request. Users without permission do not even see the environment.
- Sign-in with LDAP, Active Directory, or OIDC through Keycloak federation.
  Dokpod does not store passwords.
- Stage policies (Production, Staging, and Development): mandatory
  justification and allowed image registries.
- Usage and state alerts with silencing, and a filterable audit trail that
  can be exported as CSV.

## How it works

The control plane (web console, BFF, API, PostgreSQL, and Keycloak) runs in
containers in your environment. On each managed server, a lightweight
**agent** talks only to the local engine and opens an **outbound gRPC
connection with mTLS** to the control plane.

- No Docker or Podman port is exposed on the network.
- The control plane never reaches the engine of a remote host: only the agent
  runs commands, always versioned domain commands, never a generic proxy to
  the engine API.
- On Linux, the agent runs in a container. On Windows, it runs as a
  self-contained Windows Service, with no .NET runtime installed on the host.
- If the control plane or the agent goes down, containers keep running, and
  the state is reconciled when the connection returns.

## Editions

**Dokpod Community Edition** is free and open source under the
`AGPL-3.0-only` license, with no license key and no limit on servers, users,
or containers. Security, mTLS, per-environment and per-action authorization,
and auditing are part of the Community Edition.

## Next steps

1. Check the [requirements and compatibility](requisitos.md).
2. See how to [evaluate the 0.7 line](instalacao.md).
3. Learn about the [agent configuration](agentes.md).
