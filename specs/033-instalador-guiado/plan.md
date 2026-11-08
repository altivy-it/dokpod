# Plano de Implementação: Instalador guiado do Dokpod

**Branch**: `033-instalador-guiado` | **Data**: 2026-11-08 | **Spec**: [spec.md](spec.md)

**Entrada**: Especificação da funcionalidade em `/specs/033-instalador-guiado/spec.md`

**Nota**: Este template é preenchido pelo comando `/speckit-plan`; sua definição descreve o fluxo de execução.

> Redija este plano e todos os artefatos derivados em português brasileiro (pt-BR). Preserve nomes de código, caminhos, comandos e identificadores técnicos.

## Resumo

Um instalador único leva um host Linux com Docker ou Podman do zero ao Dokpod em execução, nos três cenários da spec
(tudo novo, PostgreSQL e/ou nginx existentes, Keycloak existente). Ele também atualiza e recria componentes, e é
idempotente.

- **Forma**: `install.sh` (asset da release) baixa e verifica a imagem `dokpod-installer`, que traz PowerShell 7,
  Docker CLI com Compose v2, `openssl` e `psql`, e orquestra tudo pelo socket do engine
  ([D-001](research.md#d-001-forma-do-instalador), [D-002](research.md#d-002-orquestração-de-compose-em-docker-e-podman)).
- **Migrations e bootstrap sem SDK**: a imagem da API ganha um modo administrativo (`admin migrations`,
  `admin environments`, `admin agent-identities`). O usuário vê o plano de migrations, exporta o SQL exato para revisão,
  aplica pelo instalador ou à mão e confere depois ([D-003](research.md#d-003-migrations-sem-sdk-e-com-controle-do-usuário),
  [D-004](research.md#d-004-cadastro-de-ambiente-e-identidade-do-agente-sem-sql-manual)).
- **Keycloak**: reaproveita `configure-keycloak.ps1` e `provision-environment-authorization.ps1`, que já são idempotentes.
- **Certificados**: CA local por `openssl` ou certificados da organização, sempre validados
  ([D-005](research.md#d-005-pki-e-validação-de-certificados)).
- **Segredos**: só por arquivo montado (`secrets:` e provedor *key-per-file* na API e no BFF)
  ([D-006](research.md#d-006-segredos-entregues-aos-serviços-por-arquivo)).
- **SQL só em arquivos**: todo SQL do instalador e do modo administrativo fica num catálogo `.sql` com manifesto, o
  mesmo que as migrations do EF Core carregam. Ele é distribuído para revisão e pode ser sobreposto com confirmação
  nomeada e registro ([D-013](research.md#d-013-catálogo-de-arquivos-sql-e-sobreposição-externa)).
- **Idempotência**: plano calculado do estado observado mais um estado sem segredos em `installDir/state`
  ([D-007](research.md#d-007-estado-plano-e-retomada)).

## Contexto Tecnico

**Linguagem/Versão**:
- PowerShell 7.5 (instalador, na imagem `dokpod-installer`);
- POSIX `sh` (bootstrap `install.sh`);
- C# / .NET 10 (modo administrativo da API e leitura de segredos por arquivo na API e no BFF).

**Dependências Principais**:
- Na imagem do instalador:
  - Docker CLI e plugin Compose v2 (Apache-2.0);
  - `openssl` e `postgresql-client` da distribuição base;
  - `Microsoft.Data.Sqlite` (MIT) com `SQLitePCLRaw.bundle_e_sqlite3` (Apache-2.0), para o estado em SQLite
    (SQLite em domínio público);
  - Pester 5.7.x (Apache-2.0) e PSScriptAnalyzer 1.24.x (MIT), só para testes e análise.

  Imagem base a confirmar na matriz `lzocateli/containers`; com PowerShell, a candidata é derivar de
  `mcr.microsoft.com/powershell:7.5-ubuntu-24.04` (MIT), que só pode entrar depois de registrada na matriz de
  `docs/distribuicao.md`.
- Cosign em container por digest fixado, como já é usado na validação da release.
- `Microsoft.Extensions.Configuration.KeyPerFile` (MIT, ASP.NET Core 10).
- Nenhum pacote pago, copyleft ou sem licença verificada.

**Persistência**:
- Nenhuma tabela nova. Usa `dokpod.environments`, `dokpod.agent_identities`, `dokpod.audit_events` (ação nova
  `environment.agent_identity_register`) e `__EFMigrationsHistory`.
- Estado do instalador em SQLite local (`installDir/state/installation.db`), com schema e consultas em arquivos SQL
  de `deploy/installer/sql/state/` ([D-014](research.md#d-014-estado-da-instalação-em-sqlite),
  [data-model.md](data-model.md#estado-da-instalação-sqlite)).

**Testes**:
- Pester na imagem do instalador (plano, validação de respostas e certificados, renderização de compose e borda);
- `Dokpod.ControlPlane.Api.Tests` para o modo administrativo contra PostgreSQL 17.6 real, incluindo
  `apply` versus SQL exportado (SC-010) e role `dokpod_runtime` sem DDL (D-009);
- `Dokpod.Bff.Tests` para a leitura por arquivo;
- ponta a ponta pelos cenários do [quickstart](quickstart.md) em hosts descartáveis com Docker rootless e Podman rootless, com o
  navegador pela borda do próprio instalador.

**Plataforma Alvo**: host Linux x86-64 com Docker Engine ≥ 27 rootless ou Podman ≥ 5 rootless, com Compose. O modo
rootful é recusado ([D-015](research.md#d-015-somente-engines-rootless)). Os
serviços do Dokpod e a infraestrutura provisionada rodam em containers atrás da borda nginx.

**Tipo de Projeto**: ferramenta de instalação e operação do plano de controle self-hosted, mais um modo administrativo na
imagem da API.

**Metas de Desempenho**:
- instalação completa em até 20 minutos sem contar download (SC-001);
- reexecução convergida em até 2 minutos (SC-003);
- novo host conectado em até 5 minutos (SC-009).

**Restrições**:
- **Execução**: tudo em containers, e o host só orquestra.
- **Segredos**: nunca em saída, relatório, `inspect` ou linha de comando; falha fechada em destino existente.
- **Imagens**: só por digest assinado.
- **Migrations**: forward-only, com confirmação ligada ao `planHash`.
- **Isolamento**: realm `master` e outros realms intocados; banco do Dokpod separado do do Keycloak.
- **Engine**: socket nunca exposto por rede.
- **Compatibilidade**: com o deploy hook.

**Escala/Escopo**: um host de plano de controle e N hosts de agente por pacote. Fora: alta disponibilidade, Kubernetes,
ACME, agente Windows (exceto o certificado e o cadastro do ambiente).

## Verificação da Constituição

*GATE: Deve ser aprovado antes da pesquisa da Fase 0 e revisado novamente após o design da Fase 1.*

| Princípio | Como o plano atende | Situação |
| --- | --- | --- |
| I. Documentação em pt-BR | Spec, plano, contratos, runbook de instalação e ajuda do instalador em pt-BR; identificadores preservados. | Atende |
| II. Engine local como fonte e workloads preservados | O instalador não altera containers de workload. A recriação nunca remove volumes de workloads, e "recriar tudo" só remove dados do próprio Dokpod com segunda confirmação nomeada. O inventário continua projeção. | Atende |
| III. Identidade externa e menor privilégio | Keycloak continua a autoridade, e só o realm do Dokpod é alterado. A credencial do `master` ou do PostgreSQL é usada só durante a execução. O runtime usa `dokpod_runtime`, sem DDL. O modo administrativo exige `dokpod_owner`, que o runtime não tem. Agentes com identidade individual e revogação. | Atende, com threat model obrigatório (superfície privilegiada nova) |
| IV. Operações duráveis e recuperáveis | Plano e retomada a partir do observado, *lock*, `planHash` para migrations, backup exigido antes de migration, auditoria dos comandos administrativos e relatório por execução. | Atende |
| V. Arquitetura modular e compatibilidade | O modo administrativo reusa serviços de aplicação, sem SQL próprio no instalador. Compatibilidade N/N-1 preservada (servidor antes de agentes). Formato do estado versionado. O instalador fica em `deploy/installer`, e nada depende dele. | Atende |
| VI. Qualidade verificável por plataforma | Docker e Podman rootless reais. PostgreSQL real para migrations e privilégios. Navegador pela borda nginx. Imagem do instalador com build, SBOM, assinatura e smoke. | Atende |
| Restrição: toolchains em containers | O host só precisa do engine; `install.sh` só baixa, verifica e executa a imagem. | Atende |
| Restrição: dependências | Licenças verificadas acima; a imagem base exige registro na matriz antes do uso. | Atende, com pendência de registro |
| Restrição: novo datastore ou serviço | Nenhum serviço novo; PostgreSQL, Keycloak e nginx já são da arquitetura. O SQLite é um arquivo local de estado do instalador, não um datastore do produto, e é registrado no ADR do instalador (T003). | Atende, com registro em ADR |

Reavaliação pós-desenho (Fase 1): sem violações. Duas decisões duradouras pedem ADR `proposed`, com revisor humano:
- D-003 e D-004, modo administrativo da imagem da API;
- D-001, imagem `dokpod-installer` como artefato da release.

## Estrutura do Projeto

### Documentação desta Funcionalidade

```text
specs/033-instalador-guiado/
├── spec.md
├── plan.md                      # este arquivo
├── research.md                  # D-001 a D-015
├── data-model.md                # layout, estado, componentes, segredos, pacote de host
├── quickstart.md                # cenários 1 a 8
├── contracts/
│   ├── installer-cli.md         # comandos, opções e códigos de saída
│   ├── admin-commands.md        # modo administrativo da imagem da API
│   ├── migration-script.md      # formato do SQL exportado
│   └── answers.schema.json      # arquivo de respostas sem segredos
├── checklists/requirements.md
└── tasks.md                     # gerado por /speckit-tasks
```

### Código-Fonte (raiz do repositório)

```text
deploy/installer/
├── install.sh                   # bootstrap POSIX; digest do instalador gravado na publicação
├── Dockerfile                   # imagem dokpod-installer
├── src/                         # módulos PowerShell: respostas, pré-requisitos, plano, pki, postgres,
│                                # keycloak, compose, borda, migrations, ambientes, pacote de host, relatório
├── sql/state/                   # schema (forward-only) e consultas do estado SQLite, com manifest.json
├── templates/                   # compose.yaml, dokpod.conf (nginx), deploy.env, compose.agent.yaml
└── tests/                       # Pester
backend/apps/Dokpod.ControlPlane.Api/
├── Admin/                       # modo administrativo: migrations, environments, agent-identities
└── Program.cs / ApiHost.cs      # despacho de `admin` antes de subir o host; key-per-file em /run/secrets
backend/apps/Dokpod.Bff/         # key-per-file em /run/secrets
backend/libs/Dokpod.ControlPlane.Infrastructure/Sql/  # catálogo SQL: manifest.json e Installation/{bootstrap,checks,status}
backend/libs/Dokpod.ControlPlane.Infrastructure/Migrations/MigrationSqlScriptLoader.cs  # raiz externa só no modo administrativo
backend/libs/Dokpod.ControlPlane.Application/  # registro de identidade de agente com auditoria
backend/libs/Dokpod.Domain/Auditing/           # ação environment.agent_identity_register
backend/tests/Dokpod.ControlPlane.Api.Tests/Admin/
tools/scripts/configure-keycloak.ps1           # reaproveitado; ajustes só se o instalador precisar de parâmetro novo
tools/scripts/provision-environment-authorization.ps1
.github/workflows/publish.yml                  # nova imagem dokpod-installer e asset install.sh
docs/runbooks/instalacao.md                    # novo: instalação, atualização, recriação e desinstalação
docs/distribuicao.md                           # matriz: imagem do instalador e caminho de instalação
docs/seguranca.md                              # seção do instalador e do modo administrativo
```

**Decisão de Estrutura**:
- O instalador é um script de instalação do produto, então fica no módulo proprietário `deploy/installer`, e não em
  `tools/scripts`, que é automação do repositório.
- Os scripts do Keycloak continuam em `tools/scripts` como fonte única e são copiados para a imagem no build.
- O modo administrativo fica na API porque reaproveita o `DbContext`, as migrations e os serviços de aplicação da mesma
  versão.

## Fases e ordem de entrega

1. **Governança e segurança**: threat model (instalador, socket montado, credenciais administrativas transitórias, modo
   administrativo, pacote de host) e os ADRs `proposed`, antes de qualquer código.
2. **Catálogo SQL e modo administrativo** (sem dependência do instalador):
   - catálogo com manifesto e varredura que proíbe SQL no código;
   - `migrations status|plan|script|apply|verify`;
   - `environments register`;
   - `agent-identities register|revoke`;
   - testes com PostgreSQL real, incluindo SC-010 e a qualificação de `dokpod_runtime` (D-009).

   Entrega valor sozinho: resolve as migrations sem SDK já na próxima release.
3. **Segredos por arquivo** em API e BFF, compatível com as variáveis de ambiente atuais.
4. **Imagem e núcleo do instalador**:
   - respostas, pré-requisitos, plano, estado, *lock* e relatório;
   - PKI e validação;
   - renderização de compose e borda.
5. **US1**: instalação completa nos dois engines.
6. **US2 e US3**: PostgreSQL, nginx e Keycloak existentes.
7. **US4**: recriação, incluindo a troca da CA.
8. **US5**: pacote de host, revogação e `upgrade`.
9. **Publicação**: imagem `dokpod-installer` e asset `install.sh` no `publish.yml`, assinatura e atestação, smoke da
   imagem e runbook.
10. **Validação**: quickstart completo, canários de segredo e `validation.md`.

## Registro de Complexidade

Sem violações da constituição a justificar.
