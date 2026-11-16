# Contrato: modo administrativo da imagem da API

Decisões: [D-003](../research.md#d-003-migrations-sem-sdk-e-com-controle-do-usuário) e
[D-004](../research.md#d-004-cadastro-de-ambiente-e-identidade-do-agente-sem-sql-manual).

```text
docker run --rm --network <rede-interna> \
  --mount type=bind,src=<secretsDir>/dokpod-owner-connection,dst=/run/secrets/ConnectionStrings__ControlPlane,ro \
  ghcr.io/altivy-it/dokpod-api@sha256:<digest> admin <grupo> <comando> [opções] --output json
```

- Vale para `dokpod-api` e `dokpod-api-business`. A edição só muda `admin licensing import`, que só existe na Business.
- Com catálogo sobreposto, o instalador monta o diretório conferido em `/run/sql-override` (somente leitura). O
  `MigrationSqlScriptLoader` o usa só no modo administrativo, e o runtime da API não o monta (D-013).
- Todo SQL dos comandos vem do catálogo (`/app/sql/manifest.json`); não há SQL no código (FR-039).
- O modo administrativo não abre portas, não inicia hosted services e termina ao fim do comando.
- A conexão vem só do arquivo montado; não há opção de conexão na linha de comando.
- Saída JSON em stdout e mensagens em stderr, sem segredos.
- Códigos de saída: 0 sucesso, 2 uso inválido, 6 divergência de migrations, 8 conflito de identidade, 1 erro.

## `admin migrations`

| Comando | Saída JSON | Efeito |
| --- | --- | --- |
| `status` | `{databaseVersion, targetVersion, applied:[id], pending:[id], newerThanTarget:[id]}` | Nenhum. |
| `plan` | `{from, to, migrations:[{id, sha256, overridden, overrides:[{path, originalSha256, appliedSha256}], objects:{created:[], altered:[], dropped:[]}}], planHash, newerThanTarget}` | Nenhum. O resumo de objetos vem de análise do SQL gerado (`CREATE`/`ALTER`/`DROP` de nível superior). |
| `script --out <arquivo>` | `{path, sha256, planHash}` | Nenhum no banco. Grava o arquivo do [formato](migration-script.md). |
| `fingerprint` | `{sha256, objects:[{kind, name, definitionSha256}]}` | Nenhum. Impressão do schema real, usada pelo instalador para registrar `accepted_schema_fingerprint` depois de uma aplicação com sobreposição. |
| `apply --confirm <planHash>` | `{applied:[id], durationMs}` | Executa os blocos do plano confirmado, depois do SQL de sessão do catálogo, uma transação por migration. Recusa se o `planHash` não for o do plano atual, ou se `newerThanTarget` não estiver vazio (código 6). |
| `verify [--script <arquivo>] [--accepted-fingerprints <arquivo>]` | `{ok, missing:[id], unexpected:[id], schemaSha256, expectedSchemaSha256, schemaAccepted, schemaDiff:[{kind, name, expected, actual}], fileHashMismatch:[id]}` | Nenhum. (1) Confere os IDs do histórico. (2) Compara a impressão do schema real (`checks/schema-fingerprint.sql`) com `schema/expected-schema.json` ou com uma impressão aceita de `--accepted-fingerprints` (a coluna `accepted_schema_fingerprint` do estado). Diferença não aceita dá código 6. (3) Com `--script`, recalcula os hashes do arquivo entregue e aponta os arquivos alterados (complementar). |

O `planHash` é o SHA-256 da lista ordenada de `id:sha256`. Ele liga a confirmação do usuário ao plano que ele viu
(FR-036).

## `admin environments register`

```text
admin environments register --id <uuid> --name <nome> --host <host> --stage <production|staging|development|none> \
  --actor <operador> --reason <texto>
```

- Cria o registro do ambiente pelo serviço de aplicação existente e grava o evento de auditoria existente `environment.register`
  com ator técnico `installer` e `actorDisplay = <operador>`.
- **Idempotência**: o mesmo `id` com os mesmos dados não muda nada (`{created:false}`); dados diferentes dão código 8.
- Não toca no Keycloak. Os recursos são provisionados pelo instalador com `provision-environment-authorization.ps1`.

## `admin agent-identities register | revoke`

```text
admin agent-identities register --environment <uuid> --fingerprint <sha256-hex> --actor <operador> --reason <texto>
admin agent-identities revoke   --environment <uuid> --actor <operador> --reason <texto>
```

- `register` grava a identidade em `dokpod.agent_identities` pelo `IAgentIdentityRegistry` e audita a ação nova
  `environment.agent_identity_register`. Ela entra na categoria `environments` de `AuditCategories`, ao lado de
  `environment.register`.
- **Idempotência de `register`**: o mesmo par não muda nada. Um fingerprint já ligado a outro ambiente, ou já revogado,
  dá código 8.
- `revoke` usa `AgentIdentityRevocationService`, o mesmo caminho do endpoint `.../agent-identity/revoke`, e audita com a mesma ação do endpoint.

## `admin licensing import` (só Business)

```text
admin licensing import --file /run/import/license.jws --slot <prd|hml> --actor <operador> --reason <texto>
```

- Usa o mesmo serviço de importação de `PUT /api/v1/licensing/license?slotId=` (spec 031), com as mesmas validações e a
  mesma auditoria, e ator técnico `installer`.
- **Idempotência**: a mesma licença já ativa no slot não muda nada.
- **Recusa**: uma licença inválida, de outra organização ou revogada é recusada com o código de erro da spec 031
  (saída 2).
- Na CE, o comando não existe (saída 2, com a mensagem "edição sem licenciamento").

## Segurança

- O comando exige a conexão de `dokpod_owner`. A API em execução usa a role de runtime (membro do grupo `dokpod_runtime`) e não tem esse arquivo montado.
- Nenhum comando aceita SQL livre nem caminho fora de `/run/secrets` e do diretório de saída montado.
- A auditoria desses comandos tem a mesma retenção e o mesmo encadeamento dos demais eventos.
