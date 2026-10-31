# Runbook: importação, estado e auditoria da licença Business

Spec 031 (T063). Vale só para a imagem Business; a CE não tem licença nem estes endpoints. Tudo funciona sem rede.
Rotação de chave e revogação: [runbook próprio](licenciamento-rotacao-e-revogacao.md).

## Importar a licença

Configurações → Instalação → Licença → *Importar licença*, ou `PUT /api/v1/licensing/license?slotId=<prd|hml>`
(`licensing:manage`, `application/jose`, até 64 KiB, `If-Match` com o ETag do estado atual).

| Situação | Resposta |
| --- | --- |
| Licença válida para o slot, revisão maior que a atual | `200`, evento `license.imported` |
| Revisão igual ou menor | `422 license_obsolete` |
| Assinatura, `kid`, perfil ou organização inválidos | `422 license_invalid`, evento `license.rejected` só com o SHA-256 do arquivo |
| Slot desconhecido ou desabilitado na licença | `422 license_slot_unknown` ou `license_slot_disabled` |
| ETag diferente | `412 license_revision_conflict`; recarregue o estado e repita |
| Arquivo acima de 64 KiB | `413 payload_too_large` |
| Muitas tentativas | `429 rate_limited` (limite por réplica) |

O arquivo recusado nunca é ecoado nem gravado; a licença vigente continua valendo.

## Estados e prazos

`GET /api/v1/licensing/status` (`licensing:read`).

| Estado | Significado | O que fazer |
| --- | --- | --- |
| `granted` | licença válida, dentro da cota | nada |
| `granted_in_grace` | expirada ou acima da cota, ainda na tolerância | renovar ou reduzir hosts antes do prazo exibido |
| `restricted` | tolerância esgotada ou revogada | renovar; só novas execuções Business ficam bloqueadas |
| `indeterminate` | relógio anterior ao último instante confiável, além de 5 minutos | corrigir o relógio (abaixo) |

Os prazos são gravados na primeira detecção e não são reiniciados por novas importações da mesma revisão. Avisos são
emitidos em 60, 30 e 7 dias. Containers, dados, leitura, backup, exportação e a CE nunca são afetados.

## Recuperar o instante confiável

Se o relógio do host foi corrigido para trás de propósito (por exemplo, NTP mal configurado), o estado fica
`indeterminate`. Depois de corrigir o relógio:

1. confirme a data e hora certas no host;
2. `POST /api/v1/licensing/trusted-instant` (`licensing:manage`, `If-Match`) com a justificativa (10 a 500
   caracteres); fora de `indeterminate` a resposta é `409 license_not_indeterminate` e nenhuma tolerância nova é concedida;
3. o evento `license.trusted_instant_reset` registra o instante anterior, o novo e o motivo na cadeia, e aparece no
   próximo relatório.

Não escreva no motivo nomes de pessoas, hosts ou dados de clientes: ele vai para o fornecedor.

## Exportar o relatório de apuração

Obrigatório na contratação e em cada renovação (FR-034); funciona sem licença importada.

1. Peça ao fornecedor o desafio do pedido (8 a 64 caracteres `[A-Za-z0-9_-]`).
2. Configurações → Instalação → Licença → *Exportar relatório*, ou
   `GET /api/v1/licensing/report?challenge=<desafio>` (`licensing:read`).
3. Envie o arquivo JSON ao fornecedor pelo canal combinado. O Dokpod não envia nada.

O relatório traz a cadeia completa de eventos, o estado e os hosts só pelo ID opaco, com vCPUs e nodes equivalentes.
Não contém nome de host, sujeito do Keycloak (só `actorRef`, um HMAC por instalação) nem a chave do HMAC. Cada
exportação grava `license.report_exported` como último evento.

O fornecedor recusa a emissão quando o resultado não é `ok`:

| Resultado | Causa provável | Ação do cliente |
| --- | --- | --- |
| `tampered` | arquivo editado ou corrompido | exportar de novo, sem editar |
| `discontinuous` | banco restaurado, reinstalado ou outra instalação | falar com o fornecedor; a nova auditoria parte de uma linha de base acordada |
| `mismatch` | licença ou revogação importada sem emissão correspondente, ou emitida e não importada | importar o que falta ou falar com o fornecedor |
| `stale` | desafio antigo ou ausente | exportar com o desafio do pedido atual |

Dias sem resumo diário (`gaps`) e recuos de relógio (`clockRegressions`) são listados, mas não bloqueiam sozinhos.

## Depois de restaurar um backup

1. Reimporte a licença mais recente e o último pacote de revogação recebido.
2. Avise o fornecedor: o próximo relatório será `discontinuous` em relação ao último aceito.
