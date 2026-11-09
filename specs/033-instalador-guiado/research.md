# Pesquisa: Instalador guiado do Dokpod

**Spec**: [spec.md](spec.md) · **Data**: 2026-11-08 · **Origem**: IA assistida · **Estado**: decisões propostas, sujeitas à revisão humana

Fatos de código usados como evidência (repositório na `v0.7.0`, `46416a3`):

- As 30 migrations de `backend/libs/Dokpod.ControlPlane.Infrastructure/Migrations` são, com uma exceção
  (`202609210004_AddAgentCommandDispatchFencing`, `AddColumn`/`DropColumn`), chamadas a `migrationBuilder.Sql` com
  scripts embutidos lidos por `MigrationSqlScriptLoader`. Vários scripts têm `SELECT` de nível superior (por exemplo
  `202609220002_ScheduleTemporalPartitions`).
- `dotnet ef migrations script --idempotent` embrulha cada migration num bloco `DO $EF$ … END $EF$`. Por isso o `SELECT`
  de nível superior falha (`query has no destination for result data`). O script sem `--idempotent` aplicou as 3
  migrations pendentes da `v0.7.0` sem erro (observado em 2026-11-08, ao subir a stack local).
- `POST /api/v1/environments` exige token de usuário e decisão do Keycloak
  (`backend/apps/Dokpod.ControlPlane.Api/Environments/EnvironmentEndpoints.cs`). Não há endpoint para registrar a identidade
  do agente, só para revogar. `PostgresAgentIdentityRegistry` lê `dokpod.agent_identities`.
- `tools/scripts/configure-keycloak.ps1` reconcilia o realm (com `-Bootstrap` pela credencial do `master`, ou depois pelo
  client provisionador). `tools/scripts/provision-environment-authorization.ps1` cria o recurso
  `urn:dokpod:environment:{id}`, a policy e uma permission por scope. Os dois são idempotentes e têm `-DryRun`.
- A lista de imagens publicadas é do projeto (`.github/workflows/publish.yml`, entrada `images`), e os assets da release
  saem de `build-assets`. Incluir uma imagem e um asset não muda o groundwork.
- O deploy hook (`deploy/hooks/compose-deploy-hook.sh`) recebe `COMPOSE_DIR`, `COMPOSE_FILE`, `ENV_FILE`, `PROJECT`,
  `SERVICES` e `ORDER` de um arquivo de configuração do host.

## D-001: Forma do instalador

**Decisão**: duas peças.
- **`install.sh`**: bootstrap POSIX `sh`, publicado como asset da release. Ele só detecta o engine, baixa a imagem
  `dokpod-installer` pelo digest gravado nele mesmo na publicação e a verifica com cosign (também em container, por digest
  fixado). Depois a executa com o socket do engine, o diretório de instalação e o destino de segredos montados, e
  repassa os argumentos.
- **Imagem `dokpod-installer`**: PowerShell 7.6 com Docker CLI e plugin Compose v2, `openssl` e os scripts
  do instalador. Ela traz cópias de `configure-keycloak.ps1` e `provision-environment-authorization.ps1` da mesma versão. O `psql` não entra na
  imagem: roda num container avulso da imagem PostgreSQL da matriz (`lzocateli/postgresql:18.4-pgvector0.8.6-bookworm`).

Os fontes ficam em `deploy/installer/`: são scripts de instalação, que pertencem ao módulo proprietário
(`.github/copilot-instructions.md`, "Scripts e automação").

**Justificativa**:
- O host precisa só do engine (spec, premissa aprovada 2).
- PowerShell 7 é a linguagem padrão do projeto para orquestrar CLIs e containers, e os dois scripts do Keycloak, que
  definem o contrato do realm, já estão nela; reaproveitá-los evita uma segunda implementação desse contrato.
- O digest do instalador fixado no `install.sh` liga o bootstrap à release.

**Alternativas consideradas**:
- **PowerShell instalado no host**: rejeitada, porque cria um pré-requisito fora do engine.
- **Aplicativo .NET dedicado**: rejeitado, porque reescreveria a reconciliação do Keycloak e os testes dos scripts
  existentes.
- **Bash puro no host**: rejeitado, porque teria lógica de estado e plano difícil de testar e quebraria a regra de
  toolchain em container.

