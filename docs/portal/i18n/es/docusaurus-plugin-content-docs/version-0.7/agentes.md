---
title: Agentes
description: Configuración de los agentes Linux y Windows de Dokpod en la línea 0.7.
sidebar_position: 4
---

El agente es el único componente que habla con el motor de contenedores. Cada
host gestionado tiene el suyo, con identidad mTLS propia.

## Agente Linux

El agente Linux es una imagen OCI separada del plano de control. Monte solo el
socket del motor elegido y un volumen persistente para identidad y journal:

```text
/var/lib/dokpod-agent        # certificado, clave, journal y estado
/run/docker.sock             # ejemplo con Docker; configurable
```

Estas variables habilitan la conexión con el plano de control:

| Variable | Obligatoria | Función |
| --- | --- | --- |
| `DOKPOD_AGENT_CONTROL_PLANE_ENDPOINT` | para conexión remota | URL HTTPS del endpoint gRPC del plano de control |
| `DOKPOD_AGENT_ENVIRONMENT_ID` | con endpoint | UUID del entorno vinculado al certificado |
| `DOKPOD_AGENT_CLIENT_CERTIFICATE_PATH` | con endpoint | ruta del PFX; por defecto, `identity/agent.pfx` en el directorio de datos |
| `DOKPOD_AGENT_CLIENT_CERTIFICATE_PASSWORD` | cuando el PFX la exige | contraseña inyectada por un mecanismo de secretos |
| `DOKPOD_AGENT_SERVER_CA_CERTIFICATE_PATH` | no | raíces de confianza para el certificado de la API; sin ella, vale la confianza del sistema |
| `DOKPOD_AGENT_REGISTRY_AUTH_FILE` | no | `config.json` en formato Docker con credenciales de registros privados |
| `DOKPOD_AGENT_BIND_MOUNT_ALLOWLIST` | no | directorios aceptados como origen de bind mount, separados por `:` |

Sin `DOKPOD_AGENT_CONTROL_PLANE_ENDPOINT`, el agente solo hace inspección
local y no abre sesión remota.

### Registros privados

Las credenciales de registro quedan **solo en el host**. El agente lee el
archivo de `DOKPOD_AGENT_REGISTRY_AUTH_FILE` en cada descarga y nada va al
plano de control. Proteja el archivo con permiso `0600` para el usuario del
agente.

### Bind mounts

Los bind mounts están prohibidos por defecto. Solo se aceptan los directorios
exactos de la allowlist. El socket del motor y directorios del sistema como
`/`, `/etc`, `/proc`, `/sys`, `/dev`, `/run`, `/root`, `/var/lib/docker` y
`/var/lib/containers` siempre se rechazan.

## Agente Windows

El agente Windows es un Worker Service .NET autocontenido (`win-x64`) y no
necesita runtime .NET en el servidor. Directorios recomendados:

```text
%ProgramFiles%\Dokpod\Agent\       # binarios, solo lectura
%ProgramData%\Dokpod\Agent\        # configuración, certificados y journal
```

El script `deploy/agent/manage-windows-service.ps1` instala y gestiona el
servicio. Ejecute PowerShell como Administrador; `-DryRun` muestra las acciones
sin cambiar nada.

```powershell
./deploy/agent/manage-windows-service.ps1 -Action Install `
  -BinaryPath "$env:ProgramFiles\Dokpod\Agent\Dokpod.Agent.exe" `
  -ControlPlaneEndpoint 'https://controlplane.example:7443' `
  -EnvironmentId '<uuid-del-entorno>' `
  -DockerSocket 'docker_engine' `
  -ClientCertificatePath '<ruta>\dokpod-agent.pfx' `
  -DataDirectory "$env:ProgramData\Dokpod\Agent"

./deploy/agent/manage-windows-service.ps1 -Action Status
```

`-Action Remove` conserva el directorio de datos, la identidad y el journal.
Para borrarlos, pase `-RemoveData` y `-DataDirectory` explícitamente.

:::note
El agente Windows todavía está en calificación en la línea 0.7.
:::
