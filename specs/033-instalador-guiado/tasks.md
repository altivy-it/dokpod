---

description: "Tarefas da implementação do instalador guiado do Dokpod"
---

# Tarefas: Instalador guiado do Dokpod

**Entrada**: Documentos de design em `/specs/033-instalador-guiado/`

> Redija todas as tarefas e descrições em português brasileiro (pt-BR), preservando identificadores, comandos e caminhos técnicos.

**Pré-requisitos**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md),
[contracts/installer-cli.md](contracts/installer-cli.md), [contracts/admin-commands.md](contracts/admin-commands.md),
[contracts/migration-script.md](contracts/migration-script.md), [contracts/answers.schema.json](contracts/answers.schema.json),
[quickstart.md](quickstart.md)

**Testes**: obrigatórios (constituição, princípio VI):
- Pester na imagem do instalador;
- testes .NET do modo administrativo contra PostgreSQL 17.6 real;
- ponta a ponta em hosts descartáveis com Docker Engine rootless e Podman rootless reais, navegador pela borda nginx;
- canário de segredos.

**Organização**: agrupadas por história da spec para implementação e validação independentes.

**Gate**: nenhuma tarefa de código começa antes de T001 (threat model) aceito pelo revisor humano. `/speckit-implement`
exige autorização humana explícita.

## Formato: `[ID] [P?] [Story] Descrição`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência pendente)
- **[Story]**: história da spec (US1 a US5)

## Convenções de Caminhos

- Instalador: `deploy/installer/install.sh`, `deploy/installer/Dockerfile`, `deploy/installer/src/*.psm1`,
  `deploy/installer/src/Dokpod.Installer.ps1`, `deploy/installer/templates/`, `deploy/installer/tests/*.Tests.ps1`
- Modo administrativo: `backend/apps/Dokpod.ControlPlane.Api/Admin/`, testes em
  `backend/tests/Dokpod.ControlPlane.Api.Tests/Admin/`
- Aplicação e domínio: `backend/libs/Dokpod.ControlPlane.Application/Agents/`, `backend/libs/Dokpod.Domain/Auditing/`
- Scripts do Keycloak reaproveitados: `tools/scripts/configure-keycloak.ps1`, `tools/scripts/provision-environment-authorization.ps1`
- Evidências: `specs/033-instalador-guiado/validation.md`

---

## Fase 1: Preparação (Infraestrutura Compartilhada)

**Objetivo**: fronteira de segurança aceita, decisões duradouras propostas e dependências registradas antes de qualquer
código.

- [X] T001 Threat model em `specs/033-instalador-guiado/threat-model.md`, no formato de
  `specs/025-aviso-nova-versao-imagem/threat-model.md`. Cobrir:
  - o socket do engine montado no container do instalador;
  - as credenciais administrativas transitórias do PostgreSQL e do Keycloak (FR-008, FR-015);
  - o modo administrativo da imagem da API com a conexão de `dokpod_owner` (D-003, D-004);
  - a chave da CA local em `secretsDir` (FR-018);
  - o pacote de host e sua revogação (FR-025);
  - a confiança no `install.sh` e no digest do instalador (D-001, D-010);
  - o arquivo de respostas e as referências a segredos (FR-002);
  - a senha temporária do primeiro administrador (FR-014);
  - a recriação total (FR-029);
  - o catálogo SQL sobreposto, que é SQL do operador executado como `dokpod_owner` (FR-041, D-013): quem pode gravar em
    `installDir/sql-override/` e se uma reexecução reaplica algo sem nova confirmação;
  - o banco de estado SQLite (FR-044, D-014): adulteração de `installation.db` (por exemplo, `run_lock` ou
    `sql_overrides`), integridade e reconstrução a partir do observado.

  Registrar lacunas, riscos residuais e a decisão do revisor humano. Bloqueia todas as tarefas de código. Nota
  (2026-11-08): aceito pelo solicitante. A premissa central foi aceita com a restrição a engines rootless (D-015).
  As recomendações L1–L7 foram aceitas e aplicadas: L1 em T026 e T037, L2 e L6 em T046 e T027, L3 em T029, L4 em
  T030, L5 em T010 e T028, L7 em T024 e T053. Os riscos R1–R7 foram aceitos.
- [X] T002 [P] ADR `proposed` "Modo administrativo da imagem da API para migrations e bootstrap" em
  `docs/adr/2026-0010-modo-administrativo-da-api.md`, seguindo `.github/ADR_TEMPLATE.md`. Ele registra D-003 e D-004,
  as alternativas rejeitadas (bundle, imagem migrator, migração na inicialização, `--idempotent`, `INSERT` direto) e a
  relação com o enrollment futuro.
- [X] T003 [P] ADR `proposed` "Instalador como imagem da release com bootstrap `install.sh`" em
  `docs/adr/2026-0011-instalador-da-release.md`, com D-001, D-002, D-010 e o estado local em SQLite (D-014),
  registrado como arquivo da ferramenta e não datastore do produto.
- [X] T004 [P] Registrar na matriz de `docs/distribuicao.md` a imagem base do instalador e suas ferramentas. Para cada
  uma, conferir e anotar licença, versão exata e manutenção:
  - a imagem PowerShell 7.5 (candidata `mcr.microsoft.com/powershell:7.5-ubuntu-24.04`, MIT, ou equivalente em
    `lzocateli/containers`);
  - Docker CLI e plugin Compose v2 (Apache-2.0);
  - `openssl` e `postgresql-client`;
  - `Microsoft.Data.Sqlite` (MIT) e `SQLitePCLRaw.bundle_e_sqlite3` (Apache-2.0) para o estado em SQLite, que é de
    domínio público (D-014);
  - Pester 5.7.x (Apache-2.0);
  - PSScriptAnalyzer 1.24.x (MIT), para a análise estática dos módulos.

  Dependência sem licença verificada bloqueia T024. Nota (2026-11-08): registrado na seção "Instalador (spec 033)" de
  `docs/distribuicao.md`.
  - A imagem MCR 7.5 foi rejeitada (sem atualização desde janeiro de 2025). A base passou a ser
    `lzocateli/dotnet-aspnet:10.0.14-noble-v1` com PowerShell 7.6.9 por SHA-256.
  - `postgresql-client` saiu da imagem: o `psql` roda em container avulso da imagem PostgreSQL da matriz.
  - Versões fixadas: Pester 6.2.0, PSScriptAnalyzer 1.25.0, Docker CLI 29.9.2, Compose v5.6.1,
    `Microsoft.Data.Sqlite` 10.0.14, `SQLitePCLRaw.bundle_e_sqlite3` 2.1.13 e cosign v3.1.4.
