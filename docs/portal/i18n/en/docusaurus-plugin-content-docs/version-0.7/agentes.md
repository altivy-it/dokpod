---
title: Agents
description: Configuration of the Dokpod Linux and Windows agents in the 0.7 line.
sidebar_position: 4
---

The agent is the only component that talks to the container engine. Each
managed host has its own, with its own mTLS identity.

## Linux agent

The Linux agent is an OCI image separate from the control plane. Mount only
the socket of the chosen engine and a persistent volume for identity and
journal:

```text
/var/lib/dokpod-agent        # certificate, key, journal, and state
/run/docker.sock             # Docker example; configurable
```

These variables enable the connection to the control plane:

| Variable | Required | Purpose |
| --- | --- | --- |
| `DOKPOD_AGENT_CONTROL_PLANE_ENDPOINT` | for remote connection | HTTPS URL of the control plane gRPC endpoint |
| `DOKPOD_AGENT_ENVIRONMENT_ID` | with endpoint | UUID of the environment bound to the certificate |
| `DOKPOD_AGENT_CLIENT_CERTIFICATE_PATH` | with endpoint | PFX path; defaults to `identity/agent.pfx` in the data directory |
| `DOKPOD_AGENT_CLIENT_CERTIFICATE_PASSWORD` | when the PFX requires it | password injected by a secrets mechanism |
| `DOKPOD_AGENT_SERVER_CA_CERTIFICATE_PATH` | no | trusted roots for the API certificate; without it, the system trust applies |
| `DOKPOD_AGENT_REGISTRY_AUTH_FILE` | no | Docker-format `config.json` with private registry credentials |
| `DOKPOD_AGENT_BIND_MOUNT_ALLOWLIST` | no | directories accepted as bind mount sources, separated by `:` |

Without `DOKPOD_AGENT_CONTROL_PLANE_ENDPOINT`, the agent only performs local
inspection and does not open a remote session.

### Private registries

Registry credentials stay **only on the host**. The agent reads the
`DOKPOD_AGENT_REGISTRY_AUTH_FILE` file on every pull, and nothing goes to the
control plane. Protect the file with `0600` permission for the agent user.

### Bind mounts

Bind mounts are forbidden by default. Only the exact directories in the
allowlist are accepted. The engine socket and system directories such as `/`,
`/etc`, `/proc`, `/sys`, `/dev`, `/run`, `/root`, `/var/lib/docker`, and
`/var/lib/containers` are always rejected.

## Windows agent

The Windows agent is a self-contained .NET Worker Service (`win-x64`) and does
not require a .NET runtime on the server. Recommended directories:

```text
%ProgramFiles%\Dokpod\Agent\       # binaries, read-only
%ProgramData%\Dokpod\Agent\        # configuration, certificates, and journal
```

The `deploy/agent/manage-windows-service.ps1` script installs and manages the
service. Run PowerShell as Administrator; `-DryRun` shows the actions without
changing anything.

```powershell
./deploy/agent/manage-windows-service.ps1 -Action Install `
  -BinaryPath "$env:ProgramFiles\Dokpod\Agent\Dokpod.Agent.exe" `
  -ControlPlaneEndpoint 'https://controlplane.example:7443' `
  -EnvironmentId '<environment-uuid>' `
  -DockerSocket 'docker_engine' `
  -ClientCertificatePath '<path>\dokpod-agent.pfx' `
  -DataDirectory "$env:ProgramData\Dokpod\Agent"

./deploy/agent/manage-windows-service.ps1 -Action Status
```

`-Action Remove` keeps the data directory, identity, and journal. To delete
them, pass `-RemoveData` and `-DataDirectory` explicitly.

:::note
The Windows agent is still under qualification in the 0.7 line.
:::
