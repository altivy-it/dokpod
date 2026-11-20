# Contrato HTTP: janelas de mudança

Alterações em `contracts/openapi/dokpod-control-plane.v1.yaml`. As rotas de configuração têm `x-dokpod-edition: business` e só são registradas na composição Business ([D-002](../research.md#d-002-composição-entre-ce-e-business)). Os campos novos nas ações e nas respostas de host existem nas duas edições: na CE, `changeWindow.state` é sempre `none` e o pedido de exceção é ignorado, porque nada fica fechado. Erros em `application/problem+json`. As decisões do Keycloak falham fechadas (`503 authorization_unavailable`).

## Configuração por estágio (Business)

| Método e rota | Autorização | Gate da licença | Sucesso |
| --- | --- | --- | --- |
| `GET /api/v1/change-windows` | `environment:manage` em algum host (como Configurações) | não | `200` lista por estágio com `editable`, `ETag` e estado atual |
| `PUT /api/v1/change-windows/{stage}` | `environment:manage` em todos os hosts legíveis do estágio | sim (`403 license_*`) | `200` com novo `ETag`; `If-None-Match: *` cria, `If-Match` edita |
| `DELETE /api/v1/change-windows/{stage}` | idem | não | `204`; exige `If-Match` |

`{stage}`: `production`, `staging` ou `development`; outro valor dá `404`.

### Corpo do `PUT`

```json
{
  "timeZone": "America/Sao_Paulo",
  "ranges": [
    { "days": ["mon", "tue", "wed", "thu", "fri"], "start": "22:00", "end": "06:00" }
  ]
}
```

Regras em [data-model.md](../data-model.md#janela-de-mudança-dokpodchange_windows). Propriedades desconhecidas são recusadas. Uma janela inválida dá `400 change_window_invalid`, com o campo em `errors`. A versão divergente dá `412 precondition_failed`.

### Item da listagem

```json
{
  "stage": "production",
  "timeZone": "America/Sao_Paulo",
  "ranges": [{ "days": ["mon", "tue", "wed", "thu", "fri"], "start": "22:00", "end": "06:00" }],
  "updatedBy": "Ana",
  "updatedAt": "2026-11-20T18:00:00Z",
  "version": 3,
  "editable": true,
  "status": { "state": "closed", "closesAt": null, "nextOpensAt": "2026-11-21T01:00:00Z" }
}
```

Os estágios sem janela aparecem com `ranges: []`, `version: null` e `status.state: "none"`.

## Ações controladas (CE e Business)

Os corpos das ações que enfileiram comando (comandos de container, criação de container, pull de imagem, remoção de volume e de rede, ações de pod) e o `OpenTerminalRequest` do hub ganham o campo opcional:

```json
{ "changeWindowOverride": true, "justification": "Incidente INC-123: container travado" }
```

| Situação | Resposta |
| --- | --- |
| Janela aberta, estágio sem janela ou host sem estágio | o mesmo de hoje; `changeWindowOverride` é ignorado |
| Reenvio com um `commandId` já aceito, com a janela aberta ou fechada | o resultado de hoje (`Duplicate` ou conflito de payload), sem avaliar a janela |
| Fora da janela, sem `changeWindowOverride` | `409 change_window_closed`, com `nextWindowStartsAt` e `timeZone`, e auditoria `change_window.denied` |
| Fora da janela, com `changeWindowOverride` e sem justificativa de 10 a 500 caracteres | `422 justification_required` |
| Fora da janela, com `changeWindowOverride`, sem `change:override` no host | `403 change_override_denied`, e auditoria `change_window.denied` |
| Fora da janela, com `changeWindowOverride` e o scope | o resultado normal da ação, e auditoria `change_window.override` na mesma transação |

No terminal, as recusas chegam como a mensagem de erro do hub, com o mesmo código. A ordem das verificações é: autorização da ação, ambiente, janela, justificativa da 016 e sessão do agente. Assim, quem não pode executar a ação não descobre o horário da janela.

## Respostas de host (CE e Business)

As respostas de `HostResponses` (lista e detalhe de host, e os hosts das listas de containers) ganham:

```json
"changeWindow": { "state": "closed", "closesAt": null, "nextOpensAt": "2026-11-21T01:00:00Z", "timeZone": "America/Sao_Paulo" }
```

A leitura de grants que o console usa passa a incluir `change:override`. A tela só oferece a exceção a quem tem o scope; a API decide de novo em toda tentativa.

## Compatibilidade

Os campos novos são opcionais e aditivos. Um cliente antigo, que não envia `changeWindowOverride`, recebe o `409` fora da janela, que é o comportamento pedido. O protocolo do agente não muda.
