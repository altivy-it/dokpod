# Pesquisa: Janelas de mudança por estágio

**Spec**: [spec.md](spec.md) | **Data**: 2026-11-20 | **Origem**: IA assistida; decisões técnicas sujeitas à revisão humana (lzocateli)

Cada decisão resolve um ponto do contexto técnico do [plano](plan.md). As decisões de produto vêm das sessões de esclarecimento de 2026-10-27 e 2026-11-20 da spec.

## D-001: Ponto único de aplicação

- **Decisão**: `AgentCommandQueueService.EnqueueAsync` passa a exigir um `ChangeWindowClearance`, que só o `ChangeWindowGuard` cria: o construtor é privado e a única fábrica é um método do próprio guard. Um construtor `internal` não bastaria, porque a fila e os seis serviços estão no mesmo assembly (`Dokpod.ControlPlane.Application`); o `ChangeWindowGuardTests` de arquitetura também reprova qualquer outro tipo que crie a liberação. Os seis serviços que enfileiram comandos (`ContainerCommandService`, `ContainerCreationService`, `ImagePullService`, `VolumeRemovalService`, `NetworkRemovalService` e `PodActionService`) pedem a liberação ao guard antes de enfileirar. O `TerminalSessionService`, que abre sessão sem passar pela fila, também consulta o guard. O guard não tem lista de ações liberadas: todo `AgentCommandKind` é controlado.
- **Justificativa**: FR-002 pede que uma ação mutável nova fique sujeita à janela por padrão. Com a liberação obrigatória na única porta de entrada da fila, um serviço novo que enfileire comando sem passar pelo guard não compila. O `ChangeWindowGuardTests` em `Dokpod.Architecture.Tests` cobre o terminal, que não usa a fila. A liberação leva o ambiente e o tipo da ação, e a fila recusa quando ela não bate com o comando.
- **Repetição idempotente (princípio IV)**: com a janela fechada, o guard procura o comando pelo `commandId` (`IAgentCommandStore.GetAsync`) antes de recusar. Ajuste da T035 (2026-11-22): a busca saiu de antes da avaliação, porque sem a chave de partição ela percorre todas as partições de `agent_commands` (cerca de 8 ms com 133 partições); com a janela aberta, a fila já trata o duplicado como antes da 026. Se ele já existe, a liberação sai no modo `Replay`, sem avaliar a janela nem pedir exceção, e a fila devolve `Duplicate` ou `ConflictingPayload` como hoje; com `Replay`, a fila nunca cria comando novo (se a linha sumiu por expurgo, a fila recusa e o serviço trata como pedido novo, avaliando a janela). Assim, o reenvio de um comando aceito dentro da janela recebe o mesmo resultado depois que ela fecha, em vez de um `409` que faria o cliente achar que a ação não aconteceu.
- **Catálogo atual**: os tipos 1 a 11 do `AgentCommandKind` (iniciar, parar, reiniciar, remover, pausar, retomar, baixar imagem, criar container, remover volume, remover rede e ações de pod), mais abrir terminal. "Remover imagem" e "criar volume ou rede", citados no esclarecimento de 2026-11-20, ainda não existem no produto; vão entrar pela mesma regra quando forem criados.
- **Alternativas**: verificar em cada endpoint, como a justificativa da 016 (repete a regra em sete lugares e uma ação nova escapa por esquecimento); verificar no envio ao agente (a recusa chegaria depois do aceite, contrariando o caso de borda "a decisão vale no aceite").

## D-002: Composição entre CE e Business

- **Decisão**: a porta `IChangeWindowSchedule` fica na aplicação. A CE registra `NoChangeWindows`, que nunca fecha nada; o módulo Business (`ChangeWindowModule`) registra a implementação sobre o PostgreSQL. O `ChangeWindowGuard` é da CE e não depende do `IBusinessCapabilityGate`. As rotas de configuração existem só na composição Business.
- **Justificativa**: a 031 (D-009) proíbe caso de uso da CE depender do gate. Além disso, a aplicação da janela não pode depender da licença (FR-007). Na CE, o guard libera sempre, sem consultar banco nem Keycloak.
- **Risco aceito**: trocar uma instalação Business pela imagem CE desliga as janelas sem aviso, porque a tabela fica no banco mas a CE não a lê. O runbook registra esse efeito.
- **Alternativas**: o guard inteiro na Business, com um decorator da fila (a CE não precisa de nada, mas a fila ficaria sem a liberação obrigatória e o D-001 perderia a garantia de compilação).

## D-003: Persistência

- **Decisão**: uma tabela `change_windows`, com uma linha por estágio que tem janela: fuso, faixas em `jsonb`, versão, autor e horário. Faixas validadas na aplicação. Migration `<data>_ChangeWindows` com `Up` e `Down`, aplicada nas duas edições, como a da 027.
- **Justificativa**: são no máximo três linhas (os estágios do `EnvironmentStage`), lidas inteiras a cada decisão. Uma tabela filha de faixas só acrescentaria junção e transação. Não cresce, então não precisa de particionamento.
- **Alternativas**: tabela de faixas separada (normalização sem consulta que precise dela); configuração em arquivo (não seria editável pelo console nem auditada).

