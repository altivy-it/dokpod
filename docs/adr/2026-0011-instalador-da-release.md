# ADR 2026-0011: Instalador como imagem da release com bootstrap `install.sh`

**Status:** accepted (2026-11-08)

**Data:** 2026-11-08  
**Responsáveis:** zocatel (responsável pelo produto)  
**Origem:** IA assistida, tarefa T003 da [spec 033](../../specs/033-instalador-guiado/spec.md), autorizada pelo solicitante em 2026-11-08 ("tudo aprovado, siga com T002 a T005")  
**Revisor humano:** zocatel; aceite em 2026-11-08, registrado a pedido do solicitante, que declarou na sessão de 2026-11-08: "Aceito os ADRs, siga com T006 a T009".  
**Relacionado:** [spec 033](../../specs/033-instalador-guiado/spec.md), [plan 033](../../specs/033-instalador-guiado/plan.md) (decisões D-001, D-002, D-007, D-010, D-014 e D-015 de [research.md](../../specs/033-instalador-guiado/research.md)), [threat model 033](../../specs/033-instalador-guiado/threat-model.md) (aceito em 2026-11-08), [ADR 2026-0002](2026-0002-distribuicao-e-identidade.md), [ADR 2026-0010](2026-0010-modo-administrativo-da-api.md)

## Contexto

O Dokpod não tem caminho de instalação para quem está fora do projeto. A única stack (`deploy/e2e`) é de laboratório:
- depende da plataforma `altivy-identity` (rede `identity-global` e caminhos do AltivyNotes);
- roda com `ASPNETCORE_ENVIRONMENT=Development`;
- usa IPs fixos e a role administrativa do PostgreSQL.

Não há ferramenta de certificados, e a identidade do agente é inserida à mão.

A spec 033 pede um instalador único, guiado e idempotente para três cenários (tudo novo, PostgreSQL ou nginx existentes, Keycloak existente). Ele também precisa atualizar a versão e recriar componentes. As decisões do solicitante restringem o desenho:
- o host precisa só do engine de containers;
- o estado da instalação fica em SQLite;
- Docker e Podman, os dois só em modo rootless;
- nenhum SQL escrito no código.

A escolha é duradoura porque cria um artefato novo da release (imagem e asset assinados), define o caminho oficial de instalação e atualização e introduz um arquivo de estado local fora dos datastores do produto.

## Forças de decisão

- **Pré-requisito único**: o engine rootless com Compose (Docker ≥ 27 ou Podman ≥ 5).
- **Toolchains em containers**: o host só orquestra.
- **Cadeia de confiança**: do asset baixado até cada imagem usada, por digest e assinatura.
- **Reaproveitamento**: a reconciliação idempotente do Keycloak já existe (`configure-keycloak.ps1` e `provision-environment-authorization.ps1`).
- **Testabilidade**: o plano, a idempotência e a retomada precisam ser testáveis.
- **Compatibilidade**: com o deploy hook (`deploy/hooks/compose-deploy-hook.sh`) para atualizações sem migration.

## Opções consideradas

### Opção A: bootstrap POSIX mais imagem do instalador

- `install.sh`, asset da release, só detecta o engine rootless.
- Ele verifica com cosign a imagem `dokpod-installer` pelo digest gravado nele na publicação e a executa com o socket do usuário, `installDir` e `secretsDir`.
- A imagem traz PowerShell 7, Docker CLI com Compose v2, `openssl` e os scripts do Keycloak.

Vantagens:
- o host precisa só do engine;
- a lógica fica em PowerShell, a linguagem de orquestração do projeto, com Pester;
- os scripts do Keycloak são reaproveitados;
- a confiança vai do asset atestado ao digest.

Desvantagens:
- `install.sh` é shell POSIX fora de uma imagem, o que desvia da regra de scripts do projeto, que reserva Bash para runtime dentro de imagem Linux;
- o container do instalador recebe o socket do engine.

### Opção B: PowerShell instalado no host

Vantagens: sem bootstrap.

Desvantagens: cria pré-requisito além do engine.

### Opção C: aplicativo .NET dedicado

Vantagens: tipagem e testes .NET.

Desvantagens:
- reescreve a reconciliação do Keycloak, que já está em PowerShell e é o contrato do realm;
- dois lugares para manter o mesmo contrato.

### Opção D: estado em JSON

Vantagens: legível.

Desvantagens:
- sem transação nem integridade;
- reescrita a cada etapa;
- substituída por decisão do solicitante ("se for necessário manter estado da instalação use sqlite").

## Decisão

Adotar a **opção A**, com estes limites:
- **`install.sh`**:
  - não tem lógica de instalação;
  - recusa socket fora de `$XDG_RUNTIME_DIR`;
  - repassa ao container só as variáveis referenciadas em `{"env": ...}`, por nome;
  - executa o instalador e os containers avulsos com `--log-driver none` (L7 do threat model).
