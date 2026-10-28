# Verificação de Bug: `fail` no log e alertas enganosos (spec 005, A2 e A3)

- **Slug**: spec005-a2-a3-log-alertas
- **Testado em**: 2026-10-28
- **Avaliação**: ./assessment.md
- **Correção**: ./fix.md
- **Resultado**: verified (imagem local e `v0.1.0-rc.3` assinada)

## Resumo

A imagem da API construída da branch de correção substituiu só a API `v0.1.0-rc.2` na VM isolada da spec 005, com 100 agentes simulados, o agente Podman real e o restante da stack na rc.2. Os cenários que antes geravam `fail` e o alerta crítico foram repetidos. Houve duas rodadas: `a2fix`, sem o ajuste do `TransactionError`, e `a2fix2`, final.

## Verificações Executadas

| Verificação | Comando / Ação | Resultado | Notas |
| --- | --- | --- | --- |
| Troca da API com 100 agentes | `vm-swap-api.sh localhost/dokpod-api:a2fix2` | pass | 100 agentes em 9,4 s |
| Restart com 100 comandos em voo (gate 7) | `vm-recovery.sh api` | pass | 100/100, 0 duplicados |
| Resposta perdida (SIGKILL no simulador) | `vm-recovery.sh perdida` (3 rodadas em `a2fix2`, 7 em `a2fix`) | pass | 0 duplicados; 1 `indeterminate` sem execução no engine |
| Linhas `fail`/`crit` no log da API | instância `a2fix2` inteira | pass | **0** (rc.2: 143 no restart e 31 na resposta perdida; `a2fix`: 18 de transação cancelada) |
| `40001` no PostgreSQL | desde a troca da API | pass | **0** (rc.2: dezenas por restart) |
| Falhas de auditoria | `dokpod_audit_append_failures_total` | pass | série nem criada (rc.2: 8 no mesmo cenário); 18 cancelamentos em `dokpod_audit_append_canceled_total` |
| Alerta de indeterminado ao vivo | `/api/v1/alerts` com as regras novas carregadas por HUP | pass | série nasceu com valor 1; `DokpodComandosIndeterminados` disparou às 11:09:57; a expressão da rc.2 avaliada no mesmo instante não dispararia |
| Regra de indeterminados nos dados reais da rc.2 | `query_range` 05:15–05:45 com as duas expressões | pass | antiga: nunca; nova: 05:20 a 05:28 |
| Testes do backend | `dotnet test Dokpod.slnx` com PostgreSQL efêmero | pass | 700 testes |
| Regras | `promtool test rules` / `check rules` | pass | 13 regras |

## Verificação na `v0.1.0-rc.3` assinada

Tag `v0.1.0-rc.3` (`6c84722`), pipeline `40768626305`; assinaturas das quatro imagens verificadas com `cosign verify` v3.1.3 e IDs de imagem iguais na VM.

| Verificação | Comando / Ação | Resultado | Notas |
| --- | --- | --- | --- |
| Gate 10, atualização rc.2 → rc.3 | `vm-gate10.sh v0.1.0-rc.3` | pass | item 7: **0** `fail`/`crit` (rc.1 → rc.2: 17); auditoria idêntica, 101 ambientes |
| Gate 10, rollback rc.3 → rc.2 e volta | `vm-gate10.sh v0.1.0-rc.2`, depois `v0.1.0-rc.3` | pass | itens 1 a 6 aprovados; a rc.2 volta a gerar `fail` (307, 552 `40001`); de novo na rc.3, 0 |
| Restart e resposta perdida | `vm-recovery.sh api` e `perdida` (13 rodadas) | pass | 0 duplicados, 0 `fail`/`crit`, 0 `40001`; 1 `stale_target` sem execução |
| Alertas | regras e séries no Prometheus | pass | `DokpodAuditoriaFalhando` inativo, 0 falhas e 39 cancelamentos; nenhum `indeterminate` espontâneo, então o disparo ao vivo fica com a prova da imagem de pré-teste |

## Riscos Residuais

- Erros reais de transação ficam em `warn` na categoria do EF; o `fail` vem de quem trata a exceção.
- O disparo ao vivo de `DokpodComandosIndeterminados` foi provado na imagem de pré-teste do mesmo código; a `v0.1.0-rc.3` não teve `indeterminate` espontâneo em 13 rodadas.
