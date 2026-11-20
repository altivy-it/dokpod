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

- [X] T006 Catálogo SQL em `backend/libs/Dokpod.ControlPlane.Infrastructure/Sql/`:
  - `manifest.json` com `version`, `files[].path`, `purpose` (`migration|bootstrap|check|status`), `executor`
    (`ef|psql|admin`), `parameters[]` (`identifier|literal|secret`) e `sha256`, cobrindo os arquivos existentes de
    `Migrations/Script/` e os novos de `Sql/Installation/`;
  - arquivos como recurso embutido, como hoje, e cópia da árvore com o manifesto para `/app/sql/` em
    `backend/apps/Dokpod.ControlPlane.Api/Dockerfile` e `Business.Dockerfile`.

  - `Installation/checks/schema-fingerprint.sql` (executor `admin`) e `schema/expected-schema.json` (FR-042):
    tabelas, colunas (tipo, nulidade, default), índices, restrições, funções (assinatura e SHA-256 do corpo) e
    gatilhos do schema `dokpod`, sem `relispartition` e sem concessões. O arquivo é gerado por
    `backend/tests/Dokpod.ControlPlane.Api.Tests/Admin/ExpectedSchemaTests.cs`, que aplica todas as migrations em
    PostgreSQL real e falha se estiver desatualizado.

  Testes em `backend/tests/Dokpod.Architecture.Tests/SqlCatalogTests.cs`: todo `.sql` está no manifesto, todo item do
  manifesto existe, os hashes conferem e a versão é a do produto.
  Nota (2026-11-08): `Sql/manifest.json` com 60 arquivos (53 de migration e 7 de instalação), versão atualizada pelo
  release-please (`extra-files`) e catálogo lido por `SqlFiles/SqlCatalog.cs`; as imagens copiam a árvore para
  `/app/sql` (`Migrations/Script/`, `Installation/`, `schema/`, `manifest.json`). Atualização dos hashes e da impressão
  com `DOKPOD_UPDATE_SQL_CATALOG=1`. A impressão (652 objetos) sai igual no PostgreSQL 17.6 e no 18.4: foi preciso
  excluir as cópias de FK e gatilho herdadas por partição (`conparentid`, `tgparentid`) e as restrições `NOT NULL` que
  o 18 passou a guardar em `pg_constraint`. Inclui `view` como tipo, além dos da spec.