- [X] T005 Esqueleto de `deploy/installer/`: `README.md`, `src/`, `templates/`, `tests/`, e
  `src/Dokpod.Installer.ps1` com `--help` sem efeitos colaterais e a lista de comandos de
  [installer-cli.md](contracts/installer-cli.md), seguindo `.github/instructions/script-authoring.instructions.md` e
  `.github/SCRIPTING.md`.
  Nota (2026-11-08): `deploy/installer/{README.md,src/Dokpod.Installer.ps1,sql/state/,templates/,tests/}`. Validado em
  container PowerShell 7: `--help`, `help` e `status --help` saem com 0 e `Get-Help -Full` funciona; comandos do contrato
  são recusados com código 2, apontando a tarefa que os entrega. O PSScriptAnalyzer 1.25.0 só aponta
  `PSUseBOMForUnicodeEncodedFile`, que contraria a convenção do repositório (scripts UTF-8 sem BOM). A T057 deve excluir
  essa regra nas configurações do analisador.

---

## Fase 2: Fundação (Pré-requisitos Bloqueadores)

**Objetivo**: modo administrativo da API, segredos por arquivo e núcleo do instalador (respostas, estado, plano, PKI,
segredos, imagens e renderização). Nenhuma história começa antes desta fase.

### Catálogo SQL (FR-039 a FR-041, D-013)

- [ ] T006 Catálogo SQL em `backend/libs/Dokpod.ControlPlane.Infrastructure/Sql/`:
  - `manifest.json` com `version`, `files[].path`, `purpose` (`migration|bootstrap|check|status`), `executor`
    (`ef|psql|admin`), `parameters[]` (`identifier|literal|secret`) e `sha256`, cobrindo os arquivos existentes de
    `Migrations/Script/` e os novos de `Sql/Installation/`;
  - arquivos como recurso embutido, como hoje, e cópia da árvore com o manifesto para `/app/sql/` em
    `backend/apps/Dokpod.ControlPlane.Api/Dockerfile` e `Business.Dockerfile`.

  - `Sql/Installation/checks/schema-fingerprint.sql` (executor `admin`) e `Sql/schema/expected-schema.json` (FR-042):
    tabelas, colunas (tipo, nulidade, default), índices, restrições, funções (assinatura e SHA-256 do corpo) e
    gatilhos do schema `dokpod`, sem `relispartition` e sem concessões. O arquivo é gerado por
    `backend/tests/Dokpod.ControlPlane.Api.Tests/Admin/ExpectedSchemaTests.cs`, que aplica todas as migrations em
    PostgreSQL real e falha se estiver desatualizado.

  Testes em `backend/tests/Dokpod.Architecture.Tests/SqlCatalogTests.cs`: todo `.sql` está no manifesto, todo item do
  manifesto existe, os hashes conferem e a versão é a do produto.
- [ ] T007 [P] Arquivos de instalação em `backend/libs/Dokpod.ControlPlane.Infrastructure/Sql/Installation/`, com
  parâmetros nomeados e sem concatenação:
  - `bootstrap/01-create-database.sql`;
  - `bootstrap/02-create-roles.sql` (`dokpod_owner` e `dokpod_runtime`, sem `SUPERUSER`/`CREATEROLE`; a senha é lida
    pelo próprio arquivo com `\set` a partir de `/run/secrets`, declarada como parâmetro `secret` no manifesto);
  - `bootstrap/03-grants.sql` (DML e `EXECUTE` a `dokpod_runtime`, mais `ALTER DEFAULT PRIVILEGES` de `dokpod_owner` no
    schema `dokpod` para as tabelas e funções futuras);
  - `checks/server-version.sql` e `checks/role-privileges.sql` (`rolsuper`, `rolcreaterole`, `CONNECT`, `USAGE`, DML e
    ausência de DDL);
  - `status/conference-counts.sql` (linhas de `dokpod.audit_events` e `dokpod.agent_identities`, usado no SC-008).

  Executor `psql` para bootstrap e checks: identificadores e literais não secretos em `:"x"` e `:'x'` por `-v`, e
  segredos nunca por `-v` nem por variável de ambiente. Executor `admin` para status (`@x`). Teste com valor canário
  na senha, conferindo que ela não aparece no `ps` nem no `docker inspect` do container do `psql`. Testes com
  PostgreSQL real em `backend/tests/Dokpod.ControlPlane.Api.Tests/Admin/InstallationSqlTests.cs`, incluindo nome de role
  com aspas e literal com apóstrofo.
- [ ] T008 Raiz externa opcional no `MigrationSqlScriptLoader`
  (`backend/libs/Dokpod.ControlPlane.Infrastructure/Migrations/MigrationSqlScriptLoader.cs`), habilitada só pelo modo
  administrativo com `/run/sql-override`:
  - exige `manifest.json` da mesma versão;
  - usa o arquivo externo de mesmo `path` e recusa caminho sem correspondente na release;
  - expõe o hash original e o aplicado de cada arquivo usado.

  O runtime da API nunca habilita essa raiz. Testes em
  `backend/tests/Dokpod.ControlPlane.Api.Tests/Admin/SqlOverrideTests.cs`: sobreposição usada no modo administrativo e
  ignorada no runtime, versão divergente recusada, caminho sem correspondente recusado.
- [ ] T009 [P] Varredura contra SQL no código (SC-011):
  - teste em `backend/tests/Dokpod.Architecture.Tests/NoInlineSqlTests.cs` que recusa literais C# com palavras-chave
    DDL/DML (`CREATE`, `ALTER`, `DROP`, `INSERT`, `UPDATE`, `DELETE`, `SELECT`, `GRANT`) em
    `backend/apps/Dokpod.ControlPlane.Api/Admin/` e nos serviços chamados por ele;
  - teste equivalente em `deploy/installer/tests/NoInlineSql.Tests.ps1` para `deploy/installer/src/`, exigindo que
    toda consulta ao estado SQLite venha de `deploy/installer/sql/state/`.

  Exceções explícitas e justificadas numa lista no próprio teste.