## D-004: Fuso e cálculo da janela

- **Decisão**: o fuso é um identificador IANA validado com `TimeZoneInfo.TryFindSystemTimeZoneById`. O cálculo fica numa função pura, `ChangeWindowCalendar`, que recebe o instante UTC e devolve o estado (`open`/`closed`), quando a janela atual fecha (`closesAt`) e quando a próxima abre (`nextOpensAt`):
  - uma faixa tem dias da semana, início e fim em minutos; se o fim for menor que o início, a faixa cruza a meia-noite e pertence ao dia de início;
  - início igual ao fim é recusado; faixas sobrepostas valem como união, e faixas contíguas são unidas no cálculo de `closesAt`;
  - quando o início ou o fim cai no buraco da mudança de horário de verão, vale o primeiro instante válido depois do buraco; quando o horário se repete, vale a primeira ocorrência para o início e a segunda para o fim. Assim o horário local é preservado e a duração da faixa pode variar em uma hora nesses dias;
  - a busca da próxima abertura cobre 8 dias locais, o suficiente para qualquer faixa semanal;
  - até 14 faixas por estágio.
- **tzdata**: conferido em 2026-11-20 que `lzocateli/dotnet-aspnet:10.0.15-noble-v1`, base das imagens CE e Business, traz o pacote `tzdata` (`/usr/share/zoneinfo/America/Sao_Paulo`). As regras de fuso acompanham a atualização da imagem base. Os testes fixam fusos e instantes e não dependem do fuso do host.
- **Alternativas**: NodaTime (Apache-2.0; resolve DST com mais clareza, mas é dependência nova para um cálculo pequeno e testável); deslocamento fixo em vez de fuso (quebraria o horário de verão, contra o caso de borda da spec).

## D-005: Exceção de emergência

- **Decisão**: o pedido da ação ganha o campo opcional `changeWindowOverride: true` e usa o campo `justification` que já existe (10 a 500 caracteres, mascarado pelo `JustificationMasker`). Com a janela fechada:
  - sem o campo, a API responde `409 change_window_closed` com `nextWindowStartsAt`;
  - com o campo e sem justificativa válida, responde `422 justification_required`;
  - com o campo, o guard pede ao Keycloak a decisão de `change:override` no recurso `urn:dokpod:environment:{id}`. Negada, a API responde `403 change_override_denied`; indisponível, `503`, falhando fechado como as demais decisões.
- **Lote**: o console já envia as ações em lote item a item (spec 010), então cada item leva o campo e a mesma justificativa e recebe o seu resultado. Os hosts sem o scope são recusados um a um, sem estado compartilhado entre itens. Não existe período de exceção.
- **Junto com a 016**: um só campo de justificativa atende às duas regras. Com a exceção, a justificativa é obrigatória mesmo no estágio sem a política de justificativa, e o registro em `action_justifications` continua como hoje para as ações justificadas da 016.
- **Alternativas**: um token de exceção emitido por um endpoint próprio (dá um período de exceção, que o esclarecimento recusou); o scope `environment:manage` no lugar de um scope novo (amplia o privilégio de quem administra hosts).

## D-006: Auditoria

- **Decisão**: categoria `governance`, com as ações:
  - `change_window.updated` e `change_window.removed`: um evento por host do estágio, com fuso e faixas anteriores e novos, como o `registry.policy_changed`;
  - `change_window.denied` (resultado `Denied`): uma recusa por tentativa, com ação, host e próxima janela;
  - `change_window.override`: gravado na mesma transação do comando, com ação, host, autor, justificativa mascarada e `commandId`.
- **Mudança na fila**: `IAgentCommandStore.EnqueueAuditedAsync` passa a aceitar a lista de eventos do comando (o da ação e, quando houver, o da exceção), para os dois serem gravados juntos ou nenhum. No terminal, a recusa usa o `container.exec.denied` que já existe, com o motivo `change_window_closed`, e a exceção grava `change_window.override` antes de abrir a sessão.
- **Justificativa**: FR-005 e SC-002. Um evento próprio da exceção permite filtrar a auditoria por "exceção de janela" sem varrer atributos.
- **Alternativas**: só um atributo no evento da ação (não aparece no filtro de ação da 017).

## D-007: Licença

- **Decisão**: capability `change-windows`, que já está em `LicenseCapabilities`. `PUT` de janela passa pelo gate (`403 license_*`). `GET` e `DELETE` não passam pelo gate, e a aplicação da janela e a exceção também não. Uma licença sem a capability é tratada como licença restrita, como na 027 (D-008).
- **Justificativa**: FR-007. Remover continua permitido para o cliente nunca ficar preso a uma regra que não consegue mudar.

