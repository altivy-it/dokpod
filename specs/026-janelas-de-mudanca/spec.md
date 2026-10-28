# Especificação da Feature: Janelas de mudança por estágio

**Branch da feature**: `025-029-roteiro-recursos`

**Criada em**: 2026-10-27

**Estado**: `draft` - aguardando revisão humana.

**Entrada**: Pedido do solicitante em 2026-10-27: "Itens de referência que ainda não estão no dokpod, inclua todos no roadmap", item 2 de 5: janelas de mudança por estágio, complementando a justificativa obrigatória.

**Depende de**: [016](../016-governanca-alertas/spec.md) (estágios e justificativa obrigatória), [022](../022-configuracoes/spec.md) (vista Políticas), [010](../010-lista-containers-acoes-lote/spec.md), [013](../013-criacao-container/spec.md), [014](../014-terminal-container/spec.md), [018](../018-volumes/spec.md) e [019](../019-redes/spec.md) (ações sujeitas à janela), [017](../017-auditoria/spec.md) (trilha).

## Esclarecimentos

### Sessão 2026-10-27 (recomendações adotadas, sujeitas à aprovação)

- P: Quais ações a janela controla? → R: As que mudam o estado do host: parar, pausar, reiniciar, iniciar, remover, criar container, baixar imagem, remover volume ou rede e abrir terminal. Leitura, logs, inspeção, alertas e auditoria nunca são bloqueados.
- P: Como a janela é definida? → R: Por estágio, com dias da semana, horário de início e fim e fuso horário; um estágio pode ter mais de uma faixa. Sem janela configurada, nada muda.
- P: E em emergência? → R: Ação fora da janela é recusada, salvo exceção de emergência por quem tiver o scope novo `change:override` no host, com justificativa obrigatória, registrada na auditoria com destaque.
- P: A janela substitui a justificativa da spec 016? → R: Não. As duas regras se somam: dentro da janela, a justificativa continua obrigatória onde estiver ligada.

## Cenários do Usuário e Testes (obrigatório)

### Jornada do Usuário 1 - Definir janelas por estágio (Prioridade: P1)

O administrador define em Configurações → Políticas que Produção só aceita mudanças de segunda a sexta, das 22h às 6h, no fuso de São Paulo.

**Valor desta prioridade**: reduz incidentes causados por mudanças em horário comercial e formaliza o acordo com as áreas de negócio.

**Teste independente**: criar a janela, conferir que aparece na vista Políticas e na auditoria, e que ações dentro e fora do horário têm o resultado esperado.

**Cenários de aceite**:

1. **Dado** um administrador com `environment:manage` em todos os hosts do estágio, **Quando** salva uma janela, **Então** ela vale imediatamente, aparece na vista Políticas com autor e horário e fica na auditoria.
2. **Dado** um usuário sem a permissão em algum host do estágio, **Quando** vê a janela, **Então** ela aparece somente para leitura.

---

### Jornada do Usuário 2 - Ser impedido de mudar fora da janela (Prioridade: P1)

O operador tenta parar um container de Produção às 15h de uma terça e recebe recusa clara, com a próxima janela.

**Valor desta prioridade**: sem a recusa no próprio console, a janela é só um combinado.

**Teste independente**: tentar cada ação controlada fora da janela, pela tela e pela API, e conferir a recusa, a mensagem e o registro.

**Cenários de aceite**:

1. **Dado** um horário fora da janela, **Quando** o operador aciona uma ação controlada, **Então** a ação é recusada antes de chegar ao host, com a mensagem "Fora da janela de mudança" e o início da próxima janela.
2. **Dado** uma ação em lote com hosts de estágios diferentes, **Quando** parte dos hosts está fora da janela, **Então** só esses itens são recusados, com o motivo por item.
3. **Dado** as telas do console, **Quando** o estágio está fora da janela, **Então** os botões das ações controladas indicam o bloqueio e a próxima janela antes do clique.

---

### Jornada do Usuário 3 - Exceção de emergência (Prioridade: P2)

Durante um incidente, quem tem `change:override` executa a ação fora da janela com justificativa.

**Valor desta prioridade**: a janela não pode impedir a resposta a incidentes.

**Teste independente**: com e sem `change:override`, tentar a ação fora da janela e conferir resultado e auditoria.

**Cenários de aceite**:

1. **Dado** o scope `change:override`, **Quando** o usuário confirma a exceção com justificativa, **Então** a ação é executada e a auditoria registra "exceção de janela" com autor, justificativa e ação.
2. **Dado** um usuário sem o scope, **Quando** tenta a exceção, **Então** ela não é oferecida e a API recusa.

---

## Casos de Borda

- Janela que cruza a meia-noite (22h às 6h): pertence ao dia de início.
- Mudança de horário de verão no fuso escolhido: o horário local da janela é preservado.
- Comando aceito dentro da janela e entregue ao host depois do fim: executa, pois a decisão vale no aceite; comandos que expiram seguem a regra existente.
- Sessão de terminal aberta dentro da janela: continua até os limites da spec 014; abrir sessão nova fora da janela é recusado.
- Relógio do plano de controle é a referência; divergência do navegador não altera a decisão.

## Requisitos (obrigatório)

### Requisitos Funcionais

- **FR-001**: O sistema DEVE permitir, por estágio, nenhuma ou várias faixas de janela com dias da semana, início, fim e fuso horário, editáveis por quem tem `environment:manage` em todos os hosts do estágio.
- **FR-002**: A API DEVE recusar, fora da janela, as ações controladas listadas nos esclarecimentos, antes de enviar o comando ao host, com motivo e próxima janela.
- **FR-003**: Leitura, logs, inspeção, alertas e auditoria NÃO DEVEM ser afetados pela janela.
- **FR-004**: O sistema DEVE oferecer exceção de emergência a quem tem o scope `change:override` no host, com justificativa obrigatória.
- **FR-005**: Criação, alteração e remoção de janelas, recusas e exceções DEVEM ser auditadas.
- **FR-006**: As telas DEVEM indicar o bloqueio e a próxima janela antes da tentativa, com texto, não só cor.

## Entidades Principais

- **Janela de mudança**: estágio, faixas (dias, início, fim), fuso, autor e horário da última alteração.
- **Exceção de janela**: ação, host, autor, justificativa, horário.

## Critérios de Sucesso (obrigatório)

### Resultados Mensuráveis

- **SC-001**: 100% das ações controladas tentadas fora da janela são recusadas sem chegar ao host, pela tela e pela API.
- **SC-002**: 100% das exceções de emergência ficam na auditoria com autor e justificativa.
- **SC-003**: Nenhuma ação de leitura é recusada por janela em toda a suíte de testes.
- **SC-004**: O operador identifica a próxima janela sem sair da tela em que tentou a ação.

## Premissas

- Edição prevista: comercial (Business), como política centralizada de governança ([licenciamento](../../docs/licenciamento.md#business)).
- O scope `change:override` é criado no resource server do Keycloak pelo script de configuração e concedido por ambiente, como os demais.
- Fora do escopo: calendário de feriados, aprovação por um segundo usuário e integração com ferramentas de ITSM.
