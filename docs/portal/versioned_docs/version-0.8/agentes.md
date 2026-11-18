---
title: Agentes
description: Configuração dos agentes Linux e Windows do Dokpod na linha 0.8.
sidebar_position: 5
---

O agente é o único componente que fala com o engine de containers. Cada host
gerenciado tem o seu, com identidade mTLS própria.

## Agente Linux

O agente Linux é uma imagem OCI separada do plano de controle. Monte somente o
socket do engine escolhido e um volume persistente para identidade e journal:

```text
/var/lib/dokpod-agent        # certificado, chave, journal e estado
/run/docker.sock             # exemplo com Docker; configurável
```

A conexão com o plano de controle é habilitada por estas variáveis:

| Variável | Obrigatória | Finalidade |
| --- | --- | --- |
| `DOKPOD_AGENT_CONTROL_PLANE_ENDPOINT` | para conexão remota | URL HTTPS do endpoint gRPC do plano de controle |
| `DOKPOD_AGENT_ENVIRONMENT_ID` | com endpoint | UUID do ambiente associado ao certificado |
| `DOKPOD_AGENT_CLIENT_CERTIFICATE_PATH` | com endpoint | caminho do PFX; padrão `identity/agent.pfx` no diretório de dados |
| `DOKPOD_AGENT_CLIENT_CERTIFICATE_PASSWORD` | quando o PFX exigir | senha injetada por um mecanismo de secrets |
| `DOKPOD_AGENT_SERVER_CA_CERTIFICATE_PATH` | não | raízes confiáveis para o certificado da API; sem ela, vale a confiança do sistema |
| `DOKPOD_AGENT_REGISTRY_AUTH_FILE` | não | `config.json` no formato Docker com credenciais de registros privados |
| `DOKPOD_AGENT_BIND_MOUNT_ALLOWLIST` | não | diretórios aceitos como origem de bind mount, separados por `:` |

Sem `DOKPOD_AGENT_CONTROL_PLANE_ENDPOINT`, o agente faz só a inspeção local e
não abre sessão remota.

### Registros privados

As credenciais de registro ficam **somente no host**. O agente lê o arquivo de
`DOKPOD_AGENT_REGISTRY_AUTH_FILE` a cada pull, e nada vai para o plano de
controle. Proteja o arquivo com permissão `0600` para o usuário do agente.

### Bind mounts

Bind mounts são proibidos por padrão. Só os diretórios exatos da allowlist são
aceitos. O socket do engine e diretórios de sistema como `/`, `/etc`, `/proc`,
`/sys`, `/dev`, `/run`, `/root`, `/var/lib/docker` e `/var/lib/containers`
são sempre recusados.

## Agente Windows

O agente Windows é um Worker Service .NET self-contained (`win-x64`) e não
exige runtime .NET no servidor. Diretórios recomendados:

```text
%ProgramFiles%\Dokpod\Agent\       # binários, somente leitura
%ProgramData%\Dokpod\Agent\        # configuração, certificados e journal
```

O script `deploy/agent/manage-windows-service.ps1` instala e gerencia o
serviço. Execute o PowerShell como Administrador; `-DryRun` mostra as ações sem
alterar nada.

```powershell
./deploy/agent/manage-windows-service.ps1 -Action Install `
  -BinaryPath "$env:ProgramFiles\Dokpod\Agent\Dokpod.Agent.exe" `
  -ControlPlaneEndpoint 'https://controlplane.example:7443' `
  -EnvironmentId '<uuid-do-ambiente>' `
  -DockerSocket 'docker_engine' `
  -ClientCertificatePath '<caminho>\dokpod-agent.pfx' `
  -DataDirectory "$env:ProgramData\Dokpod\Agent"

./deploy/agent/manage-windows-service.ps1 -Action Status
```

`-Action Remove` preserva o diretório de dados, a identidade e o journal. Para
apagá-los, informe `-RemoveData` e `-DataDirectory` explicitamente.

:::note
O agente Windows ainda está em qualificação na linha 0.8.
:::
