# Contrato: relatório de apuração e cadeia de licenciamento

**Versão**: `reportVersion: 1` | **Decisão**: [D-013](../research.md#d-013-relatório-de-apuração-com-evidência-de-adulteração) | **Requisitos**: FR-033, FR-034, FR-035, SC-010

O relatório é o insumo obrigatório da auditoria em toda contratação e renovação (FR-034). O cliente o exporta localmente e o entrega ao fornecedor pelo mesmo canal fora de banda usado para receber a licença; o produto nunca o envia (FR-002).

## Cadeia de licenciamento

Toda linha de `licensing_events` recebe, na mesma transação em que é gravada:

| Campo | Regra |
| --- | --- |
| `chain_seq` | inteiro ≥ 1, sem lacunas, atribuído a partir de `licensing_state.chain_head_seq` |
| `prev_hash` | `record_hash` do evento anterior; no primeiro, `SHA-256(UTF-8("dokpod-licensing-chain-v1") ‖ UTF-8(installationId))`, com o UUID em minúsculas no formato `8-4-4-4-12` |
| `actor_ref` | `BASE64URL(HMAC-SHA256(licensing_state.actor_hmac_key, UTF-8(sujeito do Keycloak)))`, ou `null` para o sistema; o sujeito real (`actor_id`) fica fora da cadeia e nunca é exportado |
| `record_hash` | `SHA-256(prev_hash ‖ JSON canônico de {chain_seq, event_id, occurred_at_utc, actor_ref, correlation_id, action, details})` |

JSON canônico: UTF-8, propriedades ordenadas por ordinal UTF-16, sem espaços; strings escapam só aspas, barra invertida e caracteres de controle (`\b`, `\f`, `\n`, `\r`, `\t`, demais como `\u00xx`); números só inteiros; UUIDs em minúsculas; `occurred_at_utc` como `yyyy-MM-ddTHH:mm:ss.ffffffZ` (microssegundos, a precisão do PostgreSQL). `actor_ref` é `BASE64URL` sem padding. A gravação trava a linha de `licensing_state` (`FOR UPDATE`) e só avança `chain_head_seq` e `chain_head_hash` se a cabeça ainda for a lida, então dois eventos nunca recebem o mesmo `chain_seq`. O `version` do estado não muda com eventos: ele é o `If-Match` das operações administrativas. Vetor de referência em `backend/tests/Dokpod.ControlPlane.Api.Tests/Licensing/LicensingChainTests.cs`.

### Resumo diário de capacidade

O job de avaliação grava, uma vez por dia UTC já encerrado, o evento `capacity.daily_summary` com `details`:

```json
{ "date": "2026-11-14", "vcpuBand": 32, "peakNodeEquivalents": 7, "unknownHosts": 0,
  "hosts": [ { "hostId": "8b1e…", "maxVcpus": 64, "source": "engine" } ], "late": false }
```

Dias perdidos (API parada) são gravados na execução seguinte com `late: true`. O resumo entra na cadeia, que nunca é purgada; as observações brutas continuam com retenção de 35 dias (D-005).

## Documento do relatório

`GET /api/v1/licensing/report?challenge=<texto>` devolve JSON com:

```json
{
  "reportVersion": 1,
  "installationId": "3f0c…",
  "slotId": "prd",
  "generatedAt": "2027-10-02T13:00:00Z",
  "challenge": "rnw-2027-org_51d2e0-a91f",
  "license": { "licenseId": "lic_7f3c9a", "revision": 2 },
  "summary": { "status": { }, "hosts": [ ] },
  "chain": { "headSeq": 1342, "headHash": "base64url…" },
  "events": [ { "chainSeq": 1, "eventId": "…", "occurredAt": "…", "actorRef": null, "correlationId": "…", "action": "…", "details": { }, "prevHash": "…", "recordHash": "…" } ]
}
```

- `installationId`: UUID aleatório gerado na primeira inicialização da composição Business e gravado em `licensing_state`; nunca derivado de hardware, hostname ou rede.
- `challenge`: texto de 8 a 64 caracteres `[A-Za-z0-9_-]` informado pelo fornecedor no pedido de renovação ou contratação. A exportação grava `license.report_exported` com o `challenge` antes de montar o documento; esse evento é o último da cadeia exportada.
- `events`: a cadeia completa desde o primeiro evento. Sem dados pessoais, nomes de host ou endereços: só IDs opacos, contagens e códigos (FR-007, FR-026). O autor de cada evento aparece só como `actorRef`: o fornecedor vê que o mesmo ator repetiu uma ação, mas não consegue identificá-lo, porque `actor_hmac_key` nunca sai da instalação (decisão de 2026-10-31, LGPD).
- Funciona também sem licença (`authenticity = absent`), para a auditoria inicial da contratação.

## Verificação pelo fornecedor

`Dokpod.Licensing` expõe `LicensingReportVerifier`, usado pela CLI (`report verify`) e pelos testes do produto. Resultado tipado:

| Código | Quando |
| --- | --- |
| `ok` | todas as verificações abaixo passam |
| `tampered` | `record_hash` ou `prev_hash` não confere, `chain_seq` com lacuna ou repetição, cabeça declarada diferente da cadeia, JSON fora da forma canônica |
| `discontinuous` | o evento no `headSeq` do último relatório aceito não tem o mesmo `recordHash` (história reescrita ou banco restaurado de backup anterior), ou `installationId` diferente do registrado |
| `mismatch` | `license.imported` ou `revocation.imported` sem correspondência no registro de emissões, ou emissão entregue sem importação correspondente |
| `stale` | `challenge` ausente ou diferente do pedido em andamento |
| `gaps` | dias sem `capacity.daily_summary`, ou com `late: true`; não bloqueia sozinho, mas é listado |
| `clockRegressions` | eventos com instante anterior ao do evento anterior; não bloqueia, mas é listado. Acontece com relógio atrasado (o mesmo que deixa o estado `indeterminate`) e por concorrência; a ordem é garantida pelos hashes |

`tampered`, `discontinuous`, `mismatch` e `stale` bloqueiam a emissão (FR-034). A liberação exige aprovação dupla registrada com justificativa (FR-029).

## Limites

A instalação não guarda segredo que seu operador não possa ler, então a cadeia não impede que ele recalcule todos os hashes depois do último relatório aceito. O que a cadeia garante: a história anterior a esse relatório fica congelada, a reescrita posterior precisa bater com tudo o que o fornecedor emitiu, e a remoção ou alteração isolada de um evento é detectada. O risco restante fica no contrato (residual R9 do [threat model](../threat-model.md)).
