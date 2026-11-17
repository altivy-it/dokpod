---
title: Requirements and compatibility
description: Engines, hosts, and versions supported in the Dokpod 0.8 line.
sidebar_position: 2
---

## Supported engines

| Engine | Minimum version | Socket the agent looks for |
| --- | --- | --- |
| Docker Engine | 27.0.0 | `/run/docker.sock`, `/var/run/docker.sock` |
| Podman (Docker-compatible API) | 5.0.0 | `$XDG_RUNTIME_DIR/podman/podman.sock` (rootless), `/run/podman/podman.sock` (rootful) |

- The `DOKPOD_AGENT_DOCKER_SOCKET` variable always wins when set. Without it,
  the agent uses the first existing socket in the table order.
- The actual engine is detected from the `/version` response, not from the
  socket path.
- Hosts below the minimum version show as **Update** in the console. A version
  that cannot be parsed shows as **Unknown**.
- Podman pods and the Libpod API are out of this line, except for container
  creation with Podman secrets and health checks.

## Status by target

| Target | Status |
| --- | --- |
| Docker on Linux | validated on the real stack; waiting for release gates |
| Rootless Podman on Linux | validated on the real stack; waiting for release gates |
| Docker on Windows (agent as a Windows Service) | under qualification |
| Rootful Podman | not qualified |
| Kubernetes, Swarm, and Nomad | out of scope |

## Control plane

- Every component runs in containers: web console, BFF, API, PostgreSQL, and
  Keycloak.
- **Keycloak** is required. Dokpod uses its own realm (`dokpod`) and has no
  local user and no mode that bypasses authorization.
- The Dokpod PostgreSQL and the Keycloak PostgreSQL have separate databases,
  credentials, and backups.
- The agents' gRPC endpoint requires a server certificate issued by your
  organization's PKI or by a trusted authority.

## Guided installer

- Linux host with Docker Engine or Podman in **rootless** mode, with
  *linger* enabled for the user (`loginctl enable-linger <user>`).
- Public name resolving to the address browsers use.
- A free HTTPS port. Below `net.ipv4.ip_unprivileged_port_start`, only with a
  host nginx or with the administrator lowering that limit.

## Network

- Agents open an **outbound** connection to the control plane, over gRPC with
  mTLS. No inbound port is needed on managed hosts.
- The engine socket is never published on a TCP port.

## Compatibility between versions

- **Older agent with newer server**: supported. The server only uses what the
  agent announces.
- **Newer agent with older server**: the server drops capabilities it does
  not know and keeps the session.
- **Update order**: update the control plane first, then the agents.
