# ADR 2026-0010: Modo administrativo da imagem da API para migrations e bootstrap

**Status:** accepted (2026-11-08)

**Data:** 2026-11-08  
**Responsáveis:** zocatel (responsável pelo produto)  
**Origem:** IA assistida, tarefa T002 da [spec 033](../../specs/033-instalador-guiado/spec.md), autorizada pelo solicitante em 2026-11-08 ("tudo aprovado, siga com T002 a T005")  
**Revisor humano:** zocatel; aceite em 2026-11-08, registrado a pedido do solicitante, que declarou na sessão de 2026-11-08: "Aceito os ADRs, siga com T006 a T009".  
**Relacionado:** [spec 033](../../specs/033-instalador-guiado/spec.md), [plan 033](../../specs/033-instalador-guiado/plan.md) (decisões D-003, D-004, D-009, D-013 e D-015 de [research.md](../../specs/033-instalador-guiado/research.md)), [threat model 033](../../specs/033-instalador-guiado/threat-model.md) (fronteiras B4 e B5, aceito em 2026-11-08), [contrato do modo administrativo](../../specs/033-instalador-guiado/contracts/admin-commands.md), [ADR 2026-0011](2026-0011-instalador-da-release.md)

## Contexto

Até a `v0.7.0`, aplicar migrations do Dokpod exigia o SDK .NET e `dotnet ef` no host ou num container de build. A API não migra sozinha, e a release não publica artefato de migração. Em 2026-11-08, ao subir a stack local na `v0.7.0`, o script `dotnet ef migrations script --idempotent` falhou: o EF embrulha cada migration num bloco `DO`, e as migrations do Dokpod são SQL próprio (`MigrationSqlScriptLoader`) com `SELECT` de nível superior (`query has no destination for result data`). O script sem `--idempotent` aplicou as 3 migrations pendentes sem erro.

Também não há caminho sem SQL manual para cadastrar o primeiro ambiente e a identidade do agente:
- `POST /api/v1/environments` exige token de usuário e decisão do Keycloak;
- a identidade do agente (`dokpod.agent_identities`) não tem endpoint de registro, só de revogação;
- a importação de licença Business (`PUT /api/v1/licensing/license`) também exige token de usuário.

A spec 033 pede um instalador sem SDK no host que, a pedido do solicitante, dê ao usuário controle sobre o que vai ao banco: ver as migrations pendentes, exportar o SQL exato para revisão por um DBA, aplicá-lo fora do instalador e conferir depois (FR-036 a FR-038). Também pede que não haja SQL escrito no código e que os arquivos SQL possam ser alterados externamente (FR-039 a FR-042).

A escolha é duradoura porque cria um ponto de entrada novo e privilegiado na imagem publicada da API, define como toda instalação aplica migrations e serve de base para qualquer automação futura de operação.

## Forças de decisão

- **Sem toolchain no host**: nem SDK, nem `dotnet ef`, nem binário à parte.
- **Mesma versão**: o código que migra é o mesmo que roda, na mesma imagem e no mesmo digest.
- **Controle do usuário**: plano visível, confirmação ligada ao que foi visto, SQL exportável e conferência pelo schema real.
- **Menor privilégio**: o runtime não recebe a conexão de dono do schema.
- **Auditoria**: cadastro de ambiente, identidade e licença ficam na auditoria do Dokpod, sem SQL manual.
- **Reversibilidade**: o modo pode ser removido sem afetar o runtime.

## Opções consideradas

### Opção A: modo administrativo na imagem da API

`dotnet Dokpod.ControlPlane.Api.dll admin <grupo> <comando>` despacha antes de montar o host (sem Kestrel, gRPC, SignalR nem hosted services). Ele roda como container avulso da mesma imagem, com a conexão de `dokpod_owner` só por arquivo em `/run/secrets`, e oferece:
- `migrations status|plan|script|apply|verify|fingerprint`;
- `environments register`;
- `agent-identities register|revoke`;
- `licensing import`, só na Business.

Vantagens:
- nenhum artefato novo;
- usa o `DbContext`, as migrations e os serviços de aplicação da própria versão;
- serve para CE e Business.

Desvantagens:
- a imagem do runtime ganha um caminho privilegiado, que precisa de isolamento e testes;
- o modo cresce com novas necessidades de operação.

### Opção B: EF migration bundle publicado como asset

Vantagens: é o mecanismo padrão do EF Core.

Desvantagens:
- um binário a mais por edição e por RID;
- não dá plano, exportação conferível nem verificação pelo schema;
- não resolve ambiente, identidade nem licença.

### Opção C: imagem `dokpod-migrator` dedicada

Vantagens: separa o caminho privilegiado da imagem do runtime.

Desvantagens:
- duplica código e versão;
- é mais uma imagem para assinar, escanear e qualificar;
- arrisca divergir da API que vai rodar.

### Opção D: API migrando sozinha na inicialização

Vantagens: zero passos para o operador.

Desvantagens:
- tira do usuário o controle pedido;
- exige a credencial de dono no runtime;
- dificulta a migração revisada por DBA.

### Opção E: `INSERT` direto pelo instalador

Vantagens: simples.

Desvantagens:
- sem auditoria;
- duplica regra de domínio;
- acopla o instalador ao schema;
- contraria FR-039 (SQL no código).

## Decisão

