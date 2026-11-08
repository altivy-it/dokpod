# Contrato: modo administrativo da imagem da API

Decisões: [D-003](../research.md#d-003-migrations-sem-sdk-e-com-controle-do-usuário) e
[D-004](../research.md#d-004-cadastro-de-ambiente-e-identidade-do-agente-sem-sql-manual).

```text
docker run --rm --network <rede-interna> \
  --mount type=bind,src=<secretsDir>/dokpod-owner-connection,dst=/run/secrets/ConnectionStrings__ControlPlane,ro \
  ghcr.io/altivy-it/dokpod-api@sha256:<digest> admin <grupo> <comando> [opções] --output json
```

- Vale para `dokpod-api` e `dokpod-api-business`. A edição só muda os comandos de licença, que estão fora deste
  contrato.
- O modo administrativo não abre portas, não inicia hosted services e termina ao fim do comando.
- A conexão vem só do arquivo montado; não há opção de conexão na linha de comando.
- Saída JSON em stdout e mensagens em stderr, sem segredos.
- Códigos de saída: 0 sucesso, 2 uso inválido, 6 divergência de migrations, 8 conflito de identidade, 1 erro.

## `admin migrations`

| Comando | Saída JSON | Efeito |
| --- | --- | --- |
| `status` | `{databaseVersion, targetVersion, applied:[id], pending:[id], newerThanTarget:[id]}` | Nenhum. |
| `plan` | `{from, to, migrations:[{id, sha256, objects:{created:[], altered:[], dropped:[]}}], planHash}` | Nenhum. O resumo de objetos vem de análise do SQL gerado (`CREATE`/`ALTER`/`DROP` de nível superior). |
| `script --out <arquivo>` | `{path, sha256, planHash}` | Nenhum no banco. Grava o arquivo do [formato](migration-script.md). |
| `apply --confirm <planHash>` | `{applied:[id], durationMs}` | Aplica as pendentes, uma transação por migration. Recusa se o `planHash` não for o do plano atual, ou se `newerThanTarget` não estiver vazio (código 6). |
| `verify --script <arquivo>` | `{ok, missing:[id], unexpected:[id], hashMismatch:[id]}` | Nenhum. Confere o histórico contra o cabeçalho do arquivo exportado. |

O `planHash` é o SHA-256 da lista ordenada de `id:sha256`. Ele liga a confirmação do usuário ao plano que ele viu
(FR-036).

## `admin environments register`

```text
admin environments register --id <uuid> --name <nome> --stage <production|staging|development|none> \
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

## Segurança

- O comando exige a conexão de `dokpod_owner`. A API em execução usa `dokpod_runtime` e não tem esse arquivo montado.
- Nenhum comando aceita SQL livre nem caminho fora de `/run/secrets` e do diretório de saída montado.
- A auditoria desses comandos tem a mesma retenção e o mesmo encadeamento dos demais eventos.