## D-007a: Permissão de edição

- **Decisão**: `PUT` e `DELETE` exigem `environment:manage` em todos os hosts legíveis do estágio, a mesma regra da política de justificativa (016 D-007). O `GET` devolve `editable` por estágio. A edição usa `If-Match` com a versão (`412` em conflito) e `If-None-Match: *` para criar.
- **Host sem estágio**: o `EnvironmentRegistration.Stage` é opcional, e um host sem estágio não fica sujeito a nenhuma janela. A spec não trata esse caso; ele vai para a revisão humana.
- **Emenda (decisão humana de 2026-11-24 ("D-007: B", revisor @lzocateli), a partir do achado 3 da T038 da spec 026)**: a edição exige ao menos um host legível e habilitado no estágio e `environment:manage` em todos eles. Sem host legível no estágio, `editable` é falso e `PUT`/`DELETE` respondem `403 authorization_denied`, sem revelar se o estágio tem hosts. Deixa de valer o fallback para "todos os hosts legíveis", herdado da 016 D-007, que permitia editar a janela de um estágio cujos hosts o usuário não vê. Tarefas T039–T041.

## D-008: Estado para a tela

- **Decisão**: as respostas de host (`HostResponses`) ganham `changeWindow`: `{ state, closesAt, nextOpensAt, timeZone }`, com `state` igual a `open`, `closed` ou `none`, calculado no momento da resposta. Na CE, o campo vem `none`. O console agenda a troca de estado para `closesAt` e `nextOpensAt`, sem polling, e mostra nos botões das ações controladas o texto "Fora da janela de mudança", com a próxima abertura no fuso do estágio.
- **Exceção na tela**: só aparece para quem tem `change:override` no host. O scope entra na leitura de grants (`IEnvironmentGrantReader`) que o console já usa para mostrar e esconder ações. A API decide de novo em toda tentativa.
- **Alternativas**: endpoint próprio de estado (mais uma chamada por tela); evento SignalR de abertura e fechamento (cria temporizador no servidor para algo que o cliente calcula sozinho).

## D-009: Scope `change:override` no Keycloak

- **Decisão**: o `tools/scripts/configure-keycloak.ps1` cria o scope UMA (`Ensure-AuthorizationScopes`), e o `tools/scripts/provision-environment-authorization.ps1` passa a aceitá-lo no `ValidateSet` e o concede por ambiente, como os demais. Nenhum papel recebe o scope por padrão.
- **Cadastro dos hosts**: `change:override` entra em `EnvironmentResourceScopes` (`backend/libs/Dokpod.Domain/Environments/EnvironmentRegistration.cs`, constante e `Supported`), porque o `Scopes` do cadastro limita o que a leitura de grants devolve ao console (`EnvironmentGrants.Restrict`). A migration da T011 acrescenta o scope ao `scopes` dos ambientes já cadastrados, no padrão de `202610150004_NetworkInventory`, e o `Down` o remove. O cadastro só declara o scope; a concessão a usuários continua no Keycloak. A atualização de `docs/configuracao-keycloak.md` vem junto.

## D-010: Relógio e comandos em andamento

- **Prazo do comando (threat model L1)**: num estágio com janela, o prazo pedido (`deadlineUtc`) fica limitado ao fechamento da janela (`closesAt`) mais 5 minutos; um prazo maior é encurtado no aceite. Sem isso, um comando aceito dentro da janela com prazo de dias executaria muito depois. Com exceção de emergência, o prazo pedido vale como hoje.
- **Decisão**: a decisão usa o `TimeProvider` da API no momento do aceite. Comando já aceito executa mesmo que a janela feche antes da entrega, dentro da folga do prazo, e o reenvio dele com o mesmo `commandId` devolve `Duplicate` ([D-001](#d-001-ponto-único-de-aplicação)); os prazos de expiração existentes continuam valendo. A sessão de terminal aberta dentro da janela continua até os limites da 014. Com várias réplicas, cada uma usa o próprio relógio, e a instalação já pressupõe NTP.

## D-011: Desempenho

- **Decisão**: o guard lê `change_windows` numa consulta de chave primária por decisão, sem cache. A busca do `commandId` só acontece com a janela fechada (ver D-001). Medição da T035 (2026-11-22, PostgreSQL 17.6 local): a janela aberta soma 0,16 ms ao p95 de uma ação (mediana de cinco comparações de 30 + 30); antes do ajuste, somava 10,7 ms. Fora da janela e sem exceção, nenhuma chamada ao Keycloak é acrescentada; com a exceção, acrescenta uma. Meta: a decisão soma no máximo 5 ms ao p95 de uma ação na matriz de 100 hosts (ADR 2026-0003).
- **Alternativas**: cache em memória com invalidação por versão (só faz sentido se a medição mostrar custo, o que não se espera com três linhas).
