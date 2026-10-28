# Especificação da Feature: Implantação somente por código

**Branch da feature**: `025-029-roteiro-recursos`

**Criada em**: 2026-10-27

**Estado**: `draft` - aguardando revisão humana.

**Entrada**: Pedido do solicitante em 2026-10-27: "Itens de referência que ainda não estão no dokpod, inclua todos no roadmap", item 4 de 5: política que restringe implantações em produção ao que vem de código versionado.

**Depende de**: [013](../013-criacao-container/spec.md) (criação de container), [012](../012-imagens-pull/spec.md) (pull), [016](../016-governanca-alertas/spec.md) e [022](../022-configuracoes/spec.md) (políticas por estágio), [026](../026-janelas-de-mudanca/spec.md) (exceção de emergência). A jornada 2 depende de stacks, Compose e GitOps, ainda sem spec ([licenciamento](../../docs/licenciamento.md#business)).

## Esclarecimentos

### Sessão 2026-10-27 (recomendações adotadas, sujeitas à aprovação)

- P: O que a política bloqueia? → R: Em estágios com a política ligada, criar container pelo formulário e baixar imagem manualmente. Operações de ciclo de vida (parar, reiniciar, pausar) e leitura continuam permitidas, porque são resposta operacional e não mudam o que está implantado.
- P: Como implantar então? → R: Pelo fluxo de código versionado (stacks e GitOps), quando existir. Até lá, a política só pode ser ligada com a exceção de emergência disponível, para não travar o estágio.
- P: Há exceção? → R: Sim, a mesma exceção de emergência da spec 026 (`change:override`, com justificativa e auditoria em destaque).

## Cenários do Usuário e Testes (obrigatório)

### Jornada do Usuário 1 - Bloquear implantação manual em Produção (Prioridade: P1)

O administrador liga "Implantação somente por código" em Produção; a partir daí, ninguém cria container ou baixa imagem manualmente nesse estágio.

**Valor desta prioridade**: garante que o que roda em Produção tem origem rastreável em código revisado.

**Teste independente**: ligar a política, tentar criar container e baixar imagem pela tela e pela API em Produção e em Homologação e conferir recusas e auditoria.

**Cenários de aceite**:

1. **Dado** a política ligada em Produção, **Quando** um operador tenta criar container ou baixar imagem em host de Produção, **Então** a ação é recusada antes de chegar ao host, com a mensagem "Produção aceita somente implantação por código".
2. **Dado** a política ligada, **Quando** o operador para, reinicia ou pausa um container, **Então** a ação segue as regras existentes.
3. **Dado** o formulário Novo container, **Quando** o estágio escolhido tem a política ligada, **Então** o formulário indica o bloqueio antes do envio.

---

### Jornada do Usuário 2 - Implantar pelo fluxo de código (Prioridade: P2)

Com stacks e GitOps disponíveis, uma alteração aprovada no repositório é implantada em Produção mesmo com a política ligada.

**Valor desta prioridade**: completa a política com o caminho permitido.

**Teste independente**: com o fluxo de código disponível, aplicar uma alteração do repositório em Produção e conferir que passa, com a origem (repositório e revisão) na auditoria.

**Cenários de aceite**:

1. **Dado** a política ligada e uma implantação originada do fluxo de código, **Quando** aplicada, **Então** ela é aceita e a auditoria registra repositório e revisão de origem.

---

### Jornada do Usuário 3 - Exceção de emergência (Prioridade: P2)

Durante um incidente, quem tem `change:override` cria um container de correção em Produção com justificativa.

**Valor desta prioridade**: a política não pode impedir a resposta a incidentes.

**Teste independente**: com e sem `change:override`, tentar criar container em Produção e conferir resultado e auditoria.

**Cenários de aceite**:

1. **Dado** o scope `change:override`, **Quando** confirma a exceção com justificativa, **Então** a ação é executada e auditada como exceção.

---

## Casos de Borda

- Política ligada sem fluxo de código disponível: a tela avisa que só exceções permitirão implantar no estágio.
- Ação em lote que inclui criação em vários estágios: recusa só os itens de estágios com a política ligada.
- Política desligada depois de ligada: vale a partir do momento da alteração, auditada.

## Requisitos (obrigatório)

### Requisitos Funcionais

- **FR-001**: O sistema DEVE oferecer, por estágio, a política "Implantação somente por código", editável por quem tem `environment:manage` em todos os hosts do estágio e auditada.
- **FR-002**: Com a política ligada, a API DEVE recusar criação de container e pull manual nos hosts do estágio, antes de enviar o comando ao host.
- **FR-003**: Leitura e operações de ciclo de vida NÃO DEVEM ser afetadas pela política.
- **FR-004**: Implantações originadas do fluxo de código versionado DEVEM ser aceitas e auditadas com repositório e revisão de origem, quando esse fluxo existir.
- **FR-005**: A exceção de emergência DEVE seguir a spec 026 (`change:override`, justificativa obrigatória, auditoria em destaque).
- **FR-006**: As telas DEVEM indicar o bloqueio antes da tentativa, com texto.

## Entidades Principais

- **Política de implantação por código**: estágio, ligada ou não, autor, horário da alteração.

## Critérios de Sucesso (obrigatório)

### Resultados Mensuráveis

- **SC-001**: 100% das criações e pulls manuais em estágio com a política ligada são recusados sem chegar ao host.
- **SC-002**: 100% das implantações aceitas em estágio com a política ligada têm origem em código ou exceção auditada.
- **SC-003**: Nenhuma operação de ciclo de vida é recusada pela política na suíte de testes.

## Premissas

- Edição prevista: comercial (Business), junto com stacks e GitOps ([licenciamento](../../docs/licenciamento.md#business)).
- A jornada 1 pode ser entregue antes do fluxo de código, desde que a spec 026 (exceção de emergência) esteja disponível.
- Fora do escopo: o próprio fluxo de stacks e GitOps, que terá spec própria.
