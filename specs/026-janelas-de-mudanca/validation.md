# Validação: Janelas de mudança por estágio

**Data**: 2026-11-22 (cobertura automatizada) e 2026-11-24 (quality) · **Origem**: IA assistida · **Ambiente**: host de desenvolvimento Linux, Podman rootless, PostgreSQL 17.6 em container, E2E simulada pela borda nginx.

Os cinco cenários do [quickstart](quickstart.md) pedem a imagem Business, três usuários no Keycloak e dois agentes reais (Produção e Homologação). A cobertura automatizada foi registrada em 2026-11-22; a execução com agentes reais foi no quality em 2026-11-24 (abaixo), com os cenários 1, 2, 3 e 5 aprovados e o 4 em **NOT RUN**.

## Cenários do quickstart

| Cenário | O que foi validado | Evidência | Com agentes reais |
| --- | --- | --- | --- |
| 1. Definir a janela | Criar, editar e remover pela versão; auditoria em cada host do estágio; só leitura sem permissão | `ChangeWindowEndpointsTests` (PostgreSQL real), E2E `test_define_edit_and_remove_a_window` e `test_without_manage_on_the_stage_the_window_is_read_only` | **PASS** no quality, 2026-11-24 (abaixo) |
| 2. Recusa fora da janela | `409 change_window_closed` com a próxima abertura nas seis ações, sem linha nova em `agent_commands` e com `change_window.denied`; terminal recusado; leituras fora do guard; lote misto | `ChangeWindowEnforcementTests`, `TerminalSessionServiceTests`, `ChangeWindowGuardTests` (arquitetura), E2E da História 2 | **PASS** no quality, 2026-11-24, exceto o terminal (NOT RUN com agente real) |
| 3. Exceção de emergência | Sem o scope, `403 change_override_denied`; com o scope, a ação e o `change_window.override` juntos, com a justificativa mascarada; a próxima ação pede nova exceção; lote com hosts com e sem o scope | `ChangeWindowOverrideTests`, `ChangeWindowGuardTests` (aplicação), E2E da História 3 (containers e remoção de volume) | **PASS** no quality, 2026-11-24 (pela API; o pedido de nova exceção na tela não foi exercitado) |
| 4. Licença restrita e sem capability | Editar dá `403 license_*`; remover dá `204`; a recusa e a exceção continuam | `ChangeWindowEndpointsTests` (licença vencida e sem `change-windows`), regra de arquitetura (o guard não depende do gate) | NOT RUN; observado só o `403 license_invalid` ao editar com a licença ainda não iniciada |
| 5. CE | Sem rotas `/api/v1/change-windows`; calendário vazio; `changeWindow.state` `none` | `CeCompositionTests`; E2E real `test_change_windows_ce.py` | **PASS** no quality, 2026-11-24 (abaixo) |

## Desempenho (T035)