Adotar a **opção A**, com estes limites:
- **Escopo**: o modo administrativo não abre portas, não aceita SQL livre e não lê caminho fora de `/run/secrets`, `/run/sql-override` e do diretório de saída montado.
- **Origem do SQL**: todo SQL vem do catálogo de arquivos `.sql` com `manifest.json` (D-013). Os arquivos de migration são os mesmos que as migrations do EF Core carregam.
- **Geração do SQL**: `IMigrator.GenerateScript` sem `--idempotent`, uma transação por migration.
- **Confirmação**: `apply` exige o `planHash` do plano mostrado ao usuário.
- **Conferência**: `verify` confere os IDs do histórico e compara a impressão do schema real com a esperada da versão, ou com a aceita depois de uma sobreposição confirmada. Isso é necessário porque `__EFMigrationsHistory` não guarda o SQL aplicado (FR-042).
- **Sobreposição de SQL** (`/run/sql-override`): só o modo administrativo a habilita. O runtime nunca a monta.
- **Conexões**: o runtime usa `dokpod_runtime` (DML e `EXECUTE`), e só o container avulso do modo administrativo recebe a conexão de `dokpod_owner`.
- **Container avulso**: `--read-only`, sem socket do engine, sem porta publicada e só na rede interna do banco (L5 do threat model).
- **Auditoria**:
  - o registro de identidade cria a ação `environment.agent_identity_register`, com ator técnico `installer`, nome do operador e justificativa;
  - ambiente e licença usam as ações existentes.

O enrollment de agentes com token de uso único continua sendo a solução de longo prazo, em spec própria, e esta decisão não a impede.

**Revisão**: reavaliar quando existir enrollment oficial, ou se o modo administrativo passar a precisar de rede, porta ou estado próprio.

## Consequências

### Positivas

- Migrations sem SDK já na próxima release, mesmo antes do instalador completo.
- DBA pode revisar, exportar e aplicar o SQL exato, e a conferência detecta alteração não registrada.
- Primeiro ambiente, identidade e licença sem SQL manual e com auditoria.

### Negativas e trade-offs

- A imagem publicada tem um caminho privilegiado a mais, protegido pela conexão de dono, que o runtime não tem.
- Quem tem a conexão de `dokpod_owner` pode vincular qualquer fingerprint a um ambiente sem enrollment (R7 do threat model, aceito).
- `expected-schema.json` precisa ser regenerado a cada migration nova, com teste que falha se estiver desatualizado.

## Segurança e privacidade

- **Conexão**: só por arquivo montado. Nunca por argumento, variável de ambiente ou saída. A saída JSON não tem segredos.
- **SQL sobreposto**: é decisão do operador (R5, aceito). Fica visível no plano, exige confirmação nomeada, é registrado e conferido pela impressão do schema.
- **Threat model**: fronteiras B4 e B5 da spec 033, aceito em 2026-11-08.

## Dados, protocolo e compatibilidade

- Nenhuma tabela nova no banco do Dokpod. Uma ação de auditoria nova na categoria `environments`.
- **Migrations**: continuam forward-only. O modo administrativo só aplica as pendentes e recusa banco com migration mais nova (código 6).
- **Formatos versionados**: o SQL exportado (`-- dokpod-migrations: 1`) e o manifesto do catálogo.
- **Protocolo**: nenhuma mudança no protocolo agente-servidor nem na API HTTP.

## Observabilidade e operação

- Cada comando devolve JSON em stdout e códigos de saída estáveis (0, 1, 2, 6, 8).
- O instalador grava o resultado no relatório de execução.
- O runbook de instalação (T054 da spec 033) passa a ser o caminho documentado para migrations, inclusive manuais.

## Validação

- **Banco real**: testes .NET contra PostgreSQL 17.6 real (T011, T012, T014, T016).
- **Equivalência**: o SQL exportado aplicado por `psql` e o `apply` produzem o mesmo schema e o mesmo histórico (SC-010).
- **Detecção**: alteração manual de coluna, índice, restrição, função e gatilho é detectada pelo `verify` sem o arquivo aplicado (SC-011).
- **Menor privilégio**: a API funciona com `dokpod_runtime` sem DDL (T016). Se não funcionar, a decisão D-009 é revista antes da entrega.
- **Isolamento**: o modo administrativo não abre porta nem inicia hosted services (T010).

## Rollout e rollback

- **Rollout**: sai como entrega intermediária (T006 a T016) numa release do Dokpod. Até o instalador existir, o runbook descreve o uso direto pelo container.
- **Rollback**: remover o despacho de `admin` não afeta o runtime. Bancos migrados pelo modo administrativo são iguais aos migrados por `dotnet ef` (SC-010), então voltar ao `dotnet ef` não exige conversão.

## Referências

- [Spec 033](../../specs/033-instalador-guiado/spec.md), FR-009 a FR-011, FR-024, FR-031 e FR-036 a FR-042
- [research.md da 033](../../specs/033-instalador-guiado/research.md), D-003, D-004, D-009, D-013 e D-015
- [Contrato do modo administrativo](../../specs/033-instalador-guiado/contracts/admin-commands.md) e [formato do SQL exportado](../../specs/033-instalador-guiado/contracts/migration-script.md)
- [Backup, restauração e atualização](../runbooks/backup-restore-e-atualizacao.md)