### Modo administrativo da imagem da API

- [ ] T010 (FR-009) Despacho de `admin` em `backend/apps/Dokpod.ControlPlane.Api/Program.cs` e
  `backend/apps/Dokpod.ControlPlane.Api/Admin/AdminCommandHost.cs`:
  - Com `args[0] == "admin"`, monta só o `DbContext` e os serviços de aplicação necessários, sem Kestrel, gRPC,
    SignalR nem hosted services, e termina com os códigos de [admin-commands.md](contracts/admin-commands.md)
    (0, 1, 2, 6 e 8).
  - A conexão vem só de `/run/secrets/ConnectionStrings__ControlPlane`; não há opção de linha de comando.
  - Saída JSON em stdout (`--output json`) e mensagens sem segredos em stderr.
  - O instalador executa o container avulso com `--read-only`, sem socket do engine, sem porta publicada, só na rede
    interna do banco e só com os mounts do comando (L5 do threat model).
- [ ] T011 [P] Testes de `admin migrations` contra PostgreSQL real em
  `backend/tests/Dokpod.ControlPlane.Api.Tests/Admin/AdminMigrationsTests.cs`:
  - banco vazio e banco na penúltima migration;
  - `status` com `applied`, `pending` e `newerThanTarget`;
  - `plan` com `sha256` por migration e `planHash`;
  - `apply --confirm` com `planHash` errado recusado com código 6;
  - banco com migration desconhecida recusado com código 6;
  - `script` com o cabeçalho de [migration-script.md](contracts/migration-script.md);
  - **SC-010**: aplicar o arquivo exportado com `psql -v ON_ERROR_STOP=1` num banco e `apply` noutro dá o mesmo
    `pg_dump --schema-only` e o mesmo `__EFMigrationsHistory`;
  - reaplicar o arquivo falha na chave primária do histórico, sem alteração.
- [ ] T012 Implementar `backend/apps/Dokpod.ControlPlane.Api/Admin/MigrationsCommands.cs`:
  - `status|plan|script|apply|verify` com `IMigrator.GenerateScript(from, to, MigrationsSqlGenerationOptions.Default)`
    por migration, sem `--idempotent` (D-003), com o corpo vindo do catálogo e da raiz sobreposta (T008);
  - `plan` com `overridden` e `originalSha256` por migration, e `script` com uma linha `-- override:` por arquivo
    sobreposto;
  - `sha256` do trecho entre `START TRANSACTION;` e `COMMIT;` de cada migration e `planHash` como SHA-256 da lista
    ordenada `id:sha256`;
  - resumo de objetos de `plan` a partir de `CREATE`/`ALTER`/`DROP` de nível superior;
  - `fingerprint` com a impressão do schema real;
  - `verify [--script] [--accepted-fingerprints]` conferindo os IDs do histórico e a impressão do schema real
    contra `expected-schema.json` ou uma impressão aceita (`schemaDiff` objeto a objeto, código 6 se a diferença não
    for aceita) e, com `--script`, os hashes do arquivo entregue (`fileHashMismatch`), conforme FR-042;
  - teste em `AdminMigrationsTests.cs`: cada alteração manual (coluna, índice, restrição, função, gatilho) é
    detectada sem `--script`, e partições criadas em execução não geram diferença.

  Depende de T010, T011 e T008.
- [ ] T013 [P] Ação de auditoria `environment.agent_identity_register` na categoria `environments` de
  `backend/libs/Dokpod.Domain/Auditing/AuditCategories.cs` e em `AuditEvent.cs`. Serviço de aplicação
  `backend/libs/Dokpod.ControlPlane.Application/Agents/AgentIdentityRegistrationService.cs`:
  - o mesmo par ambiente/fingerprint não muda nada;
  - um fingerprint ligado a outro ambiente ou revogado é recusado;
  - o registro grava pelo `IAgentIdentityRegistry` e audita com ator técnico `installer` e `actorDisplay`.

  Testes em `backend/tests/Dokpod.ControlPlane.Application.Tests/Agents/AgentIdentityRegistrationServiceTests.cs`.
- [ ] T014 [P] Testes de `admin environments register`, `admin agent-identities register|revoke` e
  `admin licensing import` (imagem Business) contra PostgreSQL real em
  `backend/tests/Dokpod.ControlPlane.Api.Tests/Admin/AdminBootstrapTests.cs`:
  - idempotência (`created:false`);
  - código 8 para dados divergentes ou fingerprint em outro ambiente;
  - eventos `environment.register` e `environment.agent_identity_register` com ator `installer`;
  - `revoke` pelo mesmo caminho e auditoria do endpoint `.../agent-identity/revoke`;
  - `licensing import` com as validações da spec 031, e ausente na CE (código 2).
- [ ] T015 Implementar `backend/apps/Dokpod.ControlPlane.Api/Admin/EnvironmentsCommands.cs`,
  `AgentIdentitiesCommands.cs` e `LicensingCommands.cs` (este só na composição Business), reaproveitando os serviços de
  aplicação existentes e o de T013. Depende de T010, T013 e T014.
- [ ] T016 Qualificar a role `dokpod_runtime` (D-009) em
  `backend/tests/Dokpod.ControlPlane.Api.Tests/Admin/RuntimeRolePrivilegesTests.cs`:
  - aplicar as migrations como `dokpod_owner` e conceder a `dokpod_runtime` só DML e `EXECUTE` no schema `dokpod` pelos
    arquivos `Sql/Installation/bootstrap/` de T007, sem `SUPERUSER`/`CREATEROLE`/DDL;
  - exercitar com `dokpod_runtime` a criação antecipada de partições, a retenção, a auditoria, o inventário e os
    comandos.

  Se algo exigir DDL, registrar o desvio em `research.md` (D-009) e conceder o mínimo adicional, com teste.