`ChangeWindowGuardImpactTests` com `DOKPOD_TEST_026_IMPACT=1`: mediana das diferenças de p95 de 0,16 ms (meta: até 5 ms). A primeira medição somava 10,7 ms, por causa da busca do `commandId` sobre as 133 partições de `agent_commands`; a busca passou a acontecer só com a janela fechada ([D-001](research.md#d-001-ponto-único-de-aplicação)).

## Execução no quality (T038)

O solicitante escolheu o quality (2026-11-23). Antes da execução, o quality foi de fato atualizado para a v0.12.0: até
então ele rodava a 0.8.0, porque o hook de deploy aprovava releases sem aplicá-las
([bug deploy-hook-falso-sucesso](../../.specify/bugs/deploy-hook-falso-sucesso/assessment.md), #174). O upgrade aplicou as
migrations da 0.8.0 até `202611220001_ChangeWindows` pelo modo `admin` da imagem da API, depois de um backup, e o
`gw-deploy.yml` (run 45304686781) recriou os quatro serviços com o smoke test aprovado.

### Cenário 5: CE (2026-11-24)

- **Ambiente**: quality, v0.12.0 CE (`dokpod-ce-api@sha256:55960770…`), um agente Podman rootless (Podman 6.2.0),
  Keycloak em pt-BR, usuário `dokpod-admin`, pela borda `https://localhost:7443/dokpod/`.
- **Comando**: `frontend/tests/e2e/test_change_windows_ce.py` na imagem `lzocateli/playwright-e2e:0.1.0`, em rede do
  host do quality, com `DOKPOD_E2E_ENVIRONMENT_ID` do agente.
- **Resultado**: 3 de 3 aprovados (39 s).
  - `GET /api/v1/change-windows` e `/api/v1/change-windows/production` → `404`;
  - todos os ambientes visíveis com `changeWindow.state` = `none`;
  - `POST /images/pull` de `docker.io/library/busybox:1.37.0` → `202`, comando concluído em `succeeded` no agente.
- O `conftest.py` da suíte real passou a preencher o login do Keycloak pelos nomes dos campos, porque o quality usa o
  tema em pt-BR.

### Preparação para os cenários 1 a 4 (2026-11-24)

- **Imagem**: a API do quality passou para a Business v0.12.0 (`dokpod-api-business@sha256:982eef29…`, deploy run
  45314914428); BFF, web e agente seguem CE.
- **Licença**: `lic-teste-t038`, de teste, emitida pela `dokpod-license-admin` (commit `6d16492`) com a chave
  `dokpod-2026-1`, a partir do relatório de apuração com o desafio `t038-quality-20261124`. O relatório verificou `ok`,
  sem achados. Business, slot `hml` (homologação) com 4 nodes equivalentes, faixa de 32 vCPU, `change-windows`, de
  2026-11-24T23:00Z a 2026-12-24. Importada por `admin licensing import --slot hml`; às 23:00Z o estado passou a
  `granted`, com consumo 3 de 4 e `metering_unknown` pelos 100 ambientes de carga sem medição. O registro de emissões
  foi um PostgreSQL descartável: a emissão de teste não está no registro do fornecedor.
- **Ambientes e agentes**: "T038 Produção" (`2be3ae88-6a05-43e3-8482-1a990da8974b`, `production`) e "T038 Homologação"
  (`d47e57ec-5c22-47d6-89ee-8e081c789c1d`, `staging`), cada um com um agente real (`t038-agent-prd`,
  `t038-agent-hml`) com certificado de cliente emitido pela CA do laboratório. Os dois agentes usam o mesmo engine
  Podman, então os containers de teste `t038-alvo-1` a `t038-alvo-3` aparecem nos dois hosts; a janela vale pelo
  estágio do host pelo qual a ação chega.
- **Usuários**: `admin-026` (grupo `/dokpod/gestaojanela`, `environment:read` e `environment:manage` em Produção),
  `operador-026` (`/dokpod/operadorteste`, as ações de container nos dois hosts) e `plantao-026` (`/dokpod/plantao`,
  as mesmas ações e `change:override` só em Produção).

### Cenários 1 a 3: PASS (2026-11-24)

- **Comando**: `frontend/tests/e2e/test_change_windows_business.py` na imagem `lzocateli/playwright-e2e:0.1.0`, em rede
  do host do quality, pela borda `https://localhost:7443/dokpod/`. 8 de 8 aprovados (47,6 s, 23:03Z).
- **Cenário 1**: `admin-026` criou a janela de Produção (seg a sex, 22:00–06:00, `America/Sao_Paulo`) com
  `If-None-Match: *`; a vista trouxe faixas, fuso, autor, horário e `ETag`; a auditoria tem `change_window.updated` no
  host de Produção. `operador-026`, sem `environment:manage`, recebe `403` na vista e na edição.
- **Cenário 2**: com uma janela que exclui a hora atual, `operador-026` tentou parar, reiniciar, criar container, baixar
  imagem, remover volume, remover rede e parar um pod em Produção: as sete deram `409 change_window_closed` com
  `nextWindowStartsAt` = `2026-11-25T01:00:00Z` e `timeZone`. A tela do container mostra "Fora da janela de mudança".
  Inspeção, logs, alertas e auditoria do mesmo host deram `200`. No lote, o stop em Produção deu `409` e o de
  Homologação (sem janela) deu `202` e terminou em `succeeded`.
- **Cenário 3**: `operador-026` com `changeWindowOverride` deu `403 change_override_denied`. `plantao-026` parou
  `t038-alvo-2` em Produção com a exceção: `202`, comando `succeeded`, e a auditoria tem `container.stop` e
  `change_window.override` com o mesmo `commandId`, o mesmo autor e a justificativa
  com o segredo mascarado (`token=***`). A ação seguinte sem exceção deu `409`. No lote com exceção, o start em Produção
  passou e o de Homologação, com janela fechada e sem o scope, deu `403 change_override_denied`.
- **Nenhum comando recusado chegou ao agente**: na auditoria do host de Produção da rodada final, os únicos comandos
  aceitos foram o stop e o start com exceção, com dois `container.command.result`; as dez tentativas recusadas
  aparecem só como `change_window.denied`. O agente não registra comandos no nível `info`, então a prova é pela
  auditoria, não pelo log do agente.
- **Limpeza do teste**: as janelas dos dois estágios são removidas (`change_window.removed`) e os containers voltam a
  rodar.

**Não exercitado com agente real**: abrir terminal fora da janela (coberto por `TerminalSessionServiceTests`) e o pedido
de nova exceção na tela (validado pela API).

### Cenário 4: NOT RUN

Exige importar uma licença vencida além da tolerância e outra sem `change-windows`, e nenhuma das duas foi emitida.
Observado só um pedaço: antes das 23:00Z, com a licença importada e ainda não iniciada (`restricted`), editar a janela
deu `403 license_invalid`.

### Achados da execução

1. **Defeito no provisionamento de autorização** (`tools/scripts/provision-environment-authorization.ps1`, publicado no
   instalador 0.12.0): no PowerShell 7.6.9 da imagem, o `Invoke-RestMethod` devolve o array JSON como um único objeto,
   e a busca da policy e da permission pelo nome nunca acha a existente. O segundo scope para o mesmo grupo ou usuário
   tenta recriar a policy e o Keycloak recusa (`403`). Na T038 rodou uma cópia corrigida só nesse ponto. Pede o fluxo
   de Bug Fixing.
2. **Texto da recusa de licença**: o `403 license_invalid` da janela de mudança diz "A licença não permite criar,
   editar, reativar ou testar destinos agora", texto da exportação de auditoria.
3. **Edição de estágio sem hosts legíveis** (decisão humana): pela regra D-007, `admin-026`, que não lê nenhum host de
   Homologação, pode editar a janela de Homologação, porque gerencia todos os hosts que lê. A janela passa a valer em
   hosts que ele não vê.
4. **Quickstart, cenário 1, passo 3**: diz que `operador-026` vê a janela só para leitura; pelo contrato, a vista exige
   `environment:manage` em algum host, e o operador recebe `403`.
5. **Grupos do provisionamento**: o script só aceita grupos `^/dokpod/[a-z]+$`; nomes com hífen ou dígito são recusados.
