# Validação: Janelas de mudança por estágio

**Data**: 2026-11-22 · **Origem**: IA assistida · **Ambiente**: host de desenvolvimento Linux, Podman rootless, PostgreSQL 17.6 em container, E2E simulada pela borda nginx.

Os cinco cenários do [quickstart](quickstart.md) pedem a stack E2E com a imagem Business, três usuários no Keycloak e dois agentes reais (Produção e Homologação). Essa stack não está de pé neste host (sem a plataforma de identidade nem agentes de laboratório), então a parte com agentes reais fica **NOT RUN** abaixo, e cada cenário aponta a evidência automatizada que cobre a mesma regra.

## Cenários do quickstart

| Cenário | O que foi validado | Evidência | Com agentes reais |
| --- | --- | --- | --- |
| 1. Definir a janela | Criar, editar e remover pela versão; auditoria em cada host do estágio; só leitura sem permissão | `ChangeWindowEndpointsTests` (PostgreSQL real), E2E `test_define_edit_and_remove_a_window` e `test_without_manage_on_the_stage_the_window_is_read_only` | NOT RUN |
| 2. Recusa fora da janela | `409 change_window_closed` com a próxima abertura nas seis ações, sem linha nova em `agent_commands` e com `change_window.denied`; terminal recusado; leituras fora do guard; lote misto | `ChangeWindowEnforcementTests`, `TerminalSessionServiceTests`, `ChangeWindowGuardTests` (arquitetura), E2E da História 2 | NOT RUN: "nenhum comando chega ao agente" só foi provado pela tabela `agent_commands`, não pelo log do agente |
| 3. Exceção de emergência | Sem o scope, `403 change_override_denied`; com o scope, a ação e o `change_window.override` juntos, com a justificativa mascarada; a próxima ação pede nova exceção; lote com hosts com e sem o scope | `ChangeWindowOverrideTests`, `ChangeWindowGuardTests` (aplicação), E2E da História 3 (containers e remoção de volume) | NOT RUN: o container parar de fato depende do agente |
| 4. Licença restrita e sem capability | Editar dá `403 license_*`; remover dá `204`; a recusa e a exceção continuam | `ChangeWindowEndpointsTests` (licença vencida e sem `change-windows`), regra de arquitetura (o guard não depende do gate) | NOT RUN |
| 5. CE | Sem rotas `/api/v1/change-windows`; calendário vazio; `changeWindow.state` `none` | `CeCompositionTests` | NOT RUN |

## Desempenho (T035)

`ChangeWindowGuardImpactTests` com `DOKPOD_TEST_026_IMPACT=1`: mediana das diferenças de p95 de 0,16 ms (meta: até 5 ms). A primeira medição somava 10,7 ms, por causa da busca do `commandId` sobre as 133 partições de `agent_commands`; a busca passou a acontecer só com a janela fechada ([D-001](research.md#d-001-ponto-único-de-aplicação)).

## Para fechar a T038

Rodar os cinco cenários com agentes reais pede uma decisão do solicitante sobre onde:

1. na stack E2E local (`deploy/e2e/README.md`), com a imagem Business e uma licença de teste; ou
2. no ambiente de quality, com a candidata `v0.12.0-rc.N` na imagem Business.