- [ ] T017 Segredos por arquivo na API e no BFF (D-006):
  - provedor `Microsoft.Extensions.Configuration.KeyPerFile` para `/run/secrets`, registrado só quando o diretório
    existe, em `backend/apps/Dokpod.ControlPlane.Api/ApiHost.cs` e em `backend/apps/Dokpod.Bff/`;
  - versão centralizada em `backend/Directory.Packages.props`, com licença MIT conferida;
  - senha de PFX também por arquivo.

  Testes em `backend/tests/Dokpod.ControlPlane.Api.Tests/Configuration/SecretFilesConfigurationTests.cs` e
  `backend/tests/Dokpod.Bff.Tests/SecretFilesConfigurationTests.cs`: arquivo vence, ausência mantém as variáveis
  atuais, nada é logado.

### Núcleo do instalador

- [ ] T018 [P] Respostas em `deploy/installer/src/Answers.psm1`, com testes em
  `deploy/installer/tests/Answers.Tests.ps1`:
  - validar YAML/JSON contra `contracts/answers.schema.json` (copiado para `deploy/installer/templates/answers.schema.json`);
  - recusar segredo em texto (só `{file}` ou `{env}`, FR-002);
  - recusar `installDir` igual a `secretsDir`, um dentro do outro ou dentro de checkout Git;
  - modo guiado gravando `answers.yaml` sem segredos;
  - sem TTY e com resposta faltante, código 2.
