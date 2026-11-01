# Contrato: API do aviso de nova versão

Acréscimos a `contracts/openapi/dokpod-control-plane.v1.yaml` ([D-011](../research.md#d-011-api-e-telas)). Erros em `application/problem+json`, com `code`.

## Campo `updateCheck`

O campo é opcional nos itens de `GET /api/v1/images`, nos itens de `GET /api/v1/containers` e em `GET /api/v1/environments/{environmentId}/containers/{containerId}/summary`. Ele só existe na composição Business com a capability `image-update-notice` na licença; na CE, a propriedade não aparece.

```json
{
  "updateCheck": {
    "state": "update_available",
    "reason": null,
    "checkedAt": "2026-11-01T12:00:00Z",
    "lastSuccessAt": "2026-11-01T12:00:00Z",
    "outdatedSince": "2026-10-29T06:00:00Z",
    "stale": false
  }
}
```

- `state`:
  - nas imagens: `up_to_date`, `update_available`, `not_checkable` ou `failed`;
  - nos containers, também `container_behind` ([data-model.md](../data-model.md#estado-derivado-por-container)).
- `reason`: o motivo do [modelo de dados](../data-model.md#image_update_checks) ou `null`.
- `stale`: `true` quando o host está desconectado.
- Sem verificação ainda, `updateCheck` é `null`.
- Nunca traz digest remoto, registro resolvido nem conteúdo de resposta do registro.

## `POST /api/v1/environments/{environmentId}/images/update-check`

`x-dokpod-edition: business`. Pede a verificação imediata do host ("Verificar agora").

| Situação | Resposta |
| --- | --- |
| Pedido aceito | `202` com `{ "requestedAt": "…", "nextDueAt": "…" }` |
| Sem `image:pull` no host ou Keycloak indisponível | `403 authorization_denied` |
| Licença restrita | `403 license_expired` |
| Licença sem a capability | `403 license_invalid` |
| Outro pedido manual há menos de 1 minuto | `429 rate_limited`, com `Retry-After` |
| Agente sem `CAPABILITY_IMAGE_UPDATE_CHECK` | `409 agent_unsupported` |
| Host desconectado | `409 environment_disconnected` |
| Host inexistente ou não autorizado | `404` |

Auditoria: `image.update_check_requested` (categoria `images`) no ambiente, com o resultado.

Sem `Idempotency-Key`: o pedido não muda o engine nem o inventário, só adianta a agenda do host. Repeti-lo tem o mesmo efeito, e ele fica limitado a 1 por minuto por host (constituição, princípio IV, que pede a chave para comandos mutáveis).

## Regras de alerta (spec 016)

`POST` e `PUT` das regras aceitam `conditionKind: "image_outdated"` com `outdatedDays` (1 a 365). `AlertRulePage.imageOutdatedAvailable` diz à tela se a condição pode ser oferecida (verdadeiro só na composição Business com a capability).

| Situação | Resposta |
| --- | --- |
| Composição sem o módulo (CE) | `422 alert_condition_unavailable` |
| Licença restrita ao criar ou editar | `403 license_expired` |
| Licença sem a capability ao criar ou editar | `403 license_invalid` |
| Desativar ou excluir | Permitido sempre |