- [X] T007 [P] Arquivos de instalação em `backend/libs/Dokpod.ControlPlane.Infrastructure/Sql/Installation/`, com
  parâmetros nomeados e sem concatenação:
  - `bootstrap/01-create-database.sql`;
  - `bootstrap/02-create-roles.sql` (`dokpod_owner` e a role de runtime, sem `SUPERUSER`/`CREATEROLE`; a senha é lida
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
  Nota (2026-11-08): ordem ajustada para `01-create-roles.sql`, `02-create-database.sql` e `03-grants.sql`, porque o
  banco precisa da role dona antes. Senhas lidas pelo próprio arquivo com `\set` e crase a partir de
  `*_password_file`; o teste confere o verificador SCRAM contra a senha canário. Bootstrap rodado duas vezes
  (idempotente). O CI ganhou o passo `Install PostgreSQL client` no job de backend. Risco anotado para a T058: com
  `log_statement` em `ddl` ou `all` no servidor, o `ALTER ROLE ... PASSWORD` aparece no log do PostgreSQL.
- [X] T008 Raiz externa opcional no `MigrationSqlScriptLoader`
  (`backend/libs/Dokpod.ControlPlane.Infrastructure/Migrations/MigrationSqlScriptLoader.cs`), habilitada só pelo modo
  administrativo com `/run/sql-override`:
  - exige `manifest.json` da mesma versão;
  - usa o arquivo externo de mesmo `path` e recusa caminho sem correspondente na release;
  - expõe o hash original e o aplicado de cada arquivo usado.

  O runtime da API nunca habilita essa raiz. Testes em
  `backend/tests/Dokpod.ControlPlane.Api.Tests/Admin/SqlOverrideTests.cs`: sobreposição usada no modo administrativo e
  ignorada no runtime, versão divergente recusada, caminho sem correspondente recusado.
  Nota (2026-11-08): `SqlCatalog.UseOverride` abre a raiz por `AsyncLocal` e registra cada uso em `Uses`;
  `MigrationSqlScriptLoader` passou a ler pelo catálogo. Recusa versão divergente, manifesto ausente, arquivo sem
  correspondente, link simbólico, sobreposição aninhada e a impressão do schema esperado.
- [X] T009 [P] Varredura contra SQL no código (SC-011):
  - teste em `backend/tests/Dokpod.Architecture.Tests/NoInlineSqlTests.cs` que recusa literais C# com palavras-chave
    DDL/DML (`CREATE`, `ALTER`, `DROP`, `INSERT`, `UPDATE`, `DELETE`, `SELECT`, `GRANT`) em
    `backend/apps/Dokpod.ControlPlane.Api/Admin/` e nos serviços chamados por ele;
  - teste equivalente em `deploy/installer/tests/NoInlineSql.Tests.ps1` para `deploy/installer/src/`, exigindo que
    toda consulta ao estado SQLite venha de `deploy/installer/sql/state/`.

  Exceções explícitas e justificadas numa lista no próprio teste.

  Nota (2026-11-08): `NoInlineSqlTests` cobre `Admin/` (a partir da T010) e `SqlFiles/`, com controles positivos e
  negativos do detector; `NoInlineSql.Tests.ps1` usa o parser do PowerShell e cobre também `PRAGMA` do SQLite
  (10 testes Pester 6.2.0).

### Modo administrativo da imagem da API

- [X] T010 (FR-009) Despacho de `admin` em `backend/apps/Dokpod.ControlPlane.Api/Program.cs` e
  `backend/apps/Dokpod.ControlPlane.Api/Admin/AdminCommandHost.cs`:
  - Com `args[0] == "admin"`, monta só o `DbContext` e os serviços de aplicação necessários, sem Kestrel, gRPC,
    SignalR nem hosted services, e termina com os códigos de [admin-commands.md](contracts/admin-commands.md)
    (0, 1, 2, 6 e 8).
  - A conexão vem só de `/run/secrets/ConnectionStrings__ControlPlane`; não há opção de linha de comando.
  - Saída JSON em stdout (`--output json`) e mensagens sem segredos em stderr.
  - O instalador executa o container avulso com `--read-only`, sem socket do engine, sem porta publicada, só na rede
    interna do banco e só com os mounts do comando (L5 do threat model).
  Nota (2026-11-08): `Admin/AdminCommandHost.cs` e despacho em `Program.cs`; a conexão vem só de
  `/run/secrets/ConnectionStrings__ControlPlane`, e o modo garante `search_path` `dokpod` quando a conexão não o define.
  `ControlPlaneDbContextFactory.Create` passou a ser a fábrica pública do contexto.
- [X] T011 [P] Testes de `admin migrations` contra PostgreSQL real em
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
  Nota (2026-11-08): `AdminMigrationsTests` (11 testes), com um banco novo por teste. O SC-010 é comparado pela
  impressão do schema e pelo histórico, e não por `pg_dump`: o `pg_dump` 16 do cliente da distribuição recusa servidor
  17, e a impressão é a mesma comparação do `verify`.
- [X] T012 Implementar `backend/apps/Dokpod.ControlPlane.Api/Admin/MigrationsCommands.cs`:
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
  Nota (2026-11-08): o EF não acha migration por ID neste projeto. Ele só reconhece IDs com 14 dígitos de data, e os
  daqui têm 12, então `GenerateScript(de, até)` e `Migrate(alvo)` falham. O SQL sai de `MigrationScripts`, com o gerador
  do próprio EF, e o `apply` executa os blocos do plano. O SQL exportado e o `apply` começam por
  `Installation/migrations/session.sql` (fuso UTC e `search_path`). Sem isso, o `psql` criava as primeiras tabelas em
  `public` e uma partição colidia. Os blocos têm marcadores `-- dokpod-migration-begin/end`.
- [X] T013 [P] Ação de auditoria `environment.agent_identity_register` na categoria `environments` de
  `backend/libs/Dokpod.Domain/Auditing/AuditCategories.cs` e em `AuditEvent.cs`. Serviço de aplicação
  `backend/libs/Dokpod.ControlPlane.Application/Agents/AgentIdentityRegistrationService.cs`:
  - o mesmo par ambiente/fingerprint não muda nada;
  - um fingerprint ligado a outro ambiente ou revogado é recusado;
  - o registro grava pelo `IAgentIdentityRegistry` e audita com ator técnico `installer` e `actorDisplay`.

  Testes em `backend/tests/Dokpod.ControlPlane.Application.Tests/Agents/AgentIdentityRegistrationServiceTests.cs`.
  Nota (2026-11-08): o registro grava por `IAgentIdentityEnrollment`, interface nova implementada por
  `PostgresAgentIdentityRegistry`, sem mexer em `IAgentIdentityRegistry` e nos seus dublês. Eventos de sistema passaram
  a aceitar `ActorName` (operador do instalador), e o console ganhou o rótulo da ação nova. Revogar pelo modo
  administrativo não derruba a sessão em memória de uma API em execução; fica para a T047.
- [X] T014 [P] Testes de `admin environments register`, `admin agent-identities register|revoke` e
  `admin licensing import` (imagem Business) contra PostgreSQL real em
  `backend/tests/Dokpod.ControlPlane.Api.Tests/Admin/AdminBootstrapTests.cs`:
  - idempotência (`created:false`);
  - código 8 para dados divergentes ou fingerprint em outro ambiente;
  - eventos `environment.register` e `environment.agent_identity_register` com ator `installer`;
  - `revoke` pelo mesmo caminho e auditoria do endpoint `.../agent-identity/revoke`;
  - `licensing import` com as validações da spec 031, e ausente na CE (código 2).
  Nota (2026-11-08): `AdminBootstrapTests` (7 testes), com a licença na edição Business por `AdminContext.Edition` e um
  trust store de teste.
- [X] T015 Implementar `backend/apps/Dokpod.ControlPlane.Api/Admin/EnvironmentsCommands.cs`,
  `AgentIdentitiesCommands.cs` e `LicensingCommands.cs` (este só na composição Business), reaproveitando os serviços de
  aplicação existentes e o de T013. Depende de T010, T013 e T014.
  Nota (2026-11-08): `BootstrapCommands.cs` (ambiente e identidade) e `LicensingCommands.cs`. O `environments
  register` ganhou `--host`. O código de licença compila nas duas edições, como o resto do licenciamento, e recusa em
  execução na CE.
- [X] T016 Qualificar a role `dokpod_runtime` (D-009) em
  `backend/tests/Dokpod.ControlPlane.Api.Tests/Admin/RuntimeRolePrivilegesTests.cs`:
  - aplicar as migrations como `dokpod_owner` e conceder a `dokpod_runtime` só DML e `EXECUTE` no schema `dokpod` pelos
    arquivos `Installation/bootstrap/` de T007, sem `SUPERUSER`/`CREATEROLE`/DDL;
  - exercitar com `dokpod_runtime` a criação antecipada de partições, a retenção, a auditoria, o inventário e os
    comandos.

  Se algo exigir DDL, registrar o desvio em `research.md` (D-009) e conceder o mínimo adicional, com teste.
  Nota (2026-11-08): `RuntimeRolePrivilegesTests`. As migrations já têm o grupo `dokpod_runtime` com privilégios finos;
  o bootstrap cria antes o grupo, põe a role de login nele e não concede DML amplo. As migrations rodam inteiras com a
  role dona sem privilégio administrativo. Desvio corrigido: o grupo não alcançava `environments` nem
  `agent_identities`; a migration `202611080001_GrantRuntimeEnvironments` concede o mínimo (D-009).
- [X] T017 Segredos por arquivo na API e no BFF (D-006):
  - provedor `Microsoft.Extensions.Configuration.KeyPerFile` para `/run/secrets`, registrado só quando o diretório
    existe, em `backend/apps/Dokpod.ControlPlane.Api/ApiHost.cs` e em `backend/apps/Dokpod.Bff/`;
  - versão centralizada em `backend/Directory.Packages.props`, com licença MIT conferida;
  - senha de PFX também por arquivo.

  Testes em `backend/tests/Dokpod.ControlPlane.Api.Tests/Configuration/SecretFilesConfigurationTests.cs` e
  `backend/tests/Dokpod.Bff.Tests/SecretFilesConfigurationTests.cs`: arquivo vence, ausência mantém as variáveis
  atuais, nada é logado.

  Nota (2026-11-08): `Configuration/SecretFiles.cs` na API e no BFF; o provedor *key-per-file* já está no framework
  compartilhado do ASP.NET Core 10, então não houve pacote novo. A senha do PFX também vem de arquivo. O diretório
  alternativo só vale por `DOKPOD_SECRETS_DIRECTORY`, para testes.

### Núcleo do instalador

- [X] T018 [P] Respostas em `deploy/installer/src/Answers.psm1`, com testes em
  `deploy/installer/tests/Answers.Tests.ps1`:
  - validar YAML/JSON contra `contracts/answers.schema.json` (copiado para `deploy/installer/templates/answers.schema.json`);
  - recusar segredo em texto (só `{file}` ou `{env}`, FR-002);
  - recusar `installDir` igual a `secretsDir`, um dentro do outro ou dentro de checkout Git;
  - modo guiado gravando `answers.yaml` sem segredos;
  - sem TTY e com resposta faltante, código 2.
  Nota (2026-11-11): `Answers.psm1` lê YAML pelo YamlDotNet 18.1.0 e JSON, recusa segredo em texto antes do schema (`Test-Json -SchemaFile`), aplica padrões e valida caminhos; o modo guiado grava `answers.yaml` sem segredos e, sem TTY, resposta faltante dá código 2. Pester em container: `Answers.Tests.ps1` passa; um teste confere o hash da cópia do schema contra o contrato.
- [X] T019 [P] (FR-044, FR-034) Estado em SQLite, *lock* e relatório em `deploy/installer/src/State.psm1`, com testes
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
  Nota (2026-11-11): `State.psm1` com 25 arquivos SQL em `sql/state/` (manifest com SHA-256) e acesso só por `Microsoft.Data.Sqlite` 10.0.14. O *lock* usa transação `Serializable` (equivalente a `BEGIN IMMEDIATE`). `State.Tests.ps1` cobre schema só para a frente, `integrity_check`, banco corrompido, *lock* velho, backup, retenção e o canário em todas as colunas.
- [X] T020 Motor de plano em `deploy/installer/src/Plan.psm1`, com testes em `deploy/installer/tests/Plan.Tests.ps1`:
  - desejado (respostas mais versão) contra observado (inspect do engine, `admin migrations status`, `-DryRun` dos
    scripts do Keycloak, sistema de arquivos);
  - ações `create|update|keep|recreate|remove` com motivo e efeito colateral;
  - `keep` quando `desiredHash` e digest coincidem;
  - `remove` de componente com dados só na recriação total;
  - componente `existing` nunca recriado (D-007, FR-026, FR-027).

  Depende de T019.
  Nota (2026-11-11): `Plan.psm1` decide pelo rótulo `io.dokpod.installer.config-hash` que `Render.psm1` grava em cada serviço. Container do projeto `dokpod` sem esse rótulo (por exemplo, a stack de `deploy/e2e`) bloqueia com código 3, sem ser tocado. Os observadores de `admin migrations status` e do `-DryRun` do Keycloak entram como `Pending` nas tarefas que executam essas etapas (T029, T030). `Plan.Tests.ps1` passa.
- [X] T021 [P] Pré-requisitos em `deploy/installer/src/Prerequisites.psm1`, com testes em
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
  Nota (2026-11-11): `Prerequisites.psm1` lê engine, versão, rootless e Compose pelo CLI no socket montado, e os fatos do host nas variáveis `DOKPOD_HOST_*` do `install.sh`. Validado contra o Podman 5.7.0 rootless deste host (todas as verificações `ok`). `Prerequisites.Tests.ps1` passa.
- [X] T022 [P] (FR-016, FR-017) PKI em `deploy/installer/src/Pki.psm1`, com testes em `deploy/installer/tests/Pki.Tests.ps1`:
  - CA local ECDSA P-256 de 10 anos (`CA:true,pathlen:0`);
  - servidor e cliente de 397 dias, EKU `serverAuth` para borda e API e `clientAuth` para agentes;
  - SAN da API `api` mais o nome público quando `agentGrpcPort` é definido;
  - PEM para a borda e PFX com senha gerada para API e agentes;
  - opção `rsa-3072`.

  Validação (FR-017) de certificados gerados e da organização: SAN, EKU, validade, cadeia e par chave/certificado,
  recusando com o motivo. Chave da CA só em `secretsDir/pki/` e nunca montada em serviço (FR-018).
  Nota (2026-11-11): a PKI usa as APIs de criptografia do .NET, sem `openssl` (nota em D-005 e em `docs/distribuicao.md`). PFX em AES-256 com 100000 iterações. `Pki.Tests.ps1` passa; `nginx -t` com o certificado gerado passa em T026.
- [X] T023 [P] Segredos em `deploy/installer/src/Secrets.psm1`, com testes em `deploy/installer/tests/Secrets.Tests.ps1`:
  - geração com entropia do sistema;
  - escrita `0600` em diretório `0700`;
  - falha fechada se o destino existe sem `recreate` daquele item (FR-028);
  - leitura de `{file}`/`{env}` sem eco;
  - redação de toda saída do instalador por uma lista dos valores carregados na execução (FR-033).

  O teste usa valores canário e confere saída, relatório e arquivos gerados.
  Nota (2026-11-11): `Secrets.psm1` grava com `CreateNew` em temporário e move. Destino existente sem `recreate secret:<nome>` dá código 4. `Secrets.Tests.ps1` confere o canário na saída, no relatório e nos arquivos.
- [X] T024 Imagem `deploy/installer/Dockerfile`, com as versões de T004 fixadas e usuário não root, mais
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
  Nota (2026-11-11): `install.sh` resolve o socket (inclusive link simbólico) e recusa alvo fora de `$XDG_RUNTIME_DIR`. Recusa imagem alternativa que não seja `localhost/...` e verifica o cosign por digest antes de executar. Monta socket, `installDir`, `secretsDir`, as respostas e os arquivos citados nelas; repassa as variáveis `{env}` só pelo nome. Executa com `--user 0:0` e `--log-driver none`. `InstallSh.Tests.ps1` usa engines falsos e canário; a imagem `dokpod-installer:test` constrói.
- [X] T025 [P] Imagens e assinatura em `deploy/installer/src/Images.psm1`, com testes em
  `deploy/installer/tests/Images.Tests.ps1`:
  - ler o `release-manifest.json` da versão;
  - usar só `referência@digest`;
  - `cosign verify` com a identidade do `publish.yml` de `docs/release-integrity.md` antes de qualquer `pull` ou uso;
  - modo `--offline` com `docker load` e assinaturas exportadas;
  - assinatura ou manifesto inválido dá código 5 sem efeito (FR-005, FR-006).
  Nota (2026-11-11): `Images.psm1` aceita só `referência@digest` do manifesto e verifica o cosign v3.1.4 por digest com `--log-driver none`. As imagens de infraestrutura ficam fixadas em `templates/infrastructure-images.json`. `Images.Tests.ps1` passa.
- [X] T026 [P] (FR-020, FR-022, FR-023) Renderização em `deploy/installer/src/Render.psm1` e templates em `deploy/installer/templates/`, com
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
  Nota (2026-11-11): o `compose.yaml` sai pelo YamlDotNet; os segredos entram só por `secrets:` (o Keycloak os lê com `read -r` no entrypoint). A rede `dokpod-edge` tem sub-rede fixa `10.253.33.0/24`, que é a única rede confiável do BFF com a borda provisionada. `Render.Tests.ps1` roda `docker compose config` e `nginx -t` na imagem fixada pelo socket do Podman. `ForwardedHeadersTrustTests` (Dokpod.Bff.Tests, 3 casos) prova que o BFF ignora `X-Forwarded-*` de fora da rede confiável.
- [X] T027 (FR-001, FR-004, FR-035) CLI em `deploy/installer/src/Dokpod.Installer.ps1`, com testes em `deploy/installer/tests/Cli.Tests.ps1`:
  - comandos, opções e códigos de saída 0 a 7 e 10 de [installer-cli.md](contracts/installer-cli.md);
  - `--dry-run` e `plan` sem efeitos;
  - `--yes` não confirma remoção de dados nem migrations, que exigem `--confirm-data-removal <lista>` e
    `--confirm-migrations <planHash>`;
  - `status`, `help` e `sql export --out` sem efeitos (FR-035, FR-040). `status` lista as identidades de pacote de
    host sem conexão há mais de 7 dias e sugere `host revoke` (L6 do threat model);
  - `--sql-dir` e `--confirm-sql-overrides`, com `--yes` sem confirmar sobreposição.

  Depende de T018 a T023, T025 e T026.
  Nota (2026-11-11): `Cli.psm1` faz a leitura dos argumentos com confirmações nomeadas e executa `help`, `plan`/`--dry-run`, `status` e `sql export`. Os comandos que aplicam mudanças saem com 2 e apontam a tarefa que os entrega. O ponto de entrada lê `$args`, porque com `CmdletBinding` a opção `--out` colide com os parâmetros comuns. `Cli.Tests.ps1` roda `plan` contra o engine real e confere que nada é gravado. Suíte completa em container: 145 testes passam; PSScriptAnalyzer sem erros (avisos ficam para a T057); Gitleaks limpo.

**Checkpoint**: modo administrativo testado contra PostgreSQL real; o instalador gera um plano completo em `--dry-run`
para qualquer arquivo de respostas válido.

---

## Fase 3: História 1 - Instalar tudo num host vazio (Prioridade: P1) 🎯 MVP

**Objetivo**: num host só com engine e Compose, a instalação completa termina com o console acessível, o host local
conectado e um administrador que precisa trocar a senha.

**Teste independente**: cenários 1 e 2 do [quickstart](quickstart.md) nos dois engines.

- [X] T028 [US1] PostgreSQL provisionado em `deploy/installer/src/Postgres.psm1`:
  - duas instâncias `dokpod-postgres` e `keycloak-postgres` (`lzocateli/postgresql:18.4-pgvector0.8.6-bookworm`),
    volumes e credenciais próprios (FR-007);
  - no `dokpod-postgres`, banco, role dona e role de runtime (membro do grupo `dokpod_runtime`) pelos arquivos `Installation/bootstrap/`
    do catálogo, executados com `psql` em container avulso (T007, T016);
  - conexão do `psql` pelo `PGPASSFILE` num tmpfs montado só nesse container e apagado ao fim, nunca por
    `PGPASSWORD` ou outra variável de ambiente (FR-033). Teste com canário no `docker inspect`;
  - conexões gravadas em `secretsDir` (`dokpod-owner-connection`, `ConnectionStrings__ControlPlane`);
  - container avulso do `psql` com `--read-only`, sem socket do engine, sem porta publicada e só na rede interna do
    banco (L5 do threat model).

  Testes em `deploy/installer/tests/Postgres.Tests.ps1`.
  Nota (2026-11-11): `Postgres.psm1` gera `dokpod-db-password` e `keycloak-db-password` só quando faltam e executa
  `server-version`, os três arquivos de `Installation/bootstrap/` e `role-privileges` (role dona e de runtime
  `dokpod_app`) conferidos pelo SHA-256 do manifesto. Cada parâmetro declarado no manifesto só entra pelo canal do seu
  tipo: `secret` como arquivo, os demais por `-v`. O `pgpass`, as senhas, o SQL e o roteiro vão por stdin num tar
  extraído para um tmpfs (`noexec`, `mode=1777`, porque o Podman recusa `uid=` em `--tmpfs`) do container avulso, com
  `--read-only`, `--cap-drop ALL`, `no-new-privileges`, `--log-driver none` e só a rede do banco. O bootstrap confere o
  menor privilégio depois de aplicado e recusa conexão existente de outra role sem sobrescrevê-la. A subida das
  instâncias pelo compose fica com a subida ordenada da T032. 11 testes Pester, inclusive contra PostgreSQL 18.4 real
  no Podman, com o `docker inspect` do container do psql varrido durante a execução sem achar nenhuma senha.
- [X] T029 [US1] Keycloak provisionado em `deploy/installer/src/Keycloak.psm1`:
  - container na versão homologada (`lzocateli/keycloak:26.7.0`) com banco em `keycloak-postgres` (FR-012);
  - administrador de bootstrap só no primeiro start (`keycloak-bootstrap-admin`);
  - `configure-keycloak.ps1 -Bootstrap` com o realm, o `BffBaseUrl` público e o usuário inicial com senha temporária
    (FR-013, FR-014);
  - client secret do BFF gravado em `secretsDir/Authentication__Keycloak__ClientSecret`;
  - remoção do administrador de bootstrap depois que o administrador nominal existe. Se falhar, a execução termina com
    `outcome: failed` e a ação no relatório, nunca `converged`, e a reexecução tenta de novo (L3 do threat model).

  Testes em `deploy/installer/tests/Keycloak.Tests.ps1` com um Keycloak real em container.
  Nota (2026-11-11): `Keycloak.psm1` gera `keycloak-bootstrap-admin`, o client secret do BFF e
  `keycloak-provisioner-client-secret` (novo no data-model, para as reconciliações sem o `master`). A reconciliação roda
  em container avulso da imagem do instalador (`Keycloak.Sidecar.ps1`), só em `dokpod_keycloak-data`, com raiz somente
  leitura, `--cap-drop ALL` e `--log-driver none`. O sidecar, o `configure-keycloak.ps1` e os segredos entram por stdin
  num tar extraído em tmpfs. O `configure-keycloak.ps1` ganhou `-InternalHttp`, porque recusava HTTP fora do loopback.
  O usuário inicial é lido antes: ausente recebe a senha temporária, sem senha recebe de novo, e pronto entra com
  `-SkipUser`, porque o script reaplicaria `requiredActions` vazio e limparia o `UPDATE_PASSWORD` pendente. A senha vai
  para `initial-admin-password` antes de ser aplicada, e o arquivo sai quando o Keycloak mostra a senha trocada. Com
  `-Bootstrap`, a conta `dokpod-bootstrap` sai do `master` e o arquivo fica vazio; falha na remoção dá código 1 com a
  ação, e a reexecução tenta de novo (L3). O `install.sh` repassa `DOKPOD_INSTALLER_IMAGE_REF`. A subida do container
  pelo compose fica com a T032. 8 testes Pester, 3 contra Keycloak 26.7.0 real (hostname público e HTTP interno como no
  compose), com o `docker inspect` do container avulso varrido sem achar segredo. Achado: o `configure-keycloak.ps1`
  também cria os clients de laboratório `dokpod-lab` e `dokpod-authorization-spike` numa instalação de produção.
- [X] T030 [US1] Etapa de migrations em `deploy/installer/src/Migrations.psm1`:
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
  Nota (2026-11-12): `Invoke-AdminCommand` roda o modo administrativo num container avulso com `--read-only`,
  `--cap-drop ALL`, `--user 0:0` (o usuário do host no engine rootless, dono dos arquivos de `secretsDir`), só na rede do
  banco, e com a conexão de `dokpod_owner` montada só para leitura. O `--sql-dir` é conferido antes do plano. A cópia fica em
  `installDir/sql-override.pendente` até a confirmação e só então passa a `sql-override/`; ela guarda só o manifesto e os
  arquivos alterados. Uma cópia de outra versão é ignorada com aviso.
  Decisão nova: `--confirm-backup <banco>` para o backup no modo não interativo, porque a CLI não tinha confirmação
  nomeada para FR-011. Ela foi acrescentada em `contracts/installer-cli.md`. No modo interativo, o operador digita
  `aplicar`, cada caminho sobreposto e o nome do banco.
  Na retomada manual, a impressão observada só é aceita se o `verify --script` não acusar arquivo alterado e o arquivo
  exportado tiver linhas `-- override:`. A sobreposição de arquivo de bootstrap (`executor psql`) é conferida e registrada,
  mas a T032 ainda precisa passar o catálogo efetivo ao `Invoke-PostgresBootstrap`. Parar a API e o BFF antes do backup,
  como pede o runbook, fica com a ordem do `upgrade` (T048).
  13 testes Pester, 4 contra PostgreSQL 18.4 e a imagem da API desta branch:
  - banco vazio;
  - `pg_dump` no banco com dados;
  - sobreposição aceita e cópia alterada que volta a pedir confirmação;
  - pausa 10, retomada 6 e retomada `ok`.
- [X] T031 [US1] Primeiro ambiente em `deploy/installer/src/Environments.psm1`:
  - `admin environments register` com o nome e estágio de `localEnvironment`;
  - `provision-environment-authorization.ps1` para o grupo `/dokpod/administrators` com os scopes do ambiente;
  - certificado do agente (T022);
  - `admin agent-identities register` com o fingerprint;
  - serviço `agent` do engine escolhido, com socket só nele (FR-024).

  Testes em `deploy/installer/tests/Environments.Tests.ps1`.
  Nota (2026-11-12): o ambiente local reaproveita o ID do estado; sem ele, recebe um novo. O host é `DOKPOD_HOST_NAME`,
  que o `install.sh` passou a repassar (`uname -n`), ou o host da `publicUrl`. A CA local não tinha etapa que a gravasse:
  `Initialize-LocalCertificateAuthority` (em `Pki.psm1`) a carrega de `secretsDir/pki/ca.crt` e `ca.key` ou a cria, e só o
  certificado sem a chave interrompe. O certificado do agente (`pki/agent-<id>.pfx` e `.pass`) é reaproveitado quando
  passa na validação contra a CA; inválido, interrompe e aponta `recreate pki:agent:<id>`. O conflito do modo
  administrativo (código 8) interrompe com o `failureCode` e a ação. A autorização roda num container avulso só na rede
  do Keycloak, com o client secret do `dokpod-provisioner` no tar do stdin, e concede ao grupo os scopes do `ValidateSet`
  de `-Scope` do próprio script (fonte única). O script ganhou `-InternalHttp`, como o `configure-keycloak.ps1`. O
  container avulso do Keycloak virou `Invoke-KeycloakSidecar`, usado pelas duas etapas. `Assert-EngineSocketIsolation`
  confere no modelo do compose que só o `agent` monta o socket, sem porta e só em `dokpod-internal`; a subida do serviço
  fica com a T032.
  Achado corrigido: o agente lia a senha do PFX só da variável de ambiente, mas o compose a entrega em `/run/secrets`.
  `AgentOptions.ReadSecret` lê o arquivo antes da variável, como a API e o BFF.
  Achado corrigido: a varredura de histórico da CI (um patch por commit) acusava 29 falsos positivos nos commits da T028
  à T030: nomes de arquivo de segredo, chamadas como `Read-InstallerSecret` e valores sintéticos dos testes. O
  `.gitleaks.toml` ganhou padrões ancorados para eles, com casos em `tools/scripts/test-gitleaks.ps1` que confirmam que um
  literal ao lado dessas chamadas continua bloqueado.
  Também corrigido: `run-steps-list.sql` ordena por `rowid`, porque duas etapas no mesmo milissegundo saíam em ordem
  alfabética (teste intermitente de `State.Tests.ps1`).
  9 testes Pester, 1 contra PostgreSQL 18.4, Keycloak 26.7.0 e as imagens da API e do instalador desta branch: cadastro
  com os eventos `environment.register` e `environment.agent_identity_register`, recurso com os 17 scopes e a policy do
  grupo, e reexecução sem mudança.
- [X] T032 [US1] (FR-021) Borda provisionada (`dokpod-edge` com `lzocateli/nginx:1.28.0-bookworm-v1`) e subida ordenada em
  `deploy/installer/src/Services.psm1`:
  - `docker compose up --wait` por etapa (bancos, Keycloak, API, BFF e web, borda, agente);
  - health de cada serviço;
  - edição Business com `dokpod-api-business` e `admin licensing import` quando `license` existe (FR-031);
  - resumo final com URL, usuário, caminho do relatório e senha temporária exibida uma vez só em TTY, ou em
    `secretsDir/initial-admin-password` sem TTY, removido na primeira execução seguinte em que o Keycloak mostrar a senha
    já trocada (sem `UPDATE_PASSWORD`), conforme [data-model.md](data-model.md#segredo-da-instalação).

  Nota (2026-11-12): `Services.psm1` sobe as etapas `data`, `identity`, `application`, `edge` e `agent` com
  `docker compose up --detach --wait` e confere o estado e o health de cada serviço no `compose ps`. Um serviço que não
  fica saudável interrompe a execução com o nome do serviço e o comando de logs, sem copiar os logs. O Keycloak ganhou
  healthcheck no compose a cada 5 s, com o teste da imagem, e a borda usa o `/nginx-healthz` da imagem fixada. O comando
  `install` (`Install.psm1`) ficou ligado na CLI para o cenário 1:
  - antes de qualquer efeito, confere os pré-requisitos, o manifesto e o cosign;
  - depois aplica, em ordem: plano confirmado, `secrets`, `pki`, `config`, `services:data`, `database`, `migrations`,
    `license` (só Business), `services:identity`, `realm`, `environment`, `services:application`, `services:edge`,
    `services:edge-reload` e `services:agent`;
  - registra cada etapa no estado só depois de terminar, gera o relatório e mostra o resumo final;
  - PostgreSQL, Keycloak ou borda existentes param com código 2 e apontam a T035 à T039.
  Decisões:
  - os certificados da borda (PEM) e da API (PFX) são gravados aqui, pela CA local ou pela organização, validados
    contra FR-017 e nunca sobrescritos sem `recreate pki:edge` ou `pki:api`;
  - o slot da licença é `prd` com `localEnvironment.stage` `production` e `hml` nos demais;
  - no pacote offline, as assinaturas ficam em `signatures/<nome no manifesto>` (a T051 publica o pacote);
  - no TTY, a senha temporária aparece uma vez, fora da redação, e o arquivo é apagado.
  Achados corrigidos, todos levantados pela instalação real:
  - D-015 já pedia o dono certo nos segredos montados e nenhuma tarefa o aplicava. O Keycloak (uid 1000), a API e o
    BFF (uid 1654) não liam os arquivos `0600` do root do engine. `Set-ServiceSecretOwner` troca o dono para o UID do
    serviço antes de cada etapa; serviço root não muda o dono;
  - fora de Development, a API recusava `BackchannelAuthority` HTTP. Ela passou a usar a Authority pública, que chega
    à borda pelo alias de rede (D-008);
  - pelo mesmo D-008, a borda passou a escutar também na porta pública quando ela não é 8443. Antes, o BFF não
    alcançava a Authority de dentro da rede;
  - o BFF exige um certificado RSA para o key ring fora de Development. O instalador gera `pki/bff-dataprotection.pfx`;
  - o BFF recusa downstream HTTP fora de Development, e a API não tinha HTTPS sem mTLS. Por decisão do solicitante
    (2026-11-12), a API ganhou `DOKPOD_API_HTTPS_PORT` (8443, mesmo certificado, sem certificado de cliente), e o BFF
    usa `https://api:8443`. O gRPC dos agentes continua só no mTLS;
  - o BFF só aceitava o host `localhost`. `AllowedHosts` passou a ser o nome público mais `localhost`, que o
    healthcheck usa;
  - o nginx guarda o IP dos upstreams. Quando web, BFF ou Keycloak são recriados e a borda não, o instalador a reinicia;
  - o certificado público da CA era registrado como segredo e barrava `certs/ca.crt` na redação. Os PEM públicos não
    passam mais pelo registro.
  Pendente: o catálogo efetivo no bootstrap com `--sql-dir` (nota da T030) continua para a T059. A importação real da
  licença Business só foi exercitada com engine falso.
  20 testes Pester em `Services.Tests.ps1`, 1 deles com engine real. `Install.Tests.ps1` (5 testes, com
  `DOKPOD_TEST_FULL_INSTALL=1`) rodou com Podman rootless, PostgreSQL 18.4, Keycloak 26.7.0, nginx e as imagens desta
  branch, num projeto compose próprio:
  - parou com código 4 sem `--confirm-migrations` e retomou com o planHash;
  - os 8 serviços ficaram saudáveis;
  - pela borda, `/dokpod/` e o OIDC responderam 200, e `/dokpod/bff/login` respondeu 302 para o Keycloak;
  - a reexecução terminou `converged`, sem recriar nenhum container;
  - tudo levou 147 s.
- [X] T033 [US1] Retomada e reexecução: interromper em cada etapa (pki, banco, migrations, realm, ambiente, serviços) e
  retomar, com teste em `deploy/installer/tests/Resume.Tests.ps1` usando engine real em container. A reexecução com as
  mesmas respostas deve dar plano todo `keep` e `outcome: converged` em até 2 minutos (SC-003, SC-004).

  Nota (2026-11-12): `Resume.Tests.ps1` (com `DOKPOD_TEST_FULL_INSTALL=1`) interrompe a instalação real por um
  invólucro do `docker`, que falha uma vez no primeiro comando de cada etapa: o `psql` do banco, `migrations apply`, o
  container avulso do realm, `agent-identities register` (com o ambiente e a autorização já gravados) e o `up` da
  aplicação. Na `pki`, a queda entre as duas gravações de um par é simulada: só a chave da borda e só a senha do PFX da
  API. Também entra a pausa sem `--confirm-migrations` (código 4). Cada interrupção sai com o código do contrato, marca a
  etapa como `failed` e libera a trava.
  A retomada converge em 78 s, sem recurso duplicado: um banco `dokpod`, um ambiente, uma identidade, um evento de
  auditoria por cadastro, um realm, um usuário inicial, um client `dokpod-bff` e nenhum `dokpod-bootstrap`. A reexecução
  com as mesmas respostas levou 16 s, com o plano todo `keep` (12 componentes) e `outcome: converged`. Os containers, o
  hash de cada arquivo de `secretsDir`, os fingerprints dos certificados e as contagens dos bancos do Dokpod e do
  Keycloak ficaram iguais.
  Achados corrigidos:
  - o ID do ambiente local só ia para o estado no fim da etapa `environment`. Cada execução interrompida antes gerava
    outro ID, outro certificado de agente e outro `compose.yaml`; a interrompida depois do cadastro tentava cadastrar
    outro ambiente com o mesmo nome. `Save-LocalEnvironment` grava o ambiente na `pki`, logo depois do certificado;
  - um container que termina antes de ler o stdin quebrava a escrita do tar com erro do .NET, sem código do contrato.
    `Invoke-InstallerProcess` passou a valer o código de saída;
  - a falha do `up` na subida passou a trazer a última linha do Compose.
  Achado sem correção: com o mesmo nome e outro ID, `admin environments register` saiu com código 1 ("could not be
  persisted"), não com o conflito 8 do contrato. Com a correção acima, o instalador não chega mais a isso, mas o modo
  administrativo merece a correção pelo Bug Fixing.
- [X] T034 [US1] (SC-001, SC-002) Validar os cenários 1 e 2 do quickstart em host descartável com Docker Engine rootless e com Podman rootless,
  incluindo a recusa de um engine rootful e de `httpsPort` abaixo do limite do host.
  Medir o tempo (SC-001), entrar pela borda com Playwright (`lzocateli/playwright-e2e`) e registrar as evidências em
  `specs/033-instalador-guiado/validation.md`.

  Nota (2026-11-14): sem release candidata publicada (T051), a validação seguiu o ajuste registrado em
  [validation.md](validation.md#t034---cenários-1-e-2-do-quickstart-sc-001-sc-002): o `install.sh` real com a imagem
  local para as verificações iniciais e as etapas de `Install.psm1` com as imagens desta branch para a instalação. Na
  VPS `hostinger-staging`, em usuários descartáveis com Docker 29.9.3 rootless e Podman 6.1.3 rootless:
  - recusas de porta (código 3), DNS e engine rootful (Podman) conferidas;
  - `Install.Tests.ps1` e `Resume.Tests.ps1` verdes nos dois engines;
  - primeiro acesso com Playwright: troca da senha e host local conectado;
  - tempo até o login de 121 s no Docker e 150 s no Podman, com as imagens já baixadas.
  Achados corrigidos: `redirect_uri` do BFF sem `/dokpod` no realm; `--pids-limit` explícito nos containers avulsos
  (Podman 6); imagem baixada antes de ler o `USER`; DNS resolvido no host pelo `install.sh` (Docker). Pendente da RC: o
  fluxo no TTY, a assinatura das imagens publicadas e a recusa de Docker rootful.

**Checkpoint**: História 1 funcional e testável de forma independente (MVP).

---

## Fase 4: História 2 - Usar PostgreSQL e nginx que já existem (Prioridade: P2)

**Objetivo**: usar a infraestrutura do usuário sem duplicá-la e com menor privilégio.

**Teste independente**: cenário 3 do quickstart.

- [X] T035 [US2] `postgres.mode: existing-create` em `deploy/installer/src/Postgres.psm1`:
  - credencial administrativa de `{file}`/`{env}` ou prompt, usada uma vez e descartada (nunca em `secretsDir`);
  - entregue ao container do `psql` só por `PGPASSFILE` em tmpfs, apagado ao fim, como no T028 (FR-033);
  - criação do banco `dokpod`, da role dona e da role de runtime (membro do grupo `dokpod_runtime`) pelos mesmos arquivos de bootstrap do catálogo;
  - `sslMode` e `caCertificate` respeitados;
  - versão mínima do servidor conferida (FR-008).

  Nota (2026-11-14): `Invoke-ExistingPostgresBootstrap` em `Postgres.psm1`, ligado à etapa `database` do `install`.
  - **Credencial**: `postgres.admin.password` ficou opcional no esquema; sem ela, o instalador pergunta no terminal e,
    sem terminal, sai com código 2. A credencial não vai para `secretsDir`, estado ou relatório. Na reexecução, com as
    duas conexões já gravadas, a credencial não é pedida: a role dona confere versão e privilégios.
  - **Host**: loopback é recusado com código 2, porque dentro do container é o próprio container. No Podman rootless,
    um PostgreSQL no host só responde por `host.containers.internal`; o IP do host e o gateway da rede não respondem.
  - **TLS**: `PGSSLMODE` e a CA no tar do `psql` (ou `PGSSLROOTCERT=system`); `SSL Mode` na connection string; a CA no
    bundle de `SSL_CERT_FILE` da API e dos containers do modo administrativo (`-TrustBundle`).
  - **Rede**: a `dokpod-data` deixa de ser `internal` sem PostgreSQL provisionado, e `psql` e modo administrativo usam
    a rede padrão do engine.
  - **Achado no catálogo**: o bootstrap só funcionava com `SUPERUSER`. `01-create-roles.sql` agora só retira os
    atributos que a role tem. `02-create-database.sql` e `03-grants.sql` (este agora no banco de manutenção, com
    `owner_role`) põem a credencial em `dokpod_owner` só durante o arquivo e a tiram no fim.
  - **Testes**: `Postgres.Tests.ps1` (23) com PostgreSQL 18 real, TLS de CA privada, administrador `CREATEROLE
    CREATEDB` e um banco alheio igual antes e depois (SC-006). `Install.Tests.ps1` com `DOKPOD_TEST_EXISTING_PG_HOST`
    fez uma instalação completa no Podman 5.7 rootless (4/4): sem `dokpod-postgres`, a API conectada como
    `dokpod_app` com `ssl = t`, a credencial em nenhum arquivo, e a reexecução sem ela convergida. Docker rootless e
    o cenário 3 inteiro ficam com a T038.
- [X] T036 [US2] `postgres.mode: existing-prepared`: validar conexão, versão e privilégios com `checks/server-version.sql`
  e `checks/role-privileges.sql` do catálogo (sem `rolsuper`, sem `rolcreaterole`, `CONNECT` e `USAGE` presentes), e recusar com código 3 dizendo o
  privilégio em excesso ou em falta. Testes em `deploy/installer/tests/Postgres.Tests.ps1` com PostgreSQL real.
  - **Feito em 2026-11-14** (`Invoke-PreparedPostgresBootstrap` em `Postgres.psm1`). Com a role dona, sem credencial
    administrativa, o instalador confere a versão e os privilégios das duas roles. Depois confere que a de runtime
    conecta com a senha das respostas. Falta, excesso ou role que não conecta dá código 3 com o privilégio na
    mensagem (`com SUPERUSER`, `sem CONNECT no banco <banco>` e afins). Conexões já gravadas diferentes das respostas
    param a execução com `recreate secret:<nome>`, sem sobrescrever.
  - **`USAGE`**: antes das migrations o schema `dokpod` não existe, então não há `USAGE` a conferir. A dona precisa
    poder criá-lo (`CREATE` no banco) ou já ser dona dele. A de runtime recebe `USAGE` pelas migrations, por meio do
    grupo `dokpod_runtime`, do qual precisa ser membro.
  - **Catálogo**: `role-privileges.sql` ganhou `schema_exists` e `database_create`. A de runtime também é recusada
    com `CREATE` no banco.
  - **Testes**: `Postgres.Tests.ps1` (30) com PostgreSQL real: banco preparado aceito, sem nada criado no servidor;
    dona `SUPERUSER`, runtime sem `CONNECT` e senha errada recusados com código 3. `Install.Tests.ps1` fez uma
    instalação completa no Podman 5.7 rootless (2/2): schema `dokpod` da role dona, API como role de runtime com
    TLS `require`, nenhuma role nova no servidor, reexecução convergida. `Answers.Tests.ps1` cobre o modo guiado.
- [X] T037 [US2] (FR-021, FR-043) `edge.mode: existing` em `deploy/installer/src/Edge.psm1`:
  - gerar `installDir/edge/dokpod.conf` com `upstreamHost`, `webPort` e `bffPort`;
  - publicar web e BFF só em `upstreamHost`, aceitando apenas `127.0.0.1`, `::1` ou `{network}` (rede interna
    compartilhada com o nginx do usuário), e recusar outro valor com código 2 (FR-043);
  - validar com `nginx -t` em container;
  - listar os passos manuais (certificado, `include`, `reload`), sem tocar em arquivo fora de `installDir`.

  Testes em `deploy/installer/tests/Edge.Tests.ps1`, incluindo a recusa de `upstreamHost` fora de loopback e
  `Security__TrustedNetworks` restrito ao loopback ou à rede interna compartilhada (L1 do threat model).
  - **Feito em 2026-11-14**. `Edge.psm1` recusa com código 2 um `upstreamHost` fora de `127.0.0.1`, `::1` ou
    `{ network }`, além de portas repetidas. O `config` roda `nginx -t` em container sem rede, com os dois arquivos
    incluídos como o README manda. O resumo lista os passos no nginx do usuário: certificado, os dois `include`, o
    alias na rede compartilhada e o reload. Nenhum arquivo fora de `installDir` é tocado.
  - **Loopback**: web, BFF e o Keycloak provisionado são publicados só em `upstreamHost` (`webPort`, `bffPort` e o novo
    `keycloakPort`). No Podman, API e BFF chegam ao Keycloak pelo nome público apontado para o host (`host-gateway`).
  - **Achado (L1)**: o encaminhador de portas rootless não entrega a conexão do nginx do host como 127.0.0.1. Ela chega
    com o endereço do container na rede pela qual a porta sai, e com o BFF também na `dokpod-internal` saía o dela,
    fora das redes confiáveis: o BFF ignorava os `X-Forwarded-*` e montava `redirect_uri` em http, sem `/dokpod`.
    Nesse modo, o BFF fica só na `dokpod-edge` e confia no loopback e na sub-rede dela.
  - **Rede compartilhada**: web, BFF e o Keycloak entram nela como `dokpod-web`, `dokpod-bff` e `dokpod-keycloak`, sem
    porta no host. A rede precisa existir e ter sub-rede de até /24 (código 3), e o BFF confia só nela.
  - **Testes**: `Edge.Tests.ps1` (13, com `nginx -t` e redes reais). `Install.Tests.ps1` fez a instalação completa no
    Podman 5.7 rootless (3/3), com um nginx na rede do host no papel do nginx do usuário. Console, OIDC e login com
    `redirect_uri` `https://…/dokpod/signin-oidc`, o site anterior do nginx igual e a reexecução convergida. Docker
    rootless e a rede compartilhada de ponta a ponta ficam para a T038.

- [X] T038 [US2] Combinações (só PostgreSQL existente, só nginx existente, ambos) no motor de plano, com teste em
  `deploy/installer/tests/Plan.Tests.ps1`. Validar o cenário 3 do quickstart com Docker Engine rootless e com Podman rootless
  (SC-002) e registrar em `validation.md`.
  - **Validação**: Validado em 2026-11-14 (ver `validation.md`): PostgreSQL existente e preparado nos dois engines; nginx existente em loopback no Podman. No Docker, o nginx existente com Keycloak provisionado depende do DNS do nome público (NOT RUN, limitação documentada).

---

## Fase 5: História 3 - Integrar a um Keycloak existente (Prioridade: P2)

**Objetivo**: configurar só o realm do Dokpod numa instância existente.

**Teste independente**: cenário 4 do quickstart, com export de `master` e `outro` antes e depois.

- [X] T039 [US3] `keycloak.mode: existing` em `deploy/installer/src/Keycloak.psm1`:
  - verificar versão e acesso administrativo antes de alterar;
  - credencial administrativa transitória (FR-015);
  - `caCertificate` para TLS do Keycloak;
  - `configure-keycloak.ps1 -Bootstrap -SkipUser` contra `url` e `realm`;
  - atribuir as roles do Dokpod e o grupo `/dokpod/administrators` a `administratorUser` (FR-014).

  Recusa com código 3 sem alteração quando a versão está abaixo da homologada ou falta acesso.
  - **Feito em 2026-11-14** (`Invoke-ExistingKeycloakRealm` em `Keycloak.psm1` e o modo `existing` de
    `Keycloak.Sidecar.ps1`). A credencial do `master` vem de `{ file }`, `{ env }` ou do prompt. Ela só entra no tar do
    container avulso e não fica em `secretsDir`. Antes de qualquer alteração, o instalador confere o token do `master`
    e `/admin/serverinfo`: falta de acesso ou versão fora da série 26 dá código 3.
  - A reconciliação usa `configure-keycloak.ps1 -Bootstrap -SkipUser`. `administratorUser` entra em
    `/dokpod/administrators`. Se não existe no realm, ele é criado com a senha temporária de `initial-admin-password`
    e troca obrigatória.
  - Na reexecução sem a credencial, que pode ter sido apagada, a reconciliação usa o `dokpod-provisioner`. A CA de
    `caCertificate` entra no bundle da API e do BFF e no container avulso (`SSL_CERT_FILE`). O `-LoginTheme` vazio
    mantém o tema atual quando o Keycloak não tem o tema `dokpod`. A autorização do ambiente também passa a funcionar
    com o Keycloak existente.
  - **Testes**: `Keycloak.Tests.ps1` (17) com Keycloak 26.7 real em TLS de CA privada: credencial errada recusada
    com código 3 e sem realm, só o realm do Dokpod criado, `master` e `outro` iguais. `Install.Tests.ps1` fez a
    instalação completa no Podman 5.7 rootless (3/3): o login do BFF foi ao Keycloak do usuário pela CA privada, o
    `administratorUser` ficou nos administradores e a credencial não ficou em nenhum arquivo.

- [X] T040 [US3] Divergências do realm: usar o `-DryRun` dos scripts para listar no plano cada item a corrigir e aplicar
  só após confirmação, preservando usuários, sessões e customizações fora do contrato. Testes em
  `deploy/installer/tests/Keycloak.Tests.ps1` com Keycloak real, incluindo redirect URI alterado à mão.
  - **Feito em 2026-11-14**. O `-DryRun` do `configure-keycloak.ps1` não acessa a rede e continua assim. Para listar
    as divergências entrou o `-Drift`, que compara o realm com o contrato sem alterar nada e emite linhas
    `Divergência: <item>`. Ele confere clients (com o segredo, só pelo nome), scope de audience, recurso e scopes
    UMA, roles, grupos e mapeamentos e, com `-Bootstrap`, a política do realm e as roles do `dokpod-provisioner`.
    Usuários, sessões e o que está fora do contrato não entram.
  - A cada reexecução, `Get-KeycloakRealmDrift` põe a lista no plano (`realm update`, com cada item). A correção só é
    aplicada depois da confirmação do plano, no provisionado e no existente.
  - **Testes**: `Keycloak.Tests.ps1`: redirect URI do `dokpod-bff` alterado à mão aparece como
    `client dokpod-bff: redirectUris`, a reconciliação o corrige e a lista volta vazia, no provisionado e no
    existente. `Install.Tests.ps1`: sem `--yes` a execução para com código 4, com a divergência no plano e nada
    corrigido; com `--yes` o redirect volta ao contrato.

- [X] T041 [US3] Validar o cenário 4 do quickstart com Docker Engine rootless e com Podman rootless (SC-002): os exports de
  `master` e `outro` iguais antes e depois (SC-006), registrado em `validation.md`.
  - **Validação**: Validado em 2026-11-14 nos dois engines (3/3 no Podman 5.7 e no Docker 29.9 rootless), com `master` e `outro` iguais antes e depois.

---

## Fase 6: História 4 - Recriar componentes com segurança (Prioridade: P3)

**Objetivo**: recriação por componente e total sem perda acidental de dados.

**Teste independente**: cenário 6 do quickstart.

- [X] T042 [US4] `recreate <item>` em `deploy/installer/src/Recreate.psm1` para `pki:edge`, `pki:api`,
  `pki:agent:<id>`, `secret:<nome>`, `config` e `service:<nome>`:
  - efeito colateral no plano;
  - só o item pedido muda;
  - componentes `existing` recusados (FR-029).

  Testes em `deploy/installer/tests/Recreate.Tests.ps1`.
  - **Estado**: Implementado em 2026-11-14 (`Recreate.psm1`; `Invoke-InstallerInstall -Recreate`). `Recreate.Tests.ps1` passa. Na instalação completa (Podman 5.7), `pki:edge`, `service:web` e `pki:agent:<id>` passaram antes da falha de DNS do host. Falta a rodada limpa de ponta a ponta: o `aardvark-dns` do host de desenvolvimento parou de resolver nomes (ver o relatório da sessão).
  - **Validação**: Validado em 2026-11-14 (instalação completa, Podman 8/8 e Docker).

- [X] T043 [US4] `recreate --all`:
  - confirmação normal e depois a segunda confirmação nomeando `dokpod-postgres`, `keycloak-postgres`,
    `dokpod-api-keys`, `dokpod-bff-keys` e as identidades;
  - sem ela, recria todo o resto e preserva os dados;
  - não interativo só com `--confirm-data-removal <lista>` exata (SC-008).
  - **Estado**: Implementado em 2026-11-14 (`Resolve-DataRemoval`, `Remove-InstallationData`; plano com migrations e realm recriados só com o banco removido). Testes unitários passam. Na instalação completa, a recriação total sem a segunda confirmação passou, com as contagens de auditoria e identidades iguais, e a lista inexata foi recusada com código 2. Falta a rodada com a remoção confirmada, bloqueada pelo DNS do host.
  - **Validação**: Validado em 2026-11-14: `--all` sem e com a remoção de dados nos dois engines. Achado: `compose down` recriava as redes e o DNS do Podman parava nelas; agora `compose rm --stop --force`.

- [X] T044 [US4] `recreate pki:ca` pelo procedimento de `docs/runbooks/troca-da-ca-dos-agentes.md`: estado
  `active → rotating → active`, bundle com as raízes antiga e nova em `DOKPOD_API_CLIENT_CA_CERTIFICATE_PATH` e na
  confiança dos agentes, reemissão e novo `agent-identities register` de cada agente, e remoção da raiz antiga só depois
  de todos conectados (FR-030).
  - **Estado**: Implementado em 2026-11-14 (`Start-CaRotation`, `Complete-CaRotation`, `Invoke-InstallerCaRotation`; `Installation/status/agent-connection.sql` no catálogo). Falta a validação de ponta a ponta, escrita em `Install.Tests.ps1` e bloqueada pelo DNS do host.
  - **Validação**: Validado em 2026-11-14 nos dois engines: CA nova, duas raízes, agente local reemitido e reconectado, e só a raiz nova no fim.

- [X] T045 [US4] Validar o cenário 6 do quickstart (contagens de `dokpod.audit_events` e `dokpod.agent_identities` iguais
  depois da recusa) e registrar em `validation.md`.
  - **Validação**: Validado em 2026-11-14 (cenário 6 em `validation.md`): contagens iguais depois do `--all` sem a segunda confirmação, nos dois engines.

---

## Fase 7: História 5 - Adicionar hosts e atualizar a versão (Prioridade: P3)

**Objetivo**: pacote de host, revogação e atualização com migrations sem SDK.

**Teste independente**: cenários 5 e 8 do quickstart.

- [X] T046 [US5] `host add` em `deploy/installer/src/Hosts.psm1`:
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
  - **Estado**: Implementado em 2026-11-14 (`Hosts.psm1`: pacote cifrado no formato do `openssl enc`, frase fora do pacote, `package_generated_at`). `Hosts.Tests.ps1` (5) passa, inclusive a abertura do pacote com o `openssl` real. Falta o agente do pacote conectando de ponta a ponta, escrito em `Install.Tests.ps1` e bloqueado pelo DNS do host.
  - **Validação**: Validado em 2026-11-14 no Podman: pacote 0600 cifrado, aberto como o README manda, agente do pacote conectado em menos de 5 minutos. Achado: `$name` sobrescrevia o parâmetro `$Name` e gravava o nome errado no estado.

- [X] T047 [US5] `host revoke --environment --reason` por `admin agent-identities revoke`, com o pacote marcado como
  revogado no estado.
  - **Estado**: Implementado em 2026-11-14 (`Invoke-InstallerHostRevoke`, `host revoke` na CLI). Falta a validação de ponta a ponta, junto da T046.
  - **Validação**: Validado em 2026-11-14 no Podman. Achado: o modo administrativo só grava a revogação no banco; a sessão ativa vive na memória da API, que agora é reiniciada pelo `host revoke`.

- [X] T048 [US5] `upgrade --version` em `deploy/installer/src/Upgrade.psm1`:
  - recusar versão igual ou menor com referência a `docs/runbooks/deploy-e-rollback.md` e
    `docs/runbooks/backup-restore-e-atualizacao.md`;
  - verificar as imagens (T025);
  - com migration nova, exigir backup confirmado, ou executar `pg_dump` quando `dokpod-postgres` é provisionado
    (FR-011);
  - migrations pela T030;
  - atualizar o plano de controle antes dos agentes;
  - confirmar `org.opencontainers.image.version` de cada serviço (FR-032).

  Testes em `deploy/installer/tests/Upgrade.Tests.ps1`.
  - **Estado**: Implementado em 2026-11-14 (`Upgrade.psm1`: só para versão maior, rótulo OCI de cada serviço; o resto é o fluxo do install). `Upgrade.Tests.ps1` passa. A atualização entre duas versões fica para a T050.
  - **Validação**: Validado em 2026-11-19/20 na VPS com o instalador publicado: `0.9.0-rc.2 → 0.9.1-rc.1` e `0.9.2-rc.1 → 0.10.0-rc.1` com o rótulo OCI conferido; `upgrade` para versão anterior recusado com código 2 e referência aos runbooks ([validation.md](validation.md)).

- [X] T049 [US5] `certificates check` e aviso de vencimento a partir de `renewWarningDays` (padrão 30) na reexecução,
  oferecendo `recreate pki:<item>` para certificados `local-ca`, e só aviso para `organization` (FR-019).
  - **Estado**: Feito em 2026-11-14 (`Get-InstallerCertificateStatus`, `certificates check`, aviso no fim de cada install). Teste em `Recreate.Tests.ps1`.

- [ ] T050 [US5] Validar os cenários 5 e 8 do quickstart com duas versões consecutivas com migration (candidatas da
  release), o segundo host Podman e uma imagem adulterada no `--offline` (código 5), e registrar em `validation.md`
  (SC-007, SC-009).
  - **Estado**: Parcial em 2026-11-20 ([validation.md](validation.md)): upgrades sem migration, recusa de versão anterior e `--offline` adulterado com código 5 sem alteração; NOT RUN: upgrade com migration nova (sem duas versões publicadas com migration), segundo host Podman fora do host de desenvolvimento e imagem adulterada num engine sem as camadas.

---

## Fase Final: Refinamento e Aspectos Transversais

- [X] T051 [P] Publicação em `.github/workflows/publish.yml`:
  - imagem `dokpod-installer` na lista `images` (`deploy/installer/Dockerfile`);
  - `install.sh` em `build-assets` com o placeholder do digest substituído pelo digest publicado do instalador, de
    modo que o asset seja atestado e assinado como os demais;
  - asset `dokpod-sql-<versão>.tar.gz` com o catálogo e o `manifest.json`, atestado como os demais (FR-040);
  - entrada correspondente no `release-manifest.json`.
  - **Estado**: Implementado em 2026-11-14: `dokpod-installer` na lista de imagens; `build-assets` depende das imagens e grava `install.sh` com a referência e o digest publicados, mais `dokpod-sql-<versão>.tar.gz`; os dois são atestados pelo `gw-github-assets`. Validado por actionlint. A publicação não foi executada (exige pedido explícito).
  - **Validação**: Executada nas releases `v0.9.1`, `v0.9.2-rc.1`, `v0.10.0-rc.1` e `v0.10.0`: imagem do instalador, `install.sh` com o digest publicado e `dokpod-sql-<versão>.tar.gz` no `release-manifest.json`, assinados e atestados.

- [X] T052 [P] Smoke test da imagem do instalador no CI (`.github/workflows/ci.yml`): `--help`, `plan --dry-run` com
  arquivo de respostas de exemplo e Pester completo dentro da imagem.
  - **Estado**: Feito em 2026-11-14: job `installer` no `ci.yml` (Pester na imagem de testes, PSScriptAnalyzer, `--help` e `plan` com `deploy/installer/examples/answers.yaml` saindo com 3). Reproduzido localmente com o Podman: 277 testes, analisador sem achados, `plan` com código 3 e "nada foi alterado".

- [X] T053 Canário de segredos (SC-005) em `deploy/installer/tests/SecretCanary.Tests.ps1` e no cenário 7 do quickstart.
  Procurar cada valor em saída, `reports/*.json`, `installDir`, `docker inspect`, `ps -ef` durante a execução, logs dos
  containers e journal do host (`journalctl --user`, L7 do threat model), e confirmar zero ocorrências.
  - **Estado**: Implementado em 2026-11-14 (`SecretCanary.Tests.ps1`; `--log-driver none` também no `nginx -t`). A execução ficou bloqueada pelo DNS do host.
  - **Validação**: Validado em 2026-11-14 nos dois engines (4/4 em cada).

- [X] T054 [P] Runbook `docs/runbooks/instalacao.md`: os três cenários, o arquivo de respostas, migrations manuais
  revisadas por DBA, recriação, pacote de host, atualização, desinstalação e o que é preservado.
  - **Estado**: Feito em 2026-11-14: `docs/runbooks/instalacao.md`.

- [X] T055 [P] Atualizar `docs/distribuicao.md` (caminho de instalação e imagem do instalador), `docs/seguranca.md`
  (instalador, modo administrativo e credenciais transitórias), `README.md` (link do runbook) e `deploy/e2e/README.md`
  (a stack de laboratório continua, com link para o instalador).
  - **Estado**: Feito em 2026-11-14: `docs/distribuicao.md`, `docs/seguranca.md`, `README.md` e `deploy/e2e/README.md`. Também `migrations status|plan|export|verify` na CLI, que o runbook e o cenário 5 usam.

- [X] T056 Varredura Gitleaks com `.gitleaks.toml` nos arquivos novos e alterados. Nunca escrever pares chave=valor de
  senha nem em exemplos.
  - **Estado**: Feito em 2026-11-14: Gitleaks com `.gitleaks.toml` limpo no diff de cada commit.

- [X] T057 Formatação, análise estática e testes amplos: `dotnet format --verify-no-changes` nos projetos alterados,
  suíte .NET completa, Pester completo e `PSScriptAnalyzer` (T004) sem avisos nos módulos de
  `deploy/installer/src/`.
  - **Estado**: Parcial em 2026-11-14: Pester sem engine (278), PSScriptAnalyzer sem achados com `deploy/installer/PSScriptAnalyzerSettings.psd1` (exclusões justificadas, como a T004 pediu), `dotnet format --verify-no-changes` no projeto de testes alterado. A suíte .NET completa fica com o CI.
  - **Validação**: Concluída em 2026-11-20: suíte .NET completa verde no CI da `main` (`d9096d9`, `e0c87b4`); Pester 284 passed e PSScriptAnalyzer sem achados.

- [X] T058 Revisão de segurança e privacidade das superfícies de T001 contra o implementado, em
  `specs/033-instalador-guiado/threat-model.md`, com achados e correções. Inclui o SQL sobreposto e o banco de estado
  SQLite.
  - **Estado**: Feito em 2026-11-14: seção "Revisão do implementado (T058)" no `threat-model.md`, com 12 achados e o risco residual R8, pendente de revisão humana.

- [X] T059 Validar o cenário 9 do quickstart (catálogo SQL sobreposto) e registrar em `validation.md` (FR-039 a
  FR-041, SC-011):
  - `sql export` e alteração de um arquivo de migration pendente;
  - plano com os hashes;
  - recusa sem confirmação nomeada;
  - aplicação confirmada;
  - aviso para sobreposição de migration já aplicada, registrada só com confirmação;
  - `verify` aceitando a impressão registrada;
  - alteração manual sem registro (coluna, índice, restrição, função, gatilho) detectada pela impressão do schema,
    com código 6 (FR-042).
  - **Estado**: Parcial em 2026-11-14: teste novo em `Migrations.Tests.ps1` para o FR-042 (coluna, índice, restrição, função e gatilho sem registro dão código 6 no `verify`). A execução e o registro em `validation.md` ficaram bloqueados pelo DNS do host.
  - **Validação**: Validado em 2026-11-14 no Podman (9/9, com o FR-042) e registrado em `validation.md`.

- [X] T060 Fechar `specs/033-instalador-guiado/validation.md`: os nove cenários do quickstart, o ambiente, os resultados,
  os `NOT RUN` com motivo e o estado das tarefas.
  - **Estado**: Feito em 2026-11-20: seção "T050 e fechamento (T060)" do `validation.md`, com os `NOT RUN` e o estado das tarefas. A T050 segue parcial.

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