- **Engines**: só rootless (D-015). A detecção é pela API do engine (`name=rootless` ou `host.security.rootless`), e o modo rootful é recusado.
- **Orquestração**: sempre pelo Docker CLI com Compose v2 de dentro da imagem, contra o socket rootless, nos dois engines, sem `podman-compose` (D-002).
- **Imagens**: só `referência@digest` do `release-manifest.json`, verificadas com cosign antes do uso. Há modo offline com imagens e assinaturas exportadas (D-010).
- **Estado da instalação**: banco SQLite local (`installDir/state/installation.db`), sem segredos (D-014).
  - É um arquivo da ferramenta, não um datastore do produto: não tem servidor nem rede e não é consultado pelo Dokpod.
  - Acesso por `Microsoft.Data.Sqlite`, com schema e consultas em arquivos `.sql` (`deploy/installer/sql/state/`).
  - Schema forward-only por `PRAGMA user_version`.
  - `integrity_check` no início de cada execução, backup `VACUUM INTO` antes de cada execução que altera algo, e retenção de 200 execuções.
- **Segredos**: só em `secretsDir` (`0700`/`0600`), entregues aos serviços por arquivo em `/run/secrets`.
- **`psql`**: roda num container avulso da imagem PostgreSQL da matriz, com `PGPASSFILE` em tmpfs.
- **Desvio da regra de scripts**: o `install.sh` é o único script POSIX fora de imagem. A exceção vale só para ele, porque o host do usuário não tem PowerShell, e qualquer lógica nova vai para a imagem.

**Revisão**: reavaliar se o projeto adotar outro formato de distribuição (pacote do sistema, Kubernetes), se o suporte a engine rootful voltar a ser pedido, ou se o estado precisar ser compartilhado entre hosts.

## Consequências

### Positivas

- Instalação, atualização e recriação por um único comando, com o engine como único pré-requisito.
- A mesma cadeia de confiança da release (atestação, digest e cosign) cobre o instalador.
- O estado transacional permite retomada e `status` sem reler toda a instalação.

### Negativas e trade-offs

- Um artefato novo da release: a imagem `dokpod-installer` e os assets `install.sh` e `dokpod-sql-<versão>.tar.gz`. Todos precisam de build, SBOM, scan, assinatura e smoke.
- O container do instalador tem o poder do usuário dono do engine rootless durante a execução (R1, aceito).
- Rootless traz limites operacionais que o instalador só avisa:
  - portas abaixo de `net.ipv4.ip_unprivileged_port_start`;
  - IP de origem do cliente pelo encaminhador de portas;
  - *linger* para subir depois do reboot.
- A stack de laboratório (`deploy/e2e`) continua existindo para desenvolvimento, paralela ao instalador.

## Segurança e privacidade

- O threat model da spec 033 (B1 a B12) foi aceito em 2026-11-08, com as lacunas L1 a L7 aplicadas nas tarefas e os riscos R1 a R7 aceitos.
- Nenhum segredo vai para saída, relatório, estado, `docker inspect`, `ps` ou journal do host (SC-005, verificado por canário).
- As credenciais administrativas do PostgreSQL e do Keycloak existentes são transitórias e nunca gravadas.

## Dados, protocolo e compatibilidade

- **Estado e respostas**: o estado SQLite e o arquivo de respostas (`answers.schema.json`, `schemaVersion: 1`) são versionados e evoluem só para a frente.
- **Banco do Dokpod e protocolo**: sem mudanças além das do [ADR 2026-0010](2026-0010-modo-administrativo-da-api.md).
- **Deploy hook**: os arquivos gerados são compatíveis com ele (`deploy.env`), que continua válido para atualizações sem migration.

## Observabilidade e operação

- Relatório JSON por execução em `installDir/reports/`, sem segredos.
- Códigos de saída estáveis (0 a 7 e 10, [contrato](../../specs/033-instalador-guiado/contracts/installer-cli.md)).
- `status` mostra o estado por componente e o aviso de identidades de pacote sem conexão.
- Runbook novo `docs/runbooks/instalacao.md` (T054).

## Validação

- **Engines**: os cenários 1 a 9 do [quickstart](../../specs/033-instalador-guiado/quickstart.md) em hosts descartáveis com Docker rootless e Podman rootless, incluindo a recusa de engine rootful.
- **Tempo**: instalação completa em até 20 minutos sem download (SC-001) e reexecução convergida em até 2 minutos (SC-003).
- **Retomada**: interrupção em cada etapa converge para o mesmo estado (SC-004).
- **Assinatura**: imagem adulterada no modo offline é recusada com código 5 antes de qualquer alteração.

## Rollout e rollback

- **Rollout**: a imagem e o asset entram no `publish.yml` (T051) e saem primeiro numa candidata `X.Y.Z-rc.N` validada em `quality`.
- **Rollback**: o instalador não altera a stack de laboratório nem o deploy hook. Retirá-lo da release não afeta instalações já feitas, que seguem operáveis pelo Compose gerado e pelo deploy hook.

## Referências

- [Spec 033](../../specs/033-instalador-guiado/spec.md), [plan](../../specs/033-instalador-guiado/plan.md), [research](../../specs/033-instalador-guiado/research.md) e [threat model](../../specs/033-instalador-guiado/threat-model.md)
- [Distribuição e operação](../distribuicao.md)
- [Scripts e automação](../../.github/SCRIPTING.md)
- [Integridade da release](../release-integrity.md)