## D-002: Orquestração de Compose em Docker e Podman

**Decisão**: o instalador usa sempre o Docker CLI com Compose v2 de dentro da sua imagem, apontado para o socket montado.
Só engines rootless ([D-015](#d-015-somente-engines-rootless)): no Docker rootless, `$XDG_RUNTIME_DIR/docker.sock`;
no Podman rootless, `$XDG_RUNTIME_DIR/podman/podman.sock`. O projeto Compose gerado não depende de `podman-compose`.

**Justificativa**:
- Um único comportamento de Compose, com `up --wait` e health, nos dois engines; o `podman-compose` não tem `--wait`
  (`deploy/e2e/README.md`).
- A API compatível do Podman ≥ 5 atende o Compose v2.

**Alternativas consideradas**:
- **`podman-compose` no Podman**: rejeitado, porque cria dois caminhos com semântica diferente.
- **`podman kube play`**: rejeitado, porque não serve ao Docker.

**Risco**: diferenças de rede e DNS entre os engines. A cobertura está no SC-002: os dois engines na matriz de
validação.

## D-003: Migrations sem SDK e com controle do usuário

**Decisão**: a imagem da API, CE e Business, ganha um **modo administrativo**
(`dotnet Dokpod.ControlPlane.Api.dll admin <comando>`, [contrato](contracts/admin-commands.md)). Ele roda como container
avulso da mesma imagem e da mesma versão, com a conexão do *owner* do schema, e oferece:

| Comando | Efeito |
| --- | --- |
| `migrations status` | Versão do banco e lista de pendentes. |
| `migrations plan` | Pendentes com um resumo dos objetos afetados de cada uma. Sem efeito. |
| `migrations script` | Exporta o SQL exato das pendentes, sem `--idempotent`, com cabeçalho de conferência ([formato](contracts/migration-script.md)). Sem efeito. |
| `migrations apply` | Aplica as pendentes, uma transação por migration, como o script do EF já faz. |
| `migrations verify` | Confere histórico e hashes depois de uma aplicação manual. |

O SQL de cada migration sai do gerador de SQL do próprio EF, a partir das operações dela, no formato do script sem
`--idempotent`: transação própria e linha do histórico (`MigrationScripts`). Não se usa
`IMigrator.GenerateScript(de, até)` nem `Migrate(alvo)`. O EF só reconhece um ID com 14 dígitos de data
(`yyyyMMddHHmmss_`), e os IDs deste projeto têm 12, então ele os trata como nome e não os encontra. É por isso que
`dotnet ef migrations script <de>` falhou ao subir a stack em 2026-11-08. O `apply` executa os mesmos blocos do plano
confirmado.

O SQL exportado e o `apply` começam pelo `Installation/migrations/session.sql` do catálogo (`SET TIME ZONE 'UTC'` e
`search_path` `dokpod`), igual à sessão do executor da aplicação. A migration `202609220002` calcula partições pelo fuso
da sessão, e as migrations anteriores ao isolamento do schema criam as tabelas no `search_path`. Sem isso, o `psql` do
DBA criava as primeiras tabelas em `public`, e uma partição colidia depois. O corpo de cada migration vem dos arquivos do catálogo, com a sobreposição externa quando informada
([D-013](#d-013-catálogo-de-arquivos-sql-e-sobreposição-externa)). O hash de cada migration é o SHA-256 do SQL gerado para ela, gravado no cabeçalho e conferido por
`verify`.

**Justificativa**:
- Atende FR-009, FR-010 e FR-036 a FR-038 sem novo artefato nem SDK.
- O código de migration é exatamente o da imagem em execução, então não há defasagem entre migrator e aplicação.
- O script sem `--idempotent` é o caminho já comprovado no banco real.
- A segurança na reexecução vem de aplicar só o que falta no histórico, não do modo idempotente do EF.

**Alternativas consideradas**:
- **EF migration bundle publicado como asset**: rejeitado, porque é um binário a mais por edição e por RID e não dá o
  plano e a verificação.
- **Imagem `dokpod-migrator` dedicada**: rejeitada, porque duplica código e versão e é mais uma imagem para assinar e
  qualificar.
- **API migrando sozinha na inicialização**: rejeitada, porque tira do usuário o controle pedido na revisão de
  2026-11-08 e exigiria credencial de *owner* no runtime.
- **`--idempotent`**: inviável, pelos `SELECT` de nível superior.

**Consequência**: é uma decisão duradoura de distribuição, então pede um ADR `proposed` ("modo administrativo da imagem da
API para migrations e bootstrap").

## D-004: Cadastro de ambiente e identidade do agente sem SQL manual

**Decisão**: o mesmo modo administrativo oferece `environments register`, `agent-identities register` e
`agent-identities revoke` ([contrato](contracts/admin-commands.md)).
- Os comandos usam os serviços de aplicação existentes (registro de ambiente, `IAgentIdentityRegistry`, revogação) e
  gravam o evento de auditoria com o ator técnico `installer`, o nome do operador e a justificativa.
- Os recursos de autorização no Keycloak são provisionados pelo instalador com `provision-environment-authorization.ps1`,
  concedendo os scopes ao grupo `/dokpod/administrators`.
- Os comandos são idempotentes: cadastrar de novo o mesmo ambiente ou o mesmo fingerprint para o mesmo ambiente não muda
  nada. O mesmo fingerprint num ambiente diferente é recusado.

**Justificativa**:
- Atende FR-024 e FR-025 com auditoria e sem acoplar o instalador ao schema.
- O `POST /environments` exigiria um token de usuário, ou seja, a senha do administrador ou um *direct grant*, que o
  baseline do realm não habilita.

**Alternativas consideradas**:
- **API HTTP autenticada**: rejeitada pelo motivo acima.
- **`INSERT` direto**: rejeitado, porque não deixa auditoria e duplica regra de domínio.
- **Enrollment com token de uso único**: rejeitado, porque é a solução certa a longo prazo (`specs/README.md`,
  pendência de enrollment), mas é uma spec própria, e o modo administrativo não a impede.

**Consequência**: é uma superfície privilegiada nova. Entra no threat model, e o comando só roda com acesso ao banco
como *owner*, que o runtime não tem.

## D-005: PKI e validação de certificados

**Decisão**: `openssl` da imagem do instalador.

| Item | Valor |
| --- | --- |
| CA local | ECDSA P-256, 10 anos, `basicConstraints=CA:true,pathlen:0`. |
| Certificados de servidor (borda e API) e de cliente (agentes) | ECDSA P-256, 397 dias. |
| EKU | `serverAuth` para borda e API; `clientAuth` para agentes. |
| SAN da API | `api` (nome interno) e o nome público, quando agentes remotos usam o gRPC publicado. |
| SAN da borda | O nome público. |
| Formato | PEM para a borda; PFX com senha gerada para API e agentes, como a API e o agente já esperam. |

A validação (FR-017) confere SAN, EKU, validade, cadeia até a raiz configurada e correspondência entre chave e
certificado, inclusive nos certificados da organização. O aviso de vencimento padrão é de 30 dias (FR-019).

**Justificativa**:
- Os formatos são os que `DOKPOD_API_CERTIFICATE_PATH`, `DOKPOD_API_CLIENT_CA_CERTIFICATE_PATH` e o agente já consomem
  (`docs/distribuicao.md`, "Configuração").
- 397 dias é o teto aceito pelos navegadores para certificado de servidor.

**Alternativas consideradas**:
- **RSA 3072**: aceito como opção do arquivo de respostas para ambientes que exigem RSA, mas não é o padrão.
- **`step-ca` ou outra CA externa**: rejeitada, por ser dependência e serviço a mais.

## D-006: Segredos entregues aos serviços por arquivo

**Decisão**: os segredos ficam no destino de segredos (modo `0700`, arquivos `0600`) e entram nos serviços por `secrets:`
do Compose, montados em `/run/secrets`.
- API e BFF passam a ler a configuração também de `/run/secrets` pelo provedor *key-per-file* do ASP.NET Core
  (`Microsoft.Extensions.Configuration.KeyPerFile`, MIT, do próprio ASP.NET Core), onde o nome do arquivo é a chave
  (por exemplo `ConnectionStrings__ControlPlane`).
- O provedor só é registrado quando o diretório existe, então nada muda para quem usa variáveis de ambiente.
- Senhas de PFX usam o mesmo caminho.

**Justificativa**:
- Atende FR-033: o segredo não aparece em `docker inspect`, em variável de ambiente nem na linha de comando, e não vai
  para o `.env` do Compose.
- Segue `docs/distribuicao.md`: "secrets entram por arquivo montado".

**Alternativas consideradas**:
- **`.env` com os valores**: rejeitado, porque o valor aparece no `inspect`, e as regras do projeto o desaconselham.
- **Variáveis `*_FILE` com leitura própria**: rejeitado, porque é código novo para o que o provedor padrão já faz.

**Verificação de licença**: `Microsoft.Extensions.Configuration.KeyPerFile` é MIT e versionado com o ASP.NET Core 10.
Confirmar a versão exata em `Directory.Packages.props` na implementação.

## D-007: Estado, plano e retomada

**Decisão**:
- **Estado**: banco SQLite `<install-dir>/state/installation.db` ([modelo](data-model.md#estado-da-instalação-sqlite),
  [D-014](#d-014-estado-da-instalação-em-sqlite)), sem segredos. Guarda versão, edição, cenário, origem de cada
  componente, hash da configuração gerada, fingerprints e validade dos certificados, desvios de SQL e as etapas de
  cada execução.
- **Plano**: o "desejado" vem do arquivo de respostas mais a versão; o "observado" é lido do engine (inspect),
  do banco (`admin migrations status`), do Keycloak (`-DryRun` dos scripts) e do sistema de arquivos.
- **Ações**: cada uma é `create`, `update`, `keep`, `recreate` ou `remove`.
- **Concorrência**: a linha única de `run_lock` no banco de estado, adquirida numa transação `BEGIN IMMEDIATE`,
  impede execuções simultâneas (código 7). Um *lock* cujo container `dokpod-installer-<runId>` não existe mais é
  velho e pode ser liberado com confirmação.
- **Retomada**: refaz o plano a partir do observado. Etapas já concluídas aparecem como `keep`.

**Justificativa**: o plano baseado no observado, e não só nas etapas registradas, converge mesmo após mudança manual ou
interrupção (FR-026, FR-027, SC-004).

## D-008: Borda e nome público

**Decisão**:
- **Sem nginx**: o instalador sobe `dokpod-edge` (`lzocateli/nginx:1.28.0-bookworm-v1`) com a configuração gerada.
  Ela publica a porta de `edge.httpsPort` (padrão `443`, sujeita a D-015) e encaminha `/dokpod/` para web e BFF (com SignalR). Quando o Keycloak é provisionado, encaminha
  também `/realms/` e `/resources/` para ele.
- **Com nginx existente**: gera `dokpod.conf` com os mesmos `location` e o valida com `nginx -t` em container. Web e
  BFF escutam só em loopback ou numa rede interna compartilhada com o nginx do usuário; outro endereço é recusado
  (FR-043, correção F3).
- **Nome público dentro da rede**: na rede interna, a borda recebe o nome público como alias. Assim API e BFF alcançam a
  `Authority` pelo mesmo nome do navegador, sem `extra_hosts` nem IP fixo, o que substitui o arranjo de laboratório.
- **Confiança TLS da API e do BFF**: os dois validam a `Authority` do Keycloak por TLS. O instalador monta em
  `SSL_CERT_FILE` um bundle com as raízes do sistema, a CA local (quando a borda ou o Keycloak provisionado a usam)
  e a `keycloak.caCertificate` (quando um Keycloak existente usa CA privada). A correção A3 cobre esse último caso.

- **Porta dos agentes remotos** (correção F2, opção a): com `agentGrpcPort`, a API publica a porta gRPC com mTLS
  diretamente, como na arquitetura atual (`17443` no laboratório). É a única exceção à borda (FR-022). Ela só aceita
  certificado de cliente de ambiente cadastrado. Sem `agentGrpcPort`, a porta não é publicada, e só o agente local
  conecta pela rede interna.

**Justificativa**: remove a dependência de `identity-global`, `host-gateway` e IPs fixos (FR-022). A alternativa de
passar o gRPC pela borda sem terminar o TLS (`stream` do nginx) foi rejeitada: acrescenta configuração e uma
dependência de módulo sem mudar a fronteira de confiança, que é o mTLS.

**Alternativas consideradas**:
- **Keycloak em subdomínio próprio**: aceito como opção (`keycloak.publicUrl`), mas não é o padrão, para exigir um nome
  e um certificado só.

## D-009: PostgreSQL provisionado e existente

**Decisão**:
- **Provisionado**: duas instâncias, `dokpod-postgres` e `keycloak-postgres`, com
  `lzocateli/postgresql:18.4-pgvector0.8.6-bookworm`. Cada uma tem volume, credenciais e backup próprios, como pede
  `docs/distribuicao.md`.
- **Existente**: o instalador cria o banco `dokpod` e duas roles:
  - `dokpod_owner`: dona do schema `dokpod`, usada só pelo modo administrativo;
  - `dokpod_runtime`: DML e `EXECUTE` no schema, usada pela API.

  Nenhuma das duas é superusuário ou tem `CREATEROLE`.
- **Credencial administrativa**: entra por arquivo informado ou por prompt, só durante a execução.
- **Banco e roles já criados pelo DBA**: o instalador valida os privilégios pelos arquivos de verificação do
  catálogo ([D-013](#d-013-catálogo-de-arquivos-sql-e-sobreposição-externa)) e recusa excesso ou falta (FR-008).

**Resultado da validação (T016, 2026-11-08)**:
- **Modelo de privilégios existente**: as migrations já têm um modelo. A `202609120001` cria o grupo `dokpod_runtime`
  (NOLOGIN), e as seguintes concedem a ele privilégios finos e deliberados. Exemplos: auditoria só por função de
  *append*, `licensing_capacity_anchors` só com `SELECT`, e retenção e partições por funções `SECURITY DEFINER`.
  - Por isso o bootstrap não concede DML amplo, o que quebraria essas regras. Ele cria antes o grupo `dokpod_runtime`
    (a role dona não tem `CREATEROLE` para a migration o criar), cria a role de login do runtime como membro do grupo e
    concede só `CONNECT`.
  - Os privilégios em tabelas e funções são os das migrations.
  - O grupo é do cluster inteiro, então várias instalações num mesmo PostgreSQL compartilham o nome.
- **Migrations sem privilégio administrativo**: as migrations rodam inteiras com a role dona, sem `SUPERUSER`,
  `CREATEROLE` nem `CREATEDB`.
- **Lacuna encontrada e corrigida**: o grupo não tinha acesso a `environments` nem a `agent_identities`. Isso não
  aparecia porque laboratório e CI conectam com uma role administrativa.
  - A migration `202611080001_GrantRuntimeEnvironments` concede o mínimo usado pelo runtime: `SELECT`, `INSERT` e
    `UPDATE` em `environments`; `SELECT` e `UPDATE` em `agent_identities`.
  - O registro de identidade fica com o modo administrativo, que usa a role dona.
- **Exercício com a role de runtime**: cadastro de ambiente, auditoria, consulta e revogação de identidade, inventário,
  fila de comandos (tabela particionada) e retenção funcionam.
- **Recusas confirmadas**: DDL, `DELETE` em `audit_events`, `INSERT` em `agent_identities` e chamada direta da criação
  de partições.

## D-010: Imagens, assinatura e host sem internet

**Decisão**:
- O instalador lê o `release-manifest.json` da versão pedida e usa só `referência@digest`.
- Antes de qualquer `pull` ou uso, verifica cada imagem com `cosign verify`, com a identidade do workflow de publicação
  do projeto, a mesma de `docs/release-integrity.md`.
- **Host sem internet**: aceita um diretório com o manifesto, as imagens exportadas (`docker save`) e as assinaturas
  (`cosign save`), verificados da mesma forma.
- Uma imagem com assinatura inválida interrompe a execução antes de qualquer alteração.

**Justificativa**: FR-005 e FR-006, reaproveitando o manifesto que o deploy hook já consome.

## D-011: Compatibilidade com o deploy hook

**Decisão**: o diretório de instalação contém `compose.yaml`, o env não secreto e `deploy.env` no formato do hook, com
`SERVICES` e `ORDER` preenchidos. Atualização sem migration pode seguir pelo hook; com migration, o hook continua sem
aplicá-la, e a atualização passa pelo instalador (FR-023, FR-032).

## D-012: Testes do instalador

**Decisão**:
- **Lógica do plano, validação de respostas e de certificados**: Pester 6.2.0 (Apache-2.0) no estágio de testes da
  imagem do instalador, em CI.
- **Modo administrativo**: testes .NET contra PostgreSQL 17.6 real, no padrão dos testes de integração existentes,
  incluindo a igualdade de schema entre `apply` e o SQL exportado (SC-010).
- **Ponta a ponta**: os cenários do [quickstart](quickstart.md) num host Linux descartável, nos dois engines, com
  varredura de segredos canário (SC-005).

**Verificação de licença**: Pester 6.2.0 é Apache-2.0, mantido pelo projeto Pester (conferido em 2026-11-08, T004).

## D-013: Catálogo de arquivos SQL e sobreposição externa

Diretriz do solicitante (2026-11-08): "certifique que o instalador não tenha comandos SQL fixos, o ideal é usar arquivos
SQL, assim pode ser usado pelo EfCore migrations e pode ser alterado externamente". Esclarecimento: opção B (FR-039 a
FR-041).

**Decisão**:
- **Catálogo único**: todo SQL executado pelo instalador ou pelo modo administrativo fica em arquivos `.sql` em
  `backend/libs/Dokpod.ControlPlane.Infrastructure/`.
  - As migrations continuam em `Migrations/Script/<id>/Up|Down/*.sql`, já carregadas pelas migrations do EF Core via
    `MigrationSqlScriptLoader`.
  - Os demais arquivos vão para `Sql/Installation/`:
    - `bootstrap/` (banco, roles, concessões e `ALTER DEFAULT PRIVILEGES` para as tabelas futuras);
    - `checks/` (versão do servidor e privilégios das roles);
    - `status/` (contagens de conferência).
- **Manifesto**: `Sql/manifest.json` lista, para cada arquivo:
  - `path` e `sha256`;
  - `purpose` (`migration`, `bootstrap`, `check` ou `status`);
  - `executor`: `ef` (migration), `psql` (precisa de credencial administrativa, que o modo administrativo não tem) ou
    `admin` (modo administrativo, conexão do *owner*);
  - os parâmetros declarados.

  O manifesto é gerado e conferido no build. Um teste falha se um arquivo não estiver listado ou se o hash divergir.
- **Parâmetros sem concatenação**:
  - executor `psql`: variáveis do `psql` (`:"role"` para identificadores e `:'value'` para literais), passadas por `-v`
    e nunca por interpolação de texto;
  - executor `admin`: parâmetros nomeados do Npgsql (`@name`).

  Segredos, como as senhas das roles, nunca vão por `-v` nem por variável de ambiente. O próprio `.sql` os lê do
  arquivo montado em `/run/secrets` com `\set nome `cat /run/secrets/<arquivo>``, e o manifesto declara esse
  arquivo no parâmetro `secret`. Só identificadores e literais não secretos vão por `-v` (correção A1 do
  `/speckit-analyze`).
- **Distribuição**: os arquivos ficam embutidos na assembly, como hoje, e são copiados com o manifesto para `/app/sql/` na
  imagem da API e na do instalador. Também saem como asset da release `dokpod-sql-<versão>.tar.gz`, atestado como os
  demais. `install.sh sql export --out <dir>` extrai a árvore para revisão e alteração.
- **Sobreposição** (`--sql-dir`):
  - o diretório sobreposto precisa ter o manifesto da mesma versão;
  - ele substitui arquivos de mesmo caminho e recusa um caminho sem correspondente na release;
  - o plano mostra cada arquivo alterado com o hash original e o novo, e exige a confirmação que o nomeie
    (`--confirm-sql-overrides <lista>` no modo não interativo).
- **Carga no modo administrativo**: o `MigrationSqlScriptLoader` ganha uma raiz externa opcional
  (`/run/sql-override`, montada só no container do modo administrativo). O runtime da API não monta nem lê essa raiz,
  então a aplicação nunca executa SQL sobreposto fora de uma migration confirmada.
- **Registro**: cada desvio aplicado vai para a tabela `sql_overrides` do banco de estado, para o relatório e para o
  cabeçalho do SQL exportado (`-- override:`).
- **Verificação pelo schema** (correção F1 do `/speckit-analyze`): `__EFMigrationsHistory` guarda só o ID e a versão do
  EF, então o hash do arquivo aplicado não pode ser conferido no banco. A verificação tem duas partes:
  - **IDs**: as migrations esperadas estão no histórico.
  - **Schema**: a impressão do schema real, lida por `Installation/checks/schema-fingerprint.sql` (executor
    `admin`), é comparada com `schema/expected-schema.json` da versão.

  A impressão cobre tabelas, colunas, índices, restrições, funções (assinatura e SHA-256 do corpo) e gatilhos do
  schema `dokpod`, e exclui as partições criadas em execução (`relispartition`) e as concessões.
  - **Geração**: `expected-schema.json` é gerado por um teste de integração que aplica todas as migrations num
    PostgreSQL real. Ele é versionado, e o teste falha se estiver desatualizado.
  - **Sobreposição**: depois de uma aplicação confirmada com `--sql-dir`, o instalador grava a impressão observada em
    `sql_overrides.accepted_schema_fingerprint`. A conferência aceita a impressão esperada ou a última aceita;
    qualquer outra dá código 6, com a lista de objetos divergentes.
  - **Hash do arquivo**: quando o usuário entrega o arquivo que aplicou, o `verify --script` recalcula os hashes
    dele e os compara com o cabeçalho e o catálogo, apontando qual arquivo mudou. Essa conferência é complementar,
    não é a garantia.
- **Varredura de SQL no código**: um teste de arquitetura em `Dokpod.Architecture.Tests` e um Pester no instalador
  recusam SQL no código-fonte (palavras-chave DDL/DML em literais de C# e PowerShell) fora do catálogo. As exceções são
  listadas explicitamente; a única aceita é a chamada `migrationBuilder.AddColumn/DropColumn` da migration
  `202609210004`, que o EF gera.

**Justificativa**:
- Uma fonte só para o EF Core e para o uso externo evita divergência entre o que a aplicação migra e o que o DBA revisa.
- O manifesto com hash torna toda alteração visível.
- O runtime sem acesso à raiz externa mantém a garantia de schema.

**Alternativas consideradas**:
- **Templates em `deploy/installer/templates/postgres/`**: rejeitada, porque separava o SQL de bootstrap das migrations
  e dava duas fontes.
- **Sobreposição lida também pelo runtime**: rejeitada, porque permitiria schema não confirmado.
- **Opções A e C do esclarecimento**: A não permite a alteração externa pedida, e C a permite sem rastro.

## D-014: Estado da instalação em SQLite

Diretriz do solicitante (2026-11-08): "Se for necessário manter estado da instalação use sqlite" (FR-044).

**Decisão**:
- **Arquivo**: o estado fica em `<install-dir>/state/installation.db`, com modo `0640`, SQLite 3, `journal_mode=WAL` e
  `foreign_keys=ON`. Ele não tem segredos, porque segredos só existem em `secretsDir`.
- **Acesso**: o instalador acessa o banco por `Microsoft.Data.Sqlite`, carregado no PowerShell 7 da imagem, com comandos
  parametrizados (`$name`). Não usa o CLI `sqlite3` nem concatena valores.
- **Arquivos SQL**: schema e consultas ficam em `deploy/installer/sql/state/`, com `manifest.json` no formato do catálogo
  de D-013. A diretriz de FR-039 vale aqui também.
  - `schema/NNNN-<nome>.sql`: forward-only, aplicados em ordem; `PRAGMA user_version` registra o último.
  - `queries/<nome>.sql`: uma consulta por arquivo.

  Os testes `NoInlineSql.Tests.ps1` cobrem esses arquivos.
- **Escrita**: cada etapa é registrada em `run_steps` numa transação, só depois de concluída, o que garante a retomada
  (FR-027).
- **Versão do estado**: um `user_version` maior que o conhecido pelo instalador dá código 2, com a versão necessária.
- **Integridade**: cada execução começa com `PRAGMA integrity_check`.
  - Banco corrompido ou ausente numa instalação que já existe (containers com o rótulo da instalação, realm ou banco do
    Dokpod presentes) não é recriado em silêncio.
  - Nesse caso, o instalador reconstrói o estado a partir do observado, mostra o resultado e grava só com confirmação.
- **Backup**: o instalador copia o banco por `VACUUM INTO` para `state/backups/<runId>.db` antes de cada execução que
  altera algo, mantendo as 10 últimas.
- **Retenção**: ficam as 200 últimas execuções (`runs`, `run_steps` e `reports/*.json`); as que têm
  `sql_overrides` são sempre preservadas. Com uma linha por execução, o volume não chega ao da regra de
  particionamento da constituição (correção A7).
- **Relatório**: o relatório de execução continua um JSON legível em `reports/<runId>.json` (FR-034), gerado a partir do
  banco.

**Justificativa**:
- Atende a diretriz com transações e integridade, que um JSON reescrito a cada etapa não tem.
- Consultas para `status` e a retomada ficam simples.
- O SQLite é um arquivo local da ferramenta, não um serviço nem um datastore do produto, então não muda a arquitetura do
  Dokpod. Mesmo assim, é registrado no ADR do instalador (T003), conforme a regra "novo datastore exige ADR".

**Verificação de licença**:
- SQLite: domínio público.
- `Microsoft.Data.Sqlite`: MIT, Microsoft.
- `SQLitePCLRaw.bundle_e_sqlite3`: Apache-2.0.

Fixar as versões exatas em T004.

**Alternativas consideradas**:
- **JSON**: rejeitado, porque não tem transação nem integridade e foi substituído por diretriz do solicitante.
- **CLI `sqlite3` com `.parameter set`**: rejeitado, porque a passagem de parâmetros por linha de comando é frágil e pode
  vazar valores no `ps`.
- **Estado no PostgreSQL do Dokpod**: rejeitado, porque o estado precisa existir antes do banco e nos cenários com banco
  do usuário.

## D-015: Somente engines rootless

Decisão do solicitante na revisão do threat model (T001): "a aplicação funciona rootless, o pré-requisito é docker ou
podman ambos rootless".

**Decisão**:
- **Engines suportados**: Docker Engine ≥ 27 em modo rootless e Podman ≥ 5 rootless, no plano de controle e nos hosts
  adicionais. O modo rootful é recusado com código 3 e com orientação para usar o modo rootless.
- **Detecção**:
  - Docker: `SecurityOptions` de `GET /info` contém `name=rootless`.
  - Podman: `host.security.rootless` de `GET /libpod/info` é `true`.

  O instalador não confia no caminho do socket.
- **Sockets**: `$XDG_RUNTIME_DIR/docker.sock` (Docker rootless) e `$XDG_RUNTIME_DIR/podman/podman.sock` (Podman). O
  `install.sh` recusa socket fora de `$XDG_RUNTIME_DIR`.
- **Portas abaixo de 1024**: o modo rootless só publica porta privilegiada se o administrador do host baixar
  `net.ipv4.ip_unprivileged_port_start`.
  - O instalador lê esse valor e, se `edge.httpsPort` ficar abaixo dele, para com três saídas: outra porta (por
    exemplo `8443`), um nginx existente do host na `443` (cenário 2) ou o administrador baixar o limite.
  - Ele nunca altera o `sysctl` sozinho.
- **IP de origem**: o encaminhamento de portas rootless pode trocar o IP do cliente pelo do encaminhador. Isso
  prejudica a auditoria, que registra o IP do pedido.
  - O instalador verifica o driver: no Podman, `pasta`, padrão desde o 5, preserva o IP; no Docker rootless, o driver
    `builtin` não preserva, e `slirp4netns` preserva.
  - Se o IP não for preservado, ele avisa e indica a configuração ou o nginx existente do host. Não bloqueia a
    instalação.
- **Volumes e permissões**: os dados dos containers ficam no armazenamento do usuário. Os UIDs dentro dos containers
  são mapeados pelo `subuid` do usuário, e o instalador grava os arquivos de `secretsDir` montados com o dono esperado
  pelo container (`podman unshare` ou `chown` pelo próprio container), sem `sudo`.
- **Serviço depois do reboot**: os containers só sobem após reinício se o usuário tiver *linger*
  (`loginctl enable-linger`). O instalador verifica e, sem *linger*, avisa com o comando, que exige o administrador do
  host. Ele não executa o comando sozinho.

**Justificativa**:
- Menor privilégio. Com o socket rootless, o instalador e o agente ficam limitados ao usuário, o que reduz o risco R1
  do threat model.
- O laboratório do projeto já roda com Podman rootless (`deploy/e2e/README.md`).

**Alternativas consideradas**:
- **Suportar também rootful**: rejeitada pela decisão do solicitante.
- **Baixar `ip_unprivileged_port_start` automaticamente**: rejeitada, porque exige root e muda o host além do Dokpod.