- [ ] T019 [P] (FR-044, FR-034) Estado em SQLite, *lock* e relatório em `deploy/installer/src/State.psm1`, com testes
  em `deploy/installer/tests/State.Tests.ps1` e arquivos SQL em `deploy/installer/sql/state/` (D-014):
  - `schema/0001-initial.sql` com as tabelas `installation`, `components`, `certificates`, `environments`,
    `sql_overrides`, `runs`, `run_steps` e `run_lock` de [data-model.md](data-model.md#estado-da-instalação-sqlite),
    `PRAGMA user_version`, `journal_mode=WAL` e `foreign_keys=ON`;
  - `queries/*.sql` com uma consulta por arquivo e parâmetros `$name`;
  - `manifest.json` no formato de D-013;
  - acesso só por `Microsoft.Data.Sqlite`;
  - `version` só aumenta;
  - schema aplicado só para a frente; `user_version` maior que o conhecido dá código 2;
  - `run_steps` gravado numa transação depois de cada etapa;
  - `run_lock` em `BEGIN IMMEDIATE`, com código 7 se ocupado e liberação de *lock* velho só com confirmação;
  - `PRAGMA integrity_check` no início, e banco ausente ou corrompido numa instalação existente reconstruído a partir
    do observado só com confirmação;
  - backup `VACUUM INTO state/backups/<runId>.db`, mantendo as 10 últimas;
  - retenção das 200 últimas execuções em `runs`/`run_steps` e de seus `reports/*.json`, preservando sempre as que têm
    linha em `sql_overrides`;
  - `outcome` em `converged|changed|prerequisite_failed|declined|failed|paused_for_manual_migration`;
  - `reports/<runId>.json` sem segredos (FR-034), gerado a partir do banco;
  - teste de que nenhuma coluna recebe valor canário de segredo.
- [ ] T020 Motor de plano em `deploy/installer/src/Plan.psm1`, com testes em `deploy/installer/tests/Plan.Tests.ps1`:
  - desejado (respostas mais versão) contra observado (inspect do engine, `admin migrations status`, `-DryRun` dos
    scripts do Keycloak, sistema de arquivos);
  - ações `create|update|keep|recreate|remove` com motivo e efeito colateral;
  - `keep` quando `desiredHash` e digest coincidem;
  - `remove` de componente com dados só na recriação total;
  - componente `existing` nunca recriado (D-007, FR-026, FR-027).

  Depende de T019.
- [ ] T021 [P] Pré-requisitos em `deploy/installer/src/Prerequisites.psm1`, com testes em
  `deploy/installer/tests/Prerequisites.Tests.ps1`. Verificar:
  - engine em modo rootless e versão mínima (Docker ≥ 27 rootless, Podman ≥ 5 rootless) e Compose v2 pela API do
    socket, recusando o modo rootful com código 3 (`SecurityOptions` com `name=rootless` no Docker,
    `host.security.rootless` no Podman; D-015);
  - `edge.httpsPort` contra `net.ipv4.ip_unprivileged_port_start`, com as três saídas de D-015 e sem alterar o `sysctl`;
  - preservação do IP de origem pelo driver de portas (aviso, sem bloqueio) e *linger* do usuário (aviso com o
    comando);
  - portas livres;
  - espaço em disco;
  - resolução do nome público;
  - alcance e versão de PostgreSQL e Keycloak existentes;
  - `unqualified-search-registries` no Podman.

  Qualquer falha dá código 3 sem efeito (FR-003).
- [ ] T022 [P] (FR-016, FR-017) PKI em `deploy/installer/src/Pki.psm1`, com testes em `deploy/installer/tests/Pki.Tests.ps1`:
  - CA local ECDSA P-256 de 10 anos (`CA:true,pathlen:0`);
  - servidor e cliente de 397 dias, EKU `serverAuth` para borda e API e `clientAuth` para agentes;
  - SAN da API `api` mais o nome público quando `agentGrpcPort` é definido;
  - PEM para a borda e PFX com senha gerada para API e agentes;
  - opção `rsa-3072`.

  Validação (FR-017) de certificados gerados e da organização: SAN, EKU, validade, cadeia e par chave/certificado,
  recusando com o motivo. Chave da CA só em `secretsDir/pki/` e nunca montada em serviço (FR-018).
- [ ] T023 [P] Segredos em `deploy/installer/src/Secrets.psm1`, com testes em `deploy/installer/tests/Secrets.Tests.ps1`:
  - geração com entropia do sistema;
  - escrita `0600` em diretório `0700`;
  - falha fechada se o destino existe sem `recreate` daquele item (FR-028);
  - leitura de `{file}`/`{env}` sem eco;
  - redação de toda saída do instalador por uma lista dos valores carregados na execução (FR-033).

  O teste usa valores canário e confere saída, relatório e arquivos gerados.
- [ ] T024 Imagem `deploy/installer/Dockerfile`, com as versões de T004 fixadas e usuário não root, mais
  `deploy/installer/install.sh` POSIX:
  - detecta o engine e o socket rootless (`$XDG_RUNTIME_DIR/docker.sock` ou `$XDG_RUNTIME_DIR/podman/podman.sock`) e
    recusa socket fora de `$XDG_RUNTIME_DIR` (D-015);
  - verifica a imagem do instalador pelo digest gravado no próprio script (placeholder substituído na publicação, T051)
    com cosign em container por digest;
  - executa a imagem montando só o socket, `installDir` e `secretsDir`;
  - repassa os argumentos e não tem lógica de instalação;
  - repassa ao container só as variáveis referenciadas em `{"env": ...}` no arquivo de respostas, por nome
    (`-e NOME`, sem valor na linha de comando nem eco), para que FR-002 funcione. Teste em
    `deploy/installer/tests/InstallSh.Tests.ps1` com valor canário;
  - executa o instalador e todos os containers avulsos com `--log-driver none` (`--log-driver=none` no Podman), para
    que o driver de log do engine não grave a saída nem a senha temporária (L7 do threat model).

  Os scripts `tools/scripts/configure-keycloak.ps1` e `provision-environment-authorization.ps1` são copiados para a
  imagem no build, sem duplicação. Depende de T004 e T005.
- [ ] T025 [P] Imagens e assinatura em `deploy/installer/src/Images.psm1`, com testes em
  `deploy/installer/tests/Images.Tests.ps1`:
  - ler o `release-manifest.json` da versão;
  - usar só `referência@digest`;
  - `cosign verify` com a identidade do `publish.yml` de `docs/release-integrity.md` antes de qualquer `pull` ou uso;
  - modo `--offline` com `docker load` e assinaturas exportadas;
  - assinatura ou manifesto inválido dá código 5 sem efeito (FR-005, FR-006).
- [ ] T026 [P] (FR-020, FR-022, FR-023) Renderização em `deploy/installer/src/Render.psm1` e templates em `deploy/installer/templates/`, com
  testes em `deploy/installer/tests/Render.Tests.ps1`:
  - `compose.yaml` com `ASPNETCORE_ENVIRONMENT=Production`, sem IP fixo, redes internas e só a borda publicada;
  - `secrets:` montados em `/run/secrets`;
  - socket só no agente;
  - alias do nome público na borda dentro da rede interna;
  - `SSL_CERT_FILE` da API e do BFF com um bundle das raízes do sistema, da CA local (quando usada) e da
    `keycloak.caCertificate` (quando informada), conforme D-008;
  - `dokpod.env` sem segredos;
  - `deploy.env` no formato de `deploy/hooks/compose-deploy-hook.sh` (`COMPOSE_DIR`, `COMPOSE_FILE`, `ENV_FILE`,
    `PROJECT`, `SERVICES`, `ORDER`);
  - `edge/dokpod.conf` com `/dokpod/`, BFF, SignalR e, com Keycloak provisionado, `/realms/` e `/resources/`.

  - `Security__TrustedNetworks` do BFF só com a sub-rede interna da borda provisionada, ou `127.0.0.1/32` e
    `::1/128` com nginx existente em loopback (ou a sub-rede da rede interna compartilhada). Faixa maior que a rede
    interna é recusada (L1 do threat model).

  `nginx -t` em container no teste e `docker compose config` sem erro. Teste de que o BFF ignora `X-Forwarded-*` vindos
  de fora da rede confiável.
- [ ] T027 (FR-001, FR-004, FR-035) CLI em `deploy/installer/src/Dokpod.Installer.ps1`, com testes em `deploy/installer/tests/Cli.Tests.ps1`:
  - comandos, opções e códigos de saída 0 a 7 e 10 de [installer-cli.md](contracts/installer-cli.md);
  - `--dry-run` e `plan` sem efeitos;
  - `--yes` não confirma remoção de dados nem migrations, que exigem `--confirm-data-removal <lista>` e
    `--confirm-migrations <planHash>`;
  - `status`, `help` e `sql export --out` sem efeitos (FR-035, FR-040). `status` lista as identidades de pacote de
    host sem conexão há mais de 7 dias e sugere `host revoke` (L6 do threat model);
  - `--sql-dir` e `--confirm-sql-overrides`, com `--yes` sem confirmar sobreposição.

  Depende de T018 a T023, T025 e T026.

**Checkpoint**: modo administrativo testado contra PostgreSQL real; o instalador gera um plano completo em `--dry-run`
para qualquer arquivo de respostas válido.

---

## Fase 3: História 1 - Instalar tudo num host vazio (Prioridade: P1) 🎯 MVP

**Objetivo**: num host só com engine e Compose, a instalação completa termina com o console acessível, o host local
conectado e um administrador que precisa trocar a senha.

**Teste independente**: cenários 1 e 2 do [quickstart](quickstart.md) nos dois engines.

- [ ] T028 [US1] PostgreSQL provisionado em `deploy/installer/src/Postgres.psm1`:
  - duas instâncias `dokpod-postgres` e `keycloak-postgres` (`lzocateli/postgresql:18.4-pgvector0.8.6-bookworm`),
    volumes e credenciais próprios (FR-007);
  - no `dokpod-postgres`, banco e roles `dokpod_owner` e `dokpod_runtime` pelos arquivos `Sql/Installation/bootstrap/`
    do catálogo, executados com `psql` em container avulso (T007, T016);
  - conexão do `psql` pelo `PGPASSFILE` num tmpfs montado só nesse container e apagado ao fim, nunca por
    `PGPASSWORD` ou outra variável de ambiente (FR-033). Teste com canário no `docker inspect`;
  - conexões gravadas em `secretsDir` (`dokpod-owner-connection`, `ConnectionStrings__ControlPlane`);
  - container avulso do `psql` com `--read-only`, sem socket do engine, sem porta publicada e só na rede interna do
    banco (L5 do threat model).

  Testes em `deploy/installer/tests/Postgres.Tests.ps1`.
- [ ] T029 [US1] Keycloak provisionado em `deploy/installer/src/Keycloak.psm1`:
  - container na versão homologada (`lzocateli/keycloak:26.7.0`) com banco em `keycloak-postgres` (FR-012);
  - administrador de bootstrap só no primeiro start (`keycloak-bootstrap-admin`);
  - `configure-keycloak.ps1 -Bootstrap` com o realm, o `BffBaseUrl` público e o usuário inicial com senha temporária
    (FR-013, FR-014);
  - client secret do BFF gravado em `secretsDir/Authentication__Keycloak__ClientSecret`;
  - remoção do administrador de bootstrap depois que o administrador nominal existe. Se falhar, a execução termina com
    `outcome: failed` e a ação no relatório, nunca `converged`, e a reexecução tenta de novo (L3 do threat model).

  Testes em `deploy/installer/tests/Keycloak.Tests.ps1` com um Keycloak real em container.
- [ ] T030 [US1] Etapa de migrations em `deploy/installer/src/Migrations.psm1`:
  - `admin migrations status` e `plan` pelo container da imagem da API da versão;
  - lista das pendentes com objetos afetados e confirmação ligada ao `planHash` (FR-036);
  - com `--sql-dir`: conferência do manifesto, cópia para `installDir/sql-override/`, lista de cada arquivo alterado
    com o hash original e o novo, confirmação nomeada (`--confirm-sql-overrides`), montagem em `/run/sql-override` e
    registro na tabela `sql_overrides` do estado e no relatório (FR-041);
  - `apply`; com `manualMigrations`, `script` para `installDir/migrations/<de>-<para>.sql`, pausa com código 10 e
    `verify` na retomada (FR-037, FR-038);
  - banco com migration mais nova dá código 6 (FR-010);
  - banco que já tem dados, inclusive na primeira instalação sobre `existing-prepared`, exige a confirmação de backup
    (ou `pg_dump` quando `dokpod-postgres` é provisionado) antes do `apply`, como no T048 (FR-011);
  - sobreposição de arquivo de migration já aplicada: avisa que não tem efeito nesse banco e só registra com
    confirmação (caso de borda da spec);
  - depois de `apply` com sobreposição, `admin migrations fingerprint` gravado em
    `sql_overrides.accepted_schema_fingerprint` (FR-042);
  - na reexecução, hashes da cópia em `installDir/sql-override/` recalculados e comparados com
    `sql_overrides.applied_sha256`; qualquer diferença volta ao plano e exige nova confirmação nomeada (L4 do threat
    model).

  Testes em `deploy/installer/tests/Migrations.Tests.ps1`.
- [ ] T031 [US1] Primeiro ambiente em `deploy/installer/src/Environments.psm1`:
  - `admin environments register` com o nome e estágio de `localEnvironment`;
  - `provision-environment-authorization.ps1` para o grupo `/dokpod/administrators` com os scopes do ambiente;
  - certificado do agente (T022);
  - `admin agent-identities register` com o fingerprint;
  - serviço `agent` do engine escolhido, com socket só nele (FR-024).

  Testes em `deploy/installer/tests/Environments.Tests.ps1`.
- [ ] T032 [US1] (FR-021) Borda provisionada (`dokpod-edge` com `lzocateli/nginx:1.28.0-bookworm-v1`) e subida ordenada em
  `deploy/installer/src/Services.psm1`:
  - `docker compose up --wait` por etapa (bancos, Keycloak, API, BFF e web, borda, agente);
  - health de cada serviço;
  - edição Business com `dokpod-api-business` e `admin licensing import` quando `license` existe (FR-031);
  - resumo final com URL, usuário, caminho do relatório e senha temporária exibida uma vez só em TTY, ou em
    `secretsDir/initial-admin-password` sem TTY, removido na primeira execução seguinte em que o Keycloak mostrar a senha
    já trocada (sem `UPDATE_PASSWORD`), conforme [data-model.md](data-model.md#segredo-da-instalação).
- [ ] T033 [US1] Retomada e reexecução: interromper em cada etapa (pki, banco, migrations, realm, ambiente, serviços) e
  retomar, com teste em `deploy/installer/tests/Resume.Tests.ps1` usando engine real em container. A reexecução com as
  mesmas respostas deve dar plano todo `keep` e `outcome: converged` em até 2 minutos (SC-003, SC-004).
- [ ] T034 [US1] (SC-001, SC-002) Validar os cenários 1 e 2 do quickstart em host descartável com Docker Engine rootless e com Podman rootless,
  incluindo a recusa de um engine rootful e de `httpsPort` abaixo do limite do host.
  Medir o tempo (SC-001), entrar pela borda com Playwright (`lzocateli/playwright-e2e`) e registrar as evidências em
  `specs/033-instalador-guiado/validation.md`.

**Checkpoint**: História 1 funcional e testável de forma independente (MVP).

---

## Fase 4: História 2 - Usar PostgreSQL e nginx que já existem (Prioridade: P2)

**Objetivo**: usar a infraestrutura do usuário sem duplicá-la e com menor privilégio.

**Teste independente**: cenário 3 do quickstart.

- [ ] T035 [US2] `postgres.mode: existing-create` em `deploy/installer/src/Postgres.psm1`:
  - credencial administrativa de `{file}`/`{env}` ou prompt, usada uma vez e descartada (nunca em `secretsDir`);
  - entregue ao container do `psql` só por `PGPASSFILE` em tmpfs, apagado ao fim, como no T028 (FR-033);
  - criação do banco `dokpod`, de `dokpod_owner` e de `dokpod_runtime` pelos mesmos arquivos de bootstrap do catálogo;
  - `sslMode` e `caCertificate` respeitados;
  - versão mínima do servidor conferida (FR-008).
- [ ] T036 [US2] `postgres.mode: existing-prepared`: validar conexão, versão e privilégios com `checks/server-version.sql`
  e `checks/role-privileges.sql` do catálogo (sem `rolsuper`, sem `rolcreaterole`, `CONNECT` e `USAGE` presentes), e recusar com código 3 dizendo o
  privilégio em excesso ou em falta. Testes em `deploy/installer/tests/Postgres.Tests.ps1` com PostgreSQL real.
- [ ] T037 [US2] (FR-021, FR-043) `edge.mode: existing` em `deploy/installer/src/Edge.psm1`:
  - gerar `installDir/edge/dokpod.conf` com `upstreamHost`, `webPort` e `bffPort`;
  - publicar web e BFF só em `upstreamHost`, aceitando apenas `127.0.0.1`, `::1` ou `{network}` (rede interna
    compartilhada com o nginx do usuário), e recusar outro valor com código 2 (FR-043);
  - validar com `nginx -t` em container;
  - listar os passos manuais (certificado, `include`, `reload`), sem tocar em arquivo fora de `installDir`.

  Testes em `deploy/installer/tests/Edge.Tests.ps1`, incluindo a recusa de `upstreamHost` fora de loopback e
  `Security__TrustedNetworks` restrito ao loopback ou à rede interna compartilhada (L1 do threat model).
- [ ] T038 [US2] Combinações (só PostgreSQL existente, só nginx existente, ambos) no motor de plano, com teste em
  `deploy/installer/tests/Plan.Tests.ps1`. Validar o cenário 3 do quickstart com Docker Engine rootless e com Podman rootless
  (SC-002) e registrar em `validation.md`.

---

## Fase 5: História 3 - Integrar a um Keycloak existente (Prioridade: P2)

**Objetivo**: configurar só o realm do Dokpod numa instância existente.

**Teste independente**: cenário 4 do quickstart, com export de `master` e `outro` antes e depois.

- [ ] T039 [US3] `keycloak.mode: existing` em `deploy/installer/src/Keycloak.psm1`:
  - verificar versão e acesso administrativo antes de alterar;
  - credencial administrativa transitória (FR-015);
  - `caCertificate` para TLS do Keycloak;
  - `configure-keycloak.ps1 -Bootstrap -SkipUser` contra `url` e `realm`;
  - atribuir as roles do Dokpod e o grupo `/dokpod/administrators` a `administratorUser` (FR-014).

  Recusa com código 3 sem alteração quando a versão está abaixo da homologada ou falta acesso.
- [ ] T040 [US3] Divergências do realm: usar o `-DryRun` dos scripts para listar no plano cada item a corrigir e aplicar
  só após confirmação, preservando usuários, sessões e customizações fora do contrato. Testes em
  `deploy/installer/tests/Keycloak.Tests.ps1` com Keycloak real, incluindo redirect URI alterado à mão.
- [ ] T041 [US3] Validar o cenário 4 do quickstart com Docker Engine rootless e com Podman rootless (SC-002): os exports de
  `master` e `outro` iguais antes e depois (SC-006), registrado em `validation.md`.

---

## Fase 6: História 4 - Recriar componentes com segurança (Prioridade: P3)

**Objetivo**: recriação por componente e total sem perda acidental de dados.

**Teste independente**: cenário 6 do quickstart.

- [ ] T042 [US4] `recreate <item>` em `deploy/installer/src/Recreate.psm1` para `pki:edge`, `pki:api`,
  `pki:agent:<id>`, `secret:<nome>`, `config` e `service:<nome>`:
  - efeito colateral no plano;
  - só o item pedido muda;
  - componentes `existing` recusados (FR-029).

  Testes em `deploy/installer/tests/Recreate.Tests.ps1`.
- [ ] T043 [US4] `recreate --all`:
  - confirmação normal e depois a segunda confirmação nomeando `dokpod-postgres`, `keycloak-postgres`,
    `dokpod-api-keys`, `dokpod-bff-keys` e as identidades;
  - sem ela, recria todo o resto e preserva os dados;
  - não interativo só com `--confirm-data-removal <lista>` exata (SC-008).
- [ ] T044 [US4] `recreate pki:ca` pelo procedimento de `docs/runbooks/troca-da-ca-dos-agentes.md`: estado
  `active → rotating → active`, bundle com as raízes antiga e nova em `DOKPOD_API_CLIENT_CA_CERTIFICATE_PATH` e na
  confiança dos agentes, reemissão e novo `agent-identities register` de cada agente, e remoção da raiz antiga só depois
  de todos conectados (FR-030).
- [ ] T045 [US4] Validar o cenário 6 do quickstart (contagens de `dokpod.audit_events` e `dokpod.agent_identities` iguais
  depois da recusa) e registrar em `validation.md`.

---

## Fase 7: História 5 - Adicionar hosts e atualizar a versão (Prioridade: P3)

**Objetivo**: pacote de host, revogação e atualização com migrations sem SDK.

**Teste independente**: cenários 5 e 8 do quickstart.

- [ ] T046 [US5] `host add` em `deploy/installer/src/Hosts.psm1`:
  - ambiente cadastrado, certificado de cliente, identidade registrada;
  - `packages/<environmentId>.tar` `0600` com `agent.pfx`, `agent.pass`, `ca.crt`, `agent.env`, `compose.agent.yaml` e
    `README.txt`, mais SHA-256;
  - nenhum outro arquivo de `secretsDir` (FR-025);
  - `--engine windows`: o pacote troca `compose.agent.yaml` por `windows-service.txt`, com os parâmetros de
    `deploy/agent/manage-windows-service.ps1 -Action Install` (`-ControlPlaneEndpoint` e `-EnvironmentId`). A
    instalação no Windows continua pelo script e pelo runbook de `docs/runbooks/qualificacao-windows.md`.

  - pacote cifrado com uma frase gerada, exibida uma vez em TTY ou gravada em `secretsDir` sem TTY, e nunca guardada no
    pacote; o `README.txt` explica como abri-lo (L2 do threat model);
  - `package_generated_at` registrado para o aviso de `status` sobre pacote sem conexão há mais de 7 dias (L6 do
    threat model).

  Exige `agentGrpcPort`, a única porta publicada além da borda, só com mTLS (FR-022). Testes em `deploy/installer/tests/Hosts.Tests.ps1` conferindo o conteúdo do pacote.
- [ ] T047 [US5] `host revoke --environment --reason` por `admin agent-identities revoke`, com o pacote marcado como
  revogado no estado.
- [ ] T048 [US5] `upgrade --version` em `deploy/installer/src/Upgrade.psm1`:
  - recusar versão igual ou menor com referência a `docs/runbooks/deploy-e-rollback.md` e
    `docs/runbooks/backup-restore-e-atualizacao.md`;
  - verificar as imagens (T025);
  - com migration nova, exigir backup confirmado, ou executar `pg_dump` quando `dokpod-postgres` é provisionado
    (FR-011);
  - migrations pela T030;
  - atualizar o plano de controle antes dos agentes;
  - confirmar `org.opencontainers.image.version` de cada serviço (FR-032).

  Testes em `deploy/installer/tests/Upgrade.Tests.ps1`.
- [ ] T049 [US5] `certificates check` e aviso de vencimento a partir de `renewWarningDays` (padrão 30) na reexecução,
  oferecendo `recreate pki:<item>` para certificados `local-ca`, e só aviso para `organization` (FR-019).
- [ ] T050 [US5] Validar os cenários 5 e 8 do quickstart com duas versões consecutivas com migration (candidatas da
  release), o segundo host Podman e uma imagem adulterada no `--offline` (código 5), e registrar em `validation.md`
  (SC-007, SC-009).

---

## Fase Final: Refinamento e Aspectos Transversais

- [ ] T051 [P] Publicação em `.github/workflows/publish.yml`:
  - imagem `dokpod-installer` na lista `images` (`deploy/installer/Dockerfile`);
  - `install.sh` em `build-assets` com o placeholder do digest substituído pelo digest publicado do instalador, de
    modo que o asset seja atestado e assinado como os demais;
  - asset `dokpod-sql-<versão>.tar.gz` com o catálogo e o `manifest.json`, atestado como os demais (FR-040);
  - entrada correspondente no `release-manifest.json`.
- [ ] T052 [P] Smoke test da imagem do instalador no CI (`.github/workflows/ci.yml`): `--help`, `plan --dry-run` com
  arquivo de respostas de exemplo e Pester completo dentro da imagem.
- [ ] T053 Canário de segredos (SC-005) em `deploy/installer/tests/SecretCanary.Tests.ps1` e no cenário 7 do quickstart.
  Procurar cada valor em saída, `reports/*.json`, `installDir`, `docker inspect`, `ps -ef` durante a execução, logs dos
  containers e journal do host (`journalctl --user`, L7 do threat model), e confirmar zero ocorrências.
- [ ] T054 [P] Runbook `docs/runbooks/instalacao.md`: os três cenários, o arquivo de respostas, migrations manuais
  revisadas por DBA, recriação, pacote de host, atualização, desinstalação e o que é preservado.
- [ ] T055 [P] Atualizar `docs/distribuicao.md` (caminho de instalação e imagem do instalador), `docs/seguranca.md`
  (instalador, modo administrativo e credenciais transitórias), `README.md` (link do runbook) e `deploy/e2e/README.md`
  (a stack de laboratório continua, com link para o instalador).
- [ ] T056 Varredura Gitleaks com `.gitleaks.toml` nos arquivos novos e alterados. Nunca escrever pares chave=valor de
  senha nem em exemplos.
- [ ] T057 Formatação, análise estática e testes amplos: `dotnet format --verify-no-changes` nos projetos alterados,
  suíte .NET completa, Pester completo e `PSScriptAnalyzer` (T004) sem avisos nos módulos de
  `deploy/installer/src/`.
- [ ] T058 Revisão de segurança e privacidade das superfícies de T001 contra o implementado, em
  `specs/033-instalador-guiado/threat-model.md`, com achados e correções. Inclui o SQL sobreposto e o banco de estado
  SQLite.
- [ ] T059 Validar o cenário 9 do quickstart (catálogo SQL sobreposto) e registrar em `validation.md` (FR-039 a
  FR-041, SC-011):
  - `sql export` e alteração de um arquivo de migration pendente;
  - plano com os hashes;
  - recusa sem confirmação nomeada;
  - aplicação confirmada;
  - aviso para sobreposição de migration já aplicada, registrada só com confirmação;
  - `verify` aceitando a impressão registrada;
  - alteração manual sem registro (coluna, índice, restrição, função, gatilho) detectada pela impressão do schema,
    com código 6 (FR-042).
- [ ] T060 Fechar `specs/033-instalador-guiado/validation.md`: os nove cenários do quickstart, o ambiente, os resultados,
  os `NOT RUN` com motivo e o estado das tarefas.

---

## Dependências e Ordem de Execução

- **Fase 1**: T001 bloqueia todo código. T002 a T004 em paralelo; T005 depois de T001.
- **Fase 2**:
  - Catálogo SQL: T006 → (T007, T008, T009); T008 antes de T012; T007 antes de T016.
  - Modo administrativo: T010 → (T011, T013, T014 em paralelo) → T012 e T015; T016 depende de T012.
  - Segredos por arquivo: T017 independente.
  - Núcleo do instalador: T018, T019, T021, T022, T023, T025 e T026 em paralelo; T020 depende de T019; T024 depende de
    T004 e T005; T027 fecha a fase.
- **US1** (T028 a T034) depende de toda a Fase 2.
- **US2** (T035 a T038) e **US3** (T039 a T041) dependem de US1 (reaproveitam os módulos de T028 e T029) e são
  independentes entre si.
- **US4** (T042 a T045) depende de US1.
- **US5** (T046 a T050) depende de US1. T048 depende também de T030.
- **Fase Final**: T051 e T052 dependem de T024 e T027; T053, T058, T059 e T060 depois das histórias entregues.

## Exemplos de Execução Paralela

- **Fundação**: T007 e T009 junto com T011, T013 e T014 (testes .NET em arquivos distintos) junto com T018, T019, T021, T022, T023, T025 e
  T026 (módulos PowerShell distintos).
- **Depois da US1**: US2 (T035 a T038) e US3 (T039 a T041) em paralelo; US4 e US5 em paralelo com elas, cuidando dos
  módulos compartilhados (`Postgres.psm1`, `Keycloak.psm1`).
- **Refinamento**: T051, T052, T054 e T055 em paralelo.

## Estratégia de Implementação

1. **Entrega intermediária**: T001 aceito → catálogo SQL e T010 a T016. O modo administrativo de migrations sai numa release sozinho e
   já resolve migration sem SDK (e o controle do DBA) na stack atual, antes do instalador.
2. **MVP**: Fundação completa → US1. Validar os cenários 1 e 2 nos dois engines e entregar.
3. **Incremento 2**: US2 e US3 (infraestrutura do usuário).
4. **Incremento 3**: US4 e US5 (operação contínua).
5. **Fechamento**: publicação (T051, T052), canário, runbook, revisão de segurança e `validation.md`.
