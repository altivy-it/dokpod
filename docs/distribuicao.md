# Distribuição e operação

## Princípio

O plano de controle e o frontend do Dokpod sempre executam em containers. A única distribuição nativa prevista é o agente Windows, publicado como Worker Service self-contained para não exigir runtime .NET instalado no host.

## Matriz de artefatos

| Componente | Artefato | Execução |
| --- | --- | --- |
| Web Angular | imagem OCI Linux com assets estáticos e proxy | container |
| BFF ASP.NET Core | imagem OCI Linux | container |
| API ASP.NET Core | imagem OCI Linux | container |
| Agente Linux | imagem OCI Linux | container com socket local explícito |
| Agente Windows | pacote self-contained assinado por RID | Windows Service |
| Keycloak | imagem fixada por versão e digest | container ou serviço externo homologado |
| PostgreSQL | imagem fixada por versão e digest ou serviço externo homologado | banco do Dokpod separado do banco do Keycloak |

Build, testes integrados e execução local do backend/frontend usam toolchains e serviços containerizados. O fluxo oficial não depende de SDK .NET, Node.js, Angular CLI ou pnpm instalados globalmente no host.

## Imagens base e toolchains

O baseline inicial usa imagens mantidas no projeto público [`lzocateli/containers`](https://github.com/lzocateli/containers). Dockerfiles do Dokpod referenciam a tag exata abaixo durante desenvolvimento e recebem o digest aprovado como argumento de build na release. O manifesto da release registra nome, tag e digest efetivamente utilizados; `latest` é proibida.

| Uso no Dokpod | Imagem de referência | Papel |
| --- | --- | --- |
| restore, build, testes e publicação .NET | `lzocateli/dotnet-sdk:10.0.402-noble-v1` | estágio de build da API, BFF e agentes |
| runtime da API, BFF e agente Linux | `lzocateli/dotnet-aspnet:10.0.13-noble-v1` | base dos artefatos ASP.NET Core e do host do agente |
| comandos oficiais do frontend | `lzocateli/angular-cli:22.2.4-node24.22.1-bookworm-v1` | install, lint, typecheck, testes e build Angular |
| toolchain JavaScript de apoio | `lzocateli/node:24.15.0-bookworm` | base do Angular CLI e tarefas Node sem Angular CLI |
| runtime do web e proxy público | `lzocateli/nginx:1.28.0-bookworm-v1` | base da imagem final com assets Angular e configuração Dokpod |
| testes de carga | `lzocateli/k6:2.1.0-node24.15.0-bookworm` | cenários HTTP, SignalR e capacidade do plano de controle |
| testes E2E black-box | `lzocateli/playwright-e2e:0.1.0` | pytest/Playwright contra o deployment containerizado |
| detecção de secrets | `lzocateli/gitleaks:8.30.1` | workspace, diff e histórico no CI |
| identidade | `lzocateli/keycloak:26.7.0` | serviço Keycloak homologado pelo Dokpod |
| persistência | `lzocateli/postgresql:18.4-pgvector0.8.6-bookworm` | mesma imagem adotada pelo AltivyNotes, para instâncias independentes do Dokpod e Keycloak |

A imagem PostgreSQL inclui pgvector 0.8.6, mas o Dokpod não habilita nem depende dessa extensão. API, BFF, agente Linux e web produzem imagens próprias `dokpod/*`; as imagens acima são bases, toolchains ou serviços de infraestrutura, não substitutos dos artefatos do produto.

Versões desta tabela podem receber atualização de patch sem novo ADR quando contratos, major versions e fronteiras permanecem iguais. A atualização exige build, SBOM, scan, testes do recorte, compatibilidade e atualização desta tabela. Mudança de major version, troca de distribuição base ou substituição de tecnologia exige revisão arquitetural proporcional.

## Plano de controle containerizado

- API, BFF e web possuem Dockerfiles independentes e multi-stage.
- Os estágios .NET usam `dotnet-sdk` para build e `dotnet-aspnet` para runtime; o estágio Angular usa `angular-cli`, e a imagem final web usa `nginx` sem carregar Node.js ou Angular CLI.
- Containers executam com usuário não root, filesystem read-only e capabilities removidas quando tecnicamente possível.
- A imagem final web substitui a configuração genérica do NGINX, escuta em porta não privilegiada e prepara PID, cache e temporários para o UID não root do Dokpod.
- Somente o proxy público recebe tráfego externo; API, BFF, Keycloak e bancos usam redes internas conforme a topologia.
- Secrets entram por arquivo montado ou provider seguro, nunca por camada de imagem ou argumento de build.
- Health checks distinguem startup, liveness e readiness.
- Imagens são fixadas por digest na release, possuem labels OCI, SBOM, provenance e assinatura.
- Dados PostgreSQL e Keycloak possuem volumes, credenciais, backups e testes de restauração independentes.

## Agente Linux

O agente Linux é uma imagem OCI separada do plano de controle. O operador monta somente o socket do engine selecionado e um volume persistente para identidade e journal.

Diretórios lógicos:

```text
/app                         # binário imutável
/var/lib/dokpod-agent        # certificado, chave, journal e estado
/run/docker.sock             # exemplo Docker; configurável e validado
```

O socket nunca é publicado em porta TCP nem compartilhado com outro componente Dokpod. Podman rootless exige UID, socket e regras SELinux documentados para a distribuição homologada.

### Engines homologados (spec 008)

| Engine | Versão mínima homologada | Configuração (`EngineSupport`) | Socket padrão procurado |
| --- | --- | --- | --- |
| Docker Engine | 27.0.0 | `DockerMinimumVersion` | `/run/docker.sock`, `/var/run/docker.sock` |
| Podman (API compatível com Docker) | 5.0.0 | `PodmanMinimumVersion` | `$XDG_RUNTIME_DIR/podman/podman.sock` (rootless), `/run/podman/podman.sock` (rootful) |

- `DOKPOD_AGENT_DOCKER_SOCKET`, quando definido, sempre vence; sem ele o agente usa o primeiro socket existente na ordem acima. O engine efetivo é detectado por `/version` (`Components` contém "Podman Engine"), não pelo caminho.
- Hosts abaixo da versão mínima aparecem como "Atualizar" e geram `engine.version_outdated`; versão não interpretável aparece como "Desconhecido".
- Podman é qualificado pela API compatível com testes reais (`backend/tests/Dokpod.Agent.Podman.IntegrationTests`, `DOKPOD_TEST_PODMAN_SOCKET`). Diferenças tratadas pelo agente: `stop` de container pausado retoma antes de parar (o Podman recusa) e conflitos de estado (HTTP 500 `container state improper`) são mapeados como no Docker (`not_running`/`not_paused`). Pods e API Libpod não fazem parte desta entrega.
- Evidência atual: Podman 5.7.0 rootless em Ubuntu 24.04 (2026-10-13) e Docker Engine 28.5.2 (API 1.51) aninhado no Podman pelo perfil `docker-real` do stack de teste (spec 023, 2026-10-17): inventário, ações, pausa, logs, inspeção, métricas, terminal, criação, pull com registro privado, volumes, redes e registros. Docker instalado diretamente num host, Podman rootful e Windows: `NOT RUN`.

### Compatibilidade de versões e ordem de atualização (spec 023)

- **Agente anterior com servidor novo**: suportado. O servidor só usa o que o agente anuncia; inventários que o agente não publica aparecem como "inventário indisponível" (não com dados antigos gravados por uma versão mais nova). Validado por 10 minutos com o agente da spec 020 e o servidor da 022.
- **Agente novo com servidor anterior**: a partir desta versão, o servidor descarta capabilities e mensagens que não conhece e mantém a sessão. Servidores **anteriores** a esta versão recusam a sessão de qualquer agente mais novo (`agent_hello_invalid`).
- **Ordem de atualização**: atualize primeiro o plano de controle e depois os agentes. Até que todos os servidores estejam nesta versão ou posterior, não atualize um agente antes do servidor que ele usa.

O stream operacional com o plano de controle é habilitado quando estas variáveis são injetadas no processo:

| Variável | Obrigatória | Finalidade |
| --- | --- | --- |
| `DOKPOD_AGENT_CONTROL_PLANE_ENDPOINT` | para conexão remota | URL HTTPS do endpoint gRPC do plano de controle |
| `DOKPOD_AGENT_ENVIRONMENT_ID` | com endpoint configurado | UUID do ambiente associado ao certificado |
| `DOKPOD_AGENT_CLIENT_CERTIFICATE_PATH` | com endpoint configurado | caminho absoluto do PFX; padrão `identity/agent.pfx` sob o diretório de dados |
| `DOKPOD_AGENT_CLIENT_CERTIFICATE_PASSWORD` | quando o PFX exigir | senha injetada pelo mecanismo externo de secrets, nunca por argumento ou arquivo versionado |
| `DOKPOD_AGENT_SERVER_CA_CERTIFICATE_PATH` | não | raízes confiáveis para o certificado da API (PEM, uma ou mais, ou DER com uma); sem ela, vale a confiança do sistema operacional. Em ambos os casos o nome do endpoint precisa estar no certificado da API |

| `DOKPOD_AGENT_REGISTRY_AUTH_FILE` | não | caminho absoluto de um `config.json` no formato Docker (`auths`) com credenciais de registros privados para o pull (spec 012) |

| `DOKPOD_AGENT_BIND_MOUNT_ALLOWLIST` | não | diretórios absolutos (separados por `:`) aceitos como origem de bind mount na criação de container (spec 013); só o diretório exato é aceito, sem subcaminhos, e diretórios aninhados impedem o agente de iniciar (proteção contra escape por link simbólico); vazio = somente volumes nomeados |

### Terminal interativo (spec 014)

- O agente abre o RPC `Exec` (mesmo canal mTLS) quando o servidor pede uma sessão e executa um único exec fixo: `/bin/sh -c` escolhe `/bin/bash` ou `/bin/sh` e anuncia o PID do shell. O engine **não encerra execs ao fechar a conexão** (comportamento observado no Podman 5.7); por isso o agente encerra o shell com `SIGHUP` (e `SIGKILL` após 5 s) por um exec destacado `kill` e confirma `Running=false`. Imagens sem `/bin/sh` recebem `shell_not_available`.
- Execs ativos ficam em `<diretório de dados>/exec-sessions.json`; após reinício o agente encerra os que ainda rodam e publica `container.exec_orphan_suspected` quando não consegue.
- A conexão crua com o socket (`EngineRawConnector`) é usada somente para `/exec/{id}/start`. `CAPABILITY_CONTAINER_EXEC` só é anunciada no Linux.
- A API exige WebSocket para o terminal e usa keepalive do hub de 15 s/timeout de 30 s. Configuração em `Terminal__*` (ociosidade, duração, limites, estágios com justificativa, MFA recente).
- Evidência: Podman 5.7.0 rootless (eco, resize, término por `exit`, fechamento, queda do cliente, container parado, recuperação de órfãos) e teste .NET ponta a ponta API + agente + RPC `Exec` com mTLS. Docker real e carga de 4 sessões (SC-004): `NOT RUN`.

### Criação de container (spec 013)

- O agente cria e inicia containers pela API compatível; no Podman, segredos do Podman (`secret_env`) e health check com reinício (`health_check_on_failure_action`) usam `POST /libpod/containers/create`. A capability `CAPABILITY_CONTAINER_CREATE` só é anunciada no Linux.
- Segredos "do cofre": segredos do Podman (`podman secret create`) ou arquivos em `<diretório de dados>/secrets/<nome>` (nome `[a-zA-Z0-9][a-zA-Z0-9_.-]*`, até 32 KiB, sem link simbólico e sem leitura por "outros", ex.: `chmod 600`). O agente lê o arquivo somente no momento da criação; o valor não vai ao plano de controle.
- Bind mounts ficam proibidos por padrão; o socket do engine e `/`, `/etc`, `/proc`, `/sys`, `/dev`, `/run`, `/var/run`, `/boot`, `/root`, `/var/lib/docker` e `/var/lib/containers` são sempre recusados.
- A API cifra os valores de variáveis com key ring próprio de Data Protection (`DataProtection__KeysPath`, volume `dokpod-api-keys` no compose); proteja o volume como segredo e faça backup junto com o banco. Perder o key ring faz comandos ainda não executados falharem com `parameters_unavailable`; nenhum container é criado sem os valores.
- Evidência: criação real em Podman 5.7.0 rootless (configuração aplicada, Libpod com segredo e health check com reinício, conflito de porta com rollback e reconciliação por label) em `Dokpod.Agent.Podman.IntegrationTests`. Docker real e Podman rootful: `NOT RUN`.

### Download de imagens (spec 012)

- O pull é executado pelo agente no engine local (`POST /images/create`), com prazo do comando (padrão de 10 min na interface, máximo de 30 min).
- Credenciais de registro ficam **somente no host**: o agente lê `DOKPOD_AGENT_REGISTRY_AUTH_FILE` a cada pull e envia o cabeçalho `X-Registry-Auth` ao engine local. Nada é enviado ao plano de controle, gravado no PostgreSQL ou registrado em log. Proteja o arquivo com permissão `0600` para o usuário do agente; sem o arquivo, o engine usa a própria configuração (por exemplo `~/.docker/config.json` ou `auth.json` do Podman).
- Lista opcional de registros permitidos por estágio na API: `Images__AllowedRegistriesByStage__production__0=registry.empresa.com` (chaves `production`, `staging`, `development`, `none`); sem lista, qualquer registro é aceito. Desde a spec 020 essa configuração é o **valor inicial**: depois que a política de um estágio é salva em Infraestrutura → Registros (com auditoria), vale a lista do console para aquele estágio.
- Retenção de comandos: `Commands__Retention` (padrão `90.00:00:00`).
- Evidência: pull real de `quay.io/podman/hello` e falha `image_not_found` em Podman 5.7.0 rootless (`Dokpod.Agent.Podman.IntegrationTests`, `DOKPOD_TEST_REGISTRY_NETWORK=1`). Registro privado de laboratório (`registry:2`, D-004) e Docker real: `NOT RUN`.

Sem `DOKPOD_AGENT_CONTROL_PLANE_ENDPOINT`, o agente mantém somente a inspeção local e não abre sessão remota. O certificado e sua chave privada permanecem no volume protegido do agente; a cadeia do servidor deve ser confiável pelo sistema operacional.

## Agente Windows

O projeto `backend/apps/agent` produz um Worker Service .NET 10. Cada release Windows é publicada com `--self-contained true` para um RID homologado, inicialmente candidato a `win-x64`. Não é necessário instalar runtime ou SDK .NET no servidor.

Separação de diretórios recomendada:

```text
%ProgramFiles%\Dokpod\Agent\       # binários versionados e somente leitura
%ProgramData%\Dokpod\Agent\        # configuração, certificados, chave e journal
%ProgramData%\Dokpod\Agent\logs\   # logs limitados e sem secrets
```

O serviço usa uma conta dedicada com menor privilégio. A matriz de suporte deve comprovar qual principal consegue abrir o named pipe do Docker. Usar `LocalSystem` somente quando tecnicamente indispensável, com risco documentado e aprovação explícita.

O pacote não contém bootstrap token, chave, certificado, hostname real ou credencial. A inscrição recebe o token por canal protegido e o elimina após consumo.

## Instalação e atualização Windows

O mecanismo exato de empacotamento será escolhido após o spike, mas deve cumprir:

1. verificar assinatura, hash, provenance e versão antes de instalar;
2. instalar binários em diretório versionado sem sobrescrever identidade ou journal;
3. registrar Windows Service com startup automático atrasado e recuperação limitada;
4. validar configuração, ACL, named pipe e conectividade antes de marcar readiness;
5. parar o serviço com timeout e shutdown gracioso;
6. ativar a nova versão por troca atômica;
7. restaurar a versão anterior sem reemitir identidade se o health check falhar;
8. preservar logs e evidência de auditoria da atualização.

Desinstalação remove serviço e binários. Dados e identidade só são removidos por opção explícita, pois sua exclusão exige novo enrollment.

### Pacote e qualificação

`deploy/agent/publish-windows-agent.sh` gera o pacote self-contained `win-x64` em container, a partir de um `git archive` do commit, com SHA-256 e manifesto de versão e commit. A qualificação na máquina Windows segue o [runbook](runbooks/qualificacao-windows.md) (spec 006).

### Script de gerenciamento do Windows Service

O script `deploy/agent/manage-windows-service.ps1` recebe os dados da
instalação e gerencia o ciclo de vida do serviço. Execute o PowerShell como
Administrador para operações reais. `-DryRun` valida os parâmetros e mostra as
ações sem alterar o SCM ou o registro.

```powershell
$certificateDirectory = Join-Path $env:APPDATA 'Microsoft\UserSecrets\Dokpod\certs'
$agentDataDirectory = Join-Path $env:ProgramData 'Dokpod\Agent'
$agentBinary = Join-Path $env:ProgramFiles 'Dokpod\Agent\Dokpod.Agent.exe'

./deploy/agent/manage-windows-service.ps1 -Action Install `
	-FailureRestartDelaySeconds 60 `
	-BinaryPath $agentBinary `
	-ControlPlaneEndpoint 'https://controlplane.example:7443' `
	-EnvironmentId '<uuid-do-ambiente>' `
	-DockerSocket 'docker_engine' `
	-ClientCertificatePath (Join-Path $certificateDirectory 'dokpod-agent.pfx') `
	-ServerCaCertificatePath (Join-Path $certificateDirectory 'dokpod-dev-ca.crt') `
	-DataDirectory $agentDataDirectory

./deploy/agent/manage-windows-service.ps1 -Action Start
./deploy/agent/manage-windows-service.ps1 -Action Stop
./deploy/agent/manage-windows-service.ps1 -Action Restart
./deploy/agent/manage-windows-service.ps1 -Action Status
./deploy/agent/manage-windows-service.ps1 -Action Remove -Confirm
```

`Remove` preserva o diretório de dados, certificados, identidade e journal.
Para apagar o diretório de dados é necessário informar `-RemoveData` e
`-DataDirectory` explicitamente. A remoção de dados exige confirmação e não
remove certificados fora desse diretório.

Durante `Install`, o script configura inicialização atrasada e duas tentativas
de reinício automático após falha. O intervalo é controlado por
`-FailureRestartDelaySeconds`; depois das tentativas configuradas, o SCM não
reinicia indefinidamente o processo.

## Configuração

Configuração não secreta pode vir de arquivo montado no Linux ou arquivo protegido no Windows. Secrets e chaves privadas usam arquivos com permissões mínimas ou provider seguro. Variáveis de ambiente são aceitas somente quando o ambiente operacional impedir exposição por inspeção de processo e houver justificativa.

O endpoint gRPC do plano de controle exige um certificado de servidor explícito em `DOKPOD_API_CERTIFICATE_PATH`; a senha opcional do PFX vem de `DOKPOD_API_CERTIFICATE_PASSWORD`. Em produção, use certificado emitido pela PKI da organização ou por uma autoridade confiável, montado como arquivo somente leitura. Não gere certificado autoassinado no startup e não dependa do certificado de desenvolvimento do SDK. Em testes automatizados, certificados autoassinados efêmeros e restritos ao processo são aceitáveis. Em laboratório, uma CA local controlada pode emitir os certificados de servidor e agente, desde que sua raiz seja distribuída explicitamente aos participantes e nunca seja desabilitada a validação da cadeia. As raízes aceitas para os agentes ficam em `DOKPOD_API_CLIENT_CA_CERTIFICATE_PATH`; esse arquivo e o `DOKPOD_AGENT_SERVER_CA_CERTIFICATE_PATH` aceitam um bundle PEM com a raiz antiga e a nova durante a [troca da CA](runbooks/troca-da-ca-dos-agentes.md).

Campos mínimos:

- URL canônica do plano de controle;
- caminho ou identificador do socket/named pipe permitido;
- engine esperado;
- diretório persistente;
- limites de concorrência, timeout e tamanho;
- autoridade/certificado confiável para mTLS.

## Gates de release

- build reproduzível das quatro imagens e do pacote Windows;
- bases e toolchains correspondem à matriz publicada, sem `latest`, e o manifesto da release contém seus digests;
- execução do backend/frontend apenas por containers;
- smoke test do agente Linux contra engine real;
- instalação do agente Windows em host limpo sem runtime .NET;
- teste de conta de serviço, ACL, reinício, recuperação, atualização e rollback;
- geração e verificação de SBOM, provenance, assinatura e vulnerabilidades;
- compatibilidade servidor N com agentes N e N-1;
- backup e restauração dos bancos do Dokpod e Keycloak.
