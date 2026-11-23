# Threat model: janelas de mudança por estágio

**Spec**: 026, tarefa T001 (gate antes de código) · **Data**: 2026-11-20 · **Origem**: IA assistida, a partir de [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md) e [contrato](contracts/api.md)
**Estado**: **revisado e aceito** pelo solicitante em 2026-11-20 ([decisão](#decisão-do-revisor)).

Este documento cobre só o que a 026 acrescenta: a recusa de ações fora da janela, o scope `change:override` e a configuração das janelas. Ficam fora as fronteiras que já existem (sessão do navegador, PEP com Keycloak, fila de comandos e canal mTLS com o agente, trilha de auditoria e gate de licença). Elas continuam regidas por [segurança](../../docs/seguranca.md), pelo [threat model da 031](../031-licenciamento-business-offline/threat-model.md) e pelo [consolidado da 005](../005-qualificacao-release-docker-linux/threat-model-consolidado.md).

A janela é um controle de **governança**, não de contenção: ela impede que usuários autorizados mudem o host fora do horário combinado. Quem tem acesso direto ao engine, ao host ou ao banco não é contido por ela.

## Como revisar

1. Leia o [modelo do sistema](#modelo-do-sistema) e as [premissas](#premissas-de-confiança).
2. Percorra as [fronteiras](#fronteiras-novas). Cada mitigação aponta o requisito (FR), a decisão (D) e a tarefa (T).
3. Decida cada [lacuna](#lacunas-encontradas): aplicar a recomendação na tarefa indicada ou aceitar sem mudança.
4. Aceite ou recuse cada [risco residual](#registro-de-riscos-residuais).
5. Preencha a [decisão](#decisão-do-revisor). A T001 só é marcada com a decisão registrada.

## Modelo do sistema

```text
CLIENTE (instalação)
  administrador ──HTTPS──▶ web (nginx) ──▶ BFF ──▶ API (PEP) ──▶ Keycloak (environment:manage)
                                                     └─▶ ChangeWindowSettingsService ──▶ gate "change-windows" (só PUT)
                                                           └─▶ PostgreSQL: change_windows (≤ 3 linhas)

  operador / plantão ──▶ web ──▶ BFF ──▶ API (PEP) ──▶ Keycloak (scope da ação; change:override só com exceção)
                                           │
                                           ├─▶ 6 serviços de ação ──▶ ChangeWindowGuard ──▶ ChangeWindowClearance
                                           │                                │                   │
                                           │                     TimeProvider + tzdata          ▼
                                           │                     IAgentCommandStore.GetAsync   AgentCommandQueueService
                                           │                     (Replay)                      (exige a liberação)
                                           │                                                    └─▶ agent_commands + audit_events
                                           └─▶ hub OpenTerminal ──▶ ChangeWindowGuard ──▶ sessão de terminal (spec 014)
```

### Premissas de confiança

- Quem tem `environment:manage` em todos os hosts do estágio que enxerga é administrador e pode afrouxar a janela. A mudança fica auditada em cada host do estágio.
- Quem recebe `change:override` é plantão de confiança. Cada uso exige justificativa e fica auditado, mas a justificativa não é verificada quanto ao conteúdo.
- O relógio do plano de controle é sincronizado (NTP), e o `tzdata` da imagem base é a fonte das regras de fuso.
- Quem controla o host, o engine, o PostgreSQL ou a composição da imagem controla a janela. Não há defesa contra o próprio operador da infraestrutura.

## Fronteiras novas

### B1. Configuração da janela (navegador → API)

| Ameaça | Mitigação | Rastro |
| --- | --- | --- |
| Usuário sem permissão cria, edita ou remove janela | `environment:manage` em todos os hosts legíveis do estágio, decidido pelo Keycloak; falha do Keycloak nega | FR-001, D-007a, T016, T017 |
| Corpo malicioso (propriedades extras, fuso arbitrário, faixas demais) | Contrato fechado; fuso IANA validado pelo `TimeZoneInfo`; até 14 faixas; `CHECK` no banco | D-003, D-004, T003, T011, T016 |
| Corrida entre dois administradores | `If-Match`/`If-None-Match` com a versão | D-007a, T013, T016 |
| Administrador apaga a janela para fugir dela | Permitido por desenho (inclusive com licença restrita); `change_window.removed` em cada host do estágio | FR-005, FR-007, T016, [R4](#registro-de-riscos-residuais) |

### B2. Aplicação da janela no aceite (API → fila)

| Ameaça | Mitigação | Rastro |
| --- | --- | --- |
| Cliente da API ignora a UI e envia a ação direto | A decisão é da API, antes de enfileirar; a tela só avisa | FR-002, D-001, T022 |
| Serviço novo (ou futuro) enfileira comando sem passar pela janela | A fila exige `ChangeWindowClearance`; o construtor é privado e só o guard cria; teste de arquitetura | D-001, T007–T009 |
| Terminal aberto fora da janela, já que não passa pela fila | Guard no `TerminalSessionService` e no hub; teste de arquitetura | D-001, T009, T023, T024 |
| Reenvio com `commandId` de comando existente para burlar a janela | `Replay` só devolve `Duplicate` ou conflito, nunca cria; a busca é por ambiente e `commandId` | D-001, T008, T022 |
| Comando aceito dentro da janela com prazo longo executa muito depois | O prazo é escolhido pelo cliente, sem limite máximo | [L1](#lacunas-encontradas) |
| Descobrir o horário da janela sem poder agir | Ordem: autorização da ação, ambiente, janela; o `409` só chega a quem pode agir | contrato, T022, [L3](#lacunas-encontradas) |
| Leituras bloqueadas por engano (negação de serviço da operação) | Leituras não passam pelo guard; teste das leituras | FR-003, SC-003, T022 |

### B3. Exceção de emergência (`change:override`)

| Ameaça | Mitigação | Rastro |
| --- | --- | --- |
| Usuário sem o scope executa fora da janela | Decisão do Keycloak por host a cada tentativa; falha do Keycloak nega (`503`) | FR-004, D-005, T029, T030 |
| Exceção sem rastro ou com rastro separável da ação | `change_window.override` gravado na mesma transação do comando | FR-005, SC-002, D-006, T030 |
| Período de exceção usado para várias mudanças | Não existe período: cada ação exige nova exceção | FR-004, T030 |
| Justificativa com segredo | Mascarada pelo `JustificationMasker`, como na 016 | D-005, T030, T037 |
| Justificativa trivial ("aaaaaaaaaa") | Só tamanho mínimo; revisão humana pela auditoria | [R2](#registro-de-riscos-residuais) |
| Concessão ampla do scope | Nenhum papel recebe por padrão; concessão por ambiente no Keycloak | D-009, T002 |

### B4. Tempo e fuso

| Ameaça | Mitigação | Rastro |
| --- | --- | --- |
| Relógio do navegador adiantado ou atrasado | A decisão usa o `TimeProvider` da API | D-010, [L4](#lacunas-encontradas) |
| Réplicas com relógios divergentes | NTP como premissa; diferença de segundos nas bordas | D-010, [R3](#registro-de-riscos-residuais) |
| Horário de verão abre ou fecha a janela na hora errada | Regras de buraco e repetição definidas e testadas em dois fusos | D-004, T004 |
| `tzdata` desatualizado na imagem | Regras acompanham a imagem base; runbook | D-004, T036, [R3](#registro-de-riscos-residuais) |

### B5. Composição e licença

| Ameaça | Mitigação | Rastro |
| --- | --- | --- |
| Licença restrita desliga a governança | A aplicação não passa pelo gate; só o `PUT` passa | FR-007, D-002, D-007, T022 |
| Trocar a imagem Business pela CE desliga as janelas | Efeito documentado no runbook | D-002, T036, [R1](#registro-de-riscos-residuais) |

### B6. Estado exibido no console

| Ameaça | Mitigação | Rastro |
| --- | --- | --- |
| Tela desatualizada mostra "aberta" depois do fechamento | A API decide de novo; a tela agenda a troca para `closesAt`/`nextOpensAt` | D-008, T027 |
| Horário da janela visível para quem só lê o host | `changeWindow` vai nas respostas de host para quem lê o host | [L3](#lacunas-encontradas) |

## Lacunas encontradas

| # | Lacuna | Recomendação | Tarefa |
| --- | --- | --- | --- |
| L1 | O prazo do comando (`deadlineUtc`) vem do cliente e só precisa estar no futuro. Um comando aceito dentro da janela com prazo de dias fica pendente e pode executar muito depois do fechamento, quando o agente voltar | Num estágio com janela, limitar o prazo ao fechamento da janela (`closesAt`) mais 5 minutos; um prazo maior é encurtado no aceite, e o comando que não for entregue expira. O caso de borda da spec continua valendo dentro dessa folga | T007, T008, T022 |
| L2 | O `change_window.denied` gera um evento por tentativa; um script que repita a ação enche a trilha | Manter um evento por tentativa (FR-005) e aceitar o volume, porque a trilha já é particionada por mês; o limite de taxa da API, se houver, é tratado fora desta spec | [R5](#registro-de-riscos-residuais) |
| L3 | O contrato diz que o horário da janela não vaza para quem não pode agir, mas `changeWindow` vai nas respostas de host para qualquer leitor | Aceitar: o horário não é segredo e o leitor precisa dele para entender por que a ação não aparece. Ajustar o texto do contrato para "o `409` só chega a quem pode agir" | contrato, T026 |
| L4 | Nenhum teste prova que o horário do cliente não muda a decisão | Teste em que o servidor está fora da janela e o pedido traz cabeçalhos de data do cliente dentro dela; a recusa vale | T022 |
| L5 | A sessão de terminal aberta dentro da janela continua depois do fechamento até os limites da 014 | Aceitar, como a spec define; o runbook registra a duração máxima da sessão | T036 |
| L6 | A busca do `commandId` do `Replay` não pode acontecer antes da autorização, ou quem não pode agir descobriria se um `commandId` existe | Manter a busca dentro do guard, depois da autorização da ação e da checagem do ambiente, e testar que o sem-permissão recebe o `403` da ação | T007, T022 |

## Registro de riscos residuais

| # | Risco | Fronteira | Mitigação atual | Aceite |
| --- | --- | --- | --- | --- |
| R1 | Trocar a imagem Business pela CE desliga as janelas sem aviso | B5 | Troca de edição é ato do operador da infraestrutura; runbook | Aceito (2026-11-20) |
| R2 | O plantão com `change:override` usa justificativas vazias de sentido | B3 | Auditoria com destaque, exceção por ação, scope sem concessão padrão | Aceito (2026-11-20) |
| R3 | Relógio sem NTP ou `tzdata` antigo desloca a janela | B4 | Premissa de NTP; regras da imagem base; runbook | Aceito (2026-11-20) |
| R4 | Um administrador remove a janela, executa e recria | B1 | Remoção e recriação auditadas em cada host do estágio | Aceito (2026-11-20) |
| R5 | Repetição de tentativas recusadas enche a trilha de `change_window.denied` | B2 | Trilha particionada por mês; recusa custa uma consulta e nenhuma chamada ao Keycloak | Aceito (2026-11-20) |
| R6 | Um host que o administrador não enxerga fica sujeito a uma janela que ele editou | B1 | Mesma regra da política de justificativa (016 D-007); evento no host afetado | Aceito (2026-11-20) |
| R7 | Ações feitas direto no engine ou no host não são bloqueadas | — | Fora do alcance do Dokpod; o engine é a fonte de verdade e o inventário mostra a mudança | Aceito (2026-11-20) |

## Controles transversais

- **Autorização**: Keycloak em todos os pontos; falha fechada; nenhuma permissão local.
- **Ponto único**: a fila exige a liberação e só o guard a cria; o terminal tem o guard próprio.
- **Idempotência**: o reenvio de um comando aceito nunca é recusado pela janela.
- **Dados**: a justificativa da exceção é mascarada; nada novo vai para logs, métricas ou traces além do código da recusa.
- **Auditoria**: mudança de janela, recusa e exceção viram eventos na trilha.

## Revisão de segurança (T037)

**Data**: 2026-11-22 · **Origem**: IA assistida, sobre o código integrado (#165, #167, #168, #169) · **Revisor humano**: pendente.

| Ponto | Verificação no código | Resultado |
| --- | --- | --- |
| Ordem das verificações | Nos seis serviços de ação, o guard vem depois da autorização da ação e da checagem do ambiente e antes da justificativa da 016; no terminal, depois de autorização, cadastro, MFA, agente e container. A busca do `commandId` (L6) fica dentro do guard e só com a janela fechada | Conforme; `ChangeWindowEnforcementTests` prova o `403` antes do `409` com `commandId` existente |
| Falha fechada | Decisão de `change:override` indisponível ou negação sem código vira `authorization_unavailable` (`503`); falha ao gravar `change_window.denied` não libera a ação; erro ao ler `change_windows` interrompe o pedido sem enfileirar | Conforme; o último caso responde `500`, e não `503`, como as demais falhas de banco das rotas de ação |
| Ponto único | Só os sete casos de uso que mudam o host dependem do guard; a liberação só é criada por ele; o guard não depende do gate da licença | Conforme (`ChangeWindowGuardTests` de arquitetura) |
| Prazo (L1) e relógio (L4) | Prazo encurtado para o fechamento mais 5 minutos com a janela aberta; a decisão usa só o `TimeProvider` da API | Conforme (`ChangeWindowEnforcementTests`) |
| Justificativa | Mascarada pelo `JustificationMasker` antes de entrar em `change_window.override`; recusas e respostas de erro não a repetem; o guard não registra logs | Conforme (`ChangeWindowGuardTests` com `token=***`) |
| Logs, métricas e traces | A 026 não acrescenta log, métrica nem span; a resposta `409` leva só o código, a próxima abertura e o fuso | Conforme |
| Exposição no console | A opção de exceção só aparece com `change:override` na leitura de grants; a API decide de novo | Conforme (E2E `test_026_change_windows.py`) |

Achados da revisão, para a decisão do revisor humano:

- **A1 (baixo)**: com a janela aberta, o reenvio de um comando aceito numa janela anterior, com prazo encurtado, é comparado pela fila com o prazo exato e pode voltar como `command_id_conflict` em vez de duplicado. Só acontece se o mesmo `Idempotency-Key` for reenviado numa janela seguinte, depois do prazo do comando original. Recomendação: aceitar e registrar no runbook.
- **A2 (baixo)**: falha de banco ao ler a janela responde `500`, e não `503`. Nenhuma ação é aceita. Recomendação: aceitar.
- **A3 (corrigido)**: as ações `change_window.*` não tinham rótulo na auditoria do console; os rótulos foram acrescentados junto com esta revisão.

## Decisão do revisor

| Campo | Valor |
| --- | --- |
| Revisor | Solicitante (lzocateli) |
| Data | 2026-11-20 |
| Premissas (administrador e plantão de confiança, NTP, tzdata da imagem) | Aceitas |
| Lacunas L1–L6 | Recomendações aceitas: L1, L4 e L6 aplicadas nas tarefas e no research; L3 aplicada no contrato; L2 e L5 aceitas sem mudança |
| Riscos R1–R7 | Aceitos (2026-11-20) |
| Declaração | "aceito as recomendações, siga com a T002" |
