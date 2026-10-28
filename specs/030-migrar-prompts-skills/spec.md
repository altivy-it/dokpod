# Especificação da Funcionalidade: Migração de Prompts Auxiliares para Skills

**Branch da Funcionalidade**: `030-migrar-prompts-skills`

**Criada em**: 2026-10-27

**Status**: Rascunho

**Entrada**: Descrição fornecida: "Execute o plano se fizer sentido a migração considerando que o projeto usa Speckit, se sim, depois de convertido, exclua os prompts". Após identificar a necessidade de formalização, o solicitante invocou `/speckit-specify` para esse escopo.

**Origem**: IA assistida, a partir da solicitação humana e da análise dos quatro prompts auxiliares do Dokpod.

**Revisor humano**: Solicitante; identificação nominal a confirmar e revisão desta especificação pendente.

## Cenários de Usuário e Testes *(obrigatório)*

### História de Usuário 1 - Apoiar decisões arquiteturais no Copilot (Prioridade: P1)

Como mantenedor, quero solicitar o registro de uma decisão arquitetural no harness Copilot, sem depender de uma sessão Local e sem contornar as aprovações do Spec Kit.

**Motivo da prioridade**: O fluxo atual depende de um prompt indisponível no harness de destino e envolve decisões cuja aceitação deve continuar humana.

**Teste independente**: Em contexto descartável, solicitar um ADR com referência ao planejamento e autorização explícita; verificar a proposta produzida e repetir sem autorização para confirmar a recusa de escrita.

**Cenários de aceitação**:

1. **Dado** um planejamento Spec Kit com decisão duradoura justificada e autorização explícita, **Quando** o mantenedor invoca o fluxo de ADR no Copilot, **Então** recebe uma proposta vinculada à origem, com alternativas, recomendação verificável, origem IA assistida e revisão humana identificada como pendente.
2. **Dado** um pedido sem justificativa arquitetural ou autorização, **Quando** o fluxo é solicitado, **Então** nenhum ADR é criado e as informações ausentes são indicadas.
3. **Dado** um pedido para aceitar automaticamente a decisão ou implementar código, **Quando** o fluxo é executado, **Então** o ADR permanece `proposed` e nenhuma implementação é iniciada.

---

### História de Usuário 2 - Produzir evidências de teste autorizadas (Prioridade: P1)

Como responsável pela qualidade, quero usar o fluxo de testes no Copilot para uma tarefa ou remediação autorizada, preservando o escopo e a confiabilidade das evidências.

**Motivo da prioridade**: A conversão precisa manter o vínculo entre testes, riscos e autorização, sem transformar um auxílio em processo independente de desenvolvimento.

**Teste independente**: Usar uma tarefa de teste autorizada em cenário isolado e verificar a rastreabilidade dos critérios, comandos, resultados e limitações; repetir com tarefa não autorizada.

**Cenários de aceitação**:

1. **Dado** uma tarefa autorizada via `/speckit-implement` ou remediação autorizada via `/speckit-bug-fix`, **Quando** o responsável solicita testes, **Então** o fluxo relaciona riscos aos níveis adequados, demonstra a capacidade discriminante do teste e devolve evidências ao artefato de origem.
2. **Dado** apenas um diff ou uma Issue sem tarefa ou remediação autorizada, **Quando** o fluxo é solicitado, **Então** não escreve nem executa testes e informa o requisito de autorização ausente.
3. **Dado** um comportamento dependente de engine, banco de dados ou aplicação HTTP, **Quando** os testes são planejados e executados, **Então** são preservadas as exigências vigentes de infraestrutura real, containers, borda nginx e distinção entre plataformas, sem declarar como verificado um alvo não executado.

---

### História de Usuário 3 - Auditar e localizar o Spec Kit com segurança (Prioridade: P2)

Como mantenedor das customizações, quero auditar ou traduzir textos humanos do Spec Kit pelo Copilot, sem alterar contratos operacionais nem instalar componentes implicitamente.

**Motivo da prioridade**: A manutenção em pt-BR deve permanecer disponível após atualizações, preservando componentes gerenciados e alterações locais.

**Teste independente**: Auditar um alvo conhecido sem modo explícito e comparar o conteúdo antes e depois; em cópia descartável, traduzir um alvo explícito e verificar equivalência dos contratos e integridade das exclusões.

**Cenários de aceitação**:

1. **Dado** um alvo informado sem modo explícito, **Quando** o fluxo de localização é solicitado, **Então** apresenta somente evidências de auditoria e não altera arquivos.
2. **Dado** um pedido sem alvo, **Quando** o fluxo é solicitado, **Então** inventaria candidatos e aguarda delimitação antes de traduzir.
3. **Dado** alvo explícito e modo de tradução solicitado, **Quando** a localização ocorre, **Então** somente textos humanos no escopo são traduzidos, preservando comandos, IDs, placeholders, contratos de leitura, segurança, proveniência e arquivos excluídos.
4. **Dado** que fonte da extensão e skill materializada diferem, **Quando** ambas são consideradas, **Então** o fluxo verifica a equivalência antes de replicar traduções e não sobrescreve uma representação com a outra indiscriminadamente.

---

### História de Usuário 4 - Revisar mudanças e concluir a transição (Prioridade: P2)

Como mantenedor, quero um único fluxo de revisão e referências atualizadas para os quatro procedimentos, podendo retirar os prompts sem perder comportamentos ou limites de ferramentas.

**Motivo da prioridade**: A revisão já possui uma skill equivalente; duplicá-la criaria fontes concorrentes, e apagar os prompts antes da validação poderia interromper fluxos existentes.

**Teste independente**: Revisar um diff conhecido pelo fluxo canônico, conferir achados e ausência de operações proibidas, e verificar a cobertura dos quatro fluxos e suas referências antes da retirada das fontes antigas.

**Cenários de aceitação**:

1. **Dado** um diff, branch ou PR e sua base de comparação, **Quando** a revisão é solicitada, **Então** o fluxo canônico apresenta achados por severidade com localização, cenário, impacto, correção e teste, seguidos de dúvidas e lacunas de evidência.
2. **Dado** um pedido para corrigir o diff ou executar validações durante a revisão, **Quando** o revisor o recebe, **Então** não escreve nem executa comandos, testes ou builds e encaminha a necessidade ao responsável autorizado.
3. **Dado** que os quatro fluxos substitutos foram validados no harness Copilot, **Quando** a migração é concluída, **Então** os quatro prompts são excluídos e os guias apontam somente aos fluxos canônicos.
4. **Dado** falha de descoberta, cobertura ou restrição de ferramentas de um substituto, **Quando** a transição é avaliada, **Então** a retirada do prompt correspondente é bloqueada e a limitação é registrada.

### Casos de Borda

- Um nome de skill colide com outra customização disponível: a descoberta deve identificar inequivocamente o fluxo do Dokpod antes da retirada da fonte antiga.
- A skill é descoberta, mas o agente especializado ou uma ferramenta necessária não está disponível: informar o bloqueio, sem ampliar permissões nem declarar equivalência.
- Uma solicitação ambígua parece corresponder a ADR, testes ou tradução: a disponibilidade da skill não autoriza execução automática desses fluxos.
- Falta autorização ou rastreabilidade Spec Kit: fluxos de escrita param; revisão somente leitura sinaliza a lacuna, sem criar escopo.
- A ferramenta de comparação ou o parser necessário não está disponível: registrar validação incompleta, sem instalar dependências ou declarar sucesso automaticamente.
- Existem alterações locais nos alvos: preservá-las; conflito impede sobrescrita silenciosa.
- É necessário validar escrita para comprovar o fluxo: usar cenário descartável ou tarefa explicitamente autorizada, sem executar uma iniciativa real apenas para testar a customização.
- Uma referência histórica menciona os prompts antigos: preservar a evidência histórica, distinguindo-a de orientação operacional ativa.

## Requisitos *(obrigatório)*

### Requisitos Funcionais

- **FR-001**: Os mantenedores DEVEM poder invocar no harness Copilot os quatro fluxos hoje oferecidos por `create-adr`, `generate-tests`, `localize-speckit` e `review-change`, sem depender do Session Target Local.
- **FR-002**: A migração DEVE preservar todas as regras exclusivas de entrada, autorização, procedimento, condições de parada e saída dos quatro fluxos originais, com correspondência verificável entre origem e substituto.
- **FR-003**: Os fluxos de ADR, testes e localização DEVEM manter acionamento explícito pelo usuário; a conversão NÃO DEVE habilitar sua execução automática por decisão do modelo. A política vigente da skill de revisão NÃO DEVE ser alterada silenciosamente.
- **FR-004**: O fluxo de ADR DEVE exigir referência e justificativa no planejamento Spec Kit e autorização humana, comparar no máximo três opções plausíveis, seguir o modelo e a sequência anual vigentes e manter decisões como `proposed`, sem implementar código nem conceder aceite.
- **FR-005**: O fluxo de testes DEVE exigir tarefa ou remediação autorizada, relacionar critérios e riscos aos testes, executar o recorte pertinente e informar comandos, resultados, plataformas e lacunas, sem ampliar requisitos nem corrigir defeitos fora do processo oficial.
- **FR-006**: O fluxo de testes DEVE preservar as regras vigentes de containers, engines e PostgreSQL reais quando aplicáveis, acesso HTTP pela borda nginx e exceção do agente Windows; ausência de infraestrutura necessária DEVE resultar em bloqueio ou evidência explicitamente não executada, nunca falso sucesso.
- **FR-007**: O fluxo de localização DEVE apenas auditar quando o modo não for informado e apenas inventariar candidatos quando o alvo estiver ausente; tradução exige modo e escopo explícitos.
- **FR-008**: A localização DEVE preservar contratos operacionais, exclusões, segurança e alterações locais, distinguir fontes de extensões de skills materializadas e NÃO DEVE instalar, atualizar ou reconciliar componentes nem reescrever manifests ou hashes para ocultar modificações.
- **FR-009**: O fluxo de revisão DEVE reutilizar a skill canônica `pull-request-review`, absorvendo somente diferenças necessárias do prompt antigo, sem criar procedimento equivalente concorrente.
- **FR-010**: A revisão DEVE permanecer restrita a leitura, busca e evidências existentes, sem edição ou execução de comandos, testes, builds ou migrações; achados DEVEM ser rastreáveis e ordenados por severidade, com dúvidas e lacunas explícitas.
- **FR-011**: A migração DEVE verificar os papéis e as ferramentas efetivamente disponíveis dos agentes associados no harness de destino; texto de procedimento NÃO DEVE ser considerado substituto de restrição efetiva de ferramentas, nem seleção de agente deve ser presumida herdada do prompt.
- **FR-012**: Cada fluxo DEVE possuir uma única fonte canônica compartilhada pelo repositório, com orientações em pt-BR e referências válidas às regras e aos modelos existentes, sem duplicação extensa de conteúdo proprietário de outros artefatos.
- **FR-013**: Após validar cobertura, invocação e restrições dos substitutos, a migração DEVE excluir os quatro prompts e atualizar todas as referências operacionais ativas a eles; NÃO DEVE manter pontes Local como parte desta entrega.
- **FR-014**: A migração DEVE preservar as skills oficiais `speckit-*`, fontes e manifests das extensões, configuração de hooks, constituição, artefatos históricos e alterações preexistentes; somente os artefatos oficiais necessários ao andamento desta feature podem ser atualizados por seus respectivos fluxos.
- **FR-015**: A validação DEVE incluir casos positivos e negativos dos quatro fluxos, distinguir verificação documental de execução real e informar explicitamente qualquer resultado `NOT RUN`; nenhum workflow de produto pode ser acionado apenas para testar descoberta.
- **FR-016**: A entrega NÃO DEVE modificar código da aplicação, contratos, deployment, secrets, políticas de aprovação ou aceite de ADRs, nem realizar commit, push, publicação ou deploy por consequência da conversão.

## Critérios de Sucesso *(obrigatório)*

### Resultados Mensuráveis

- **SC-001**: Os mantenedores conseguem selecionar e iniciar os quatro fluxos substitutos em uma sessão nova do Copilot, com uma entrada representativa por fluxo e sem recorrer à sessão Local.
- **SC-002**: A revisão da correspondência entre origens e substitutos comprova preservação de 100% das regras exclusivas dos quatro fluxos, sem revisão duplicada.
- **SC-003**: Em 100% dos cenários negativos de autorização, escrita em revisão e localização sem modo, nenhuma operação proibida é realizada.
- **SC-004**: Ao concluir a transição, nenhum dos quatro prompts permanece disponível e nenhuma referência operacional ativa aponta para um prompt removido.
- **SC-005**: A comparação anterior e posterior confirma nenhuma alteração não autorizada em componentes gerenciados, regras de governança ou arquivos fora do escopo.
- **SC-006**: O relatório final identifica, para cada um dos quatro fluxos, o cenário verificado, o resultado, as evidências e as limitações; nenhuma validação não executada é apresentada como sucesso.

## Premissas

- O alvo principal é o harness Copilot selecionado como Session Target no VS Code, não apenas trocar o nome do modo Agent em uma sessão Local. Preparar infraestrutura para o Copilot cloud agent no GitHub fica fora deste recorte.
- A migração abrange somente os quatro prompts auxiliares existentes. As skills oficiais do Spec Kit já são o mecanismo de integração do processo e não serão convertidas nem regeneradas.
- A análise anterior e o [rascunho de migração](../../.github/plano-migracao-prompts-skills.md) são insumos, não aprovação desta spec, plano ou tarefas futuros.
- Os nomes propostos com prefixo `dokpod-` reduzem ambiguidade com customizações de outros projetos; os detalhes de representação e compatibilidade pertencem à fase de planejamento.
- Modelos, instruções e agentes existentes devem ser reutilizados quando compatíveis, sem presumir que os metadados e ferramentas da sessão Local tenham equivalência automática no destino.
- A solicitação humana registra a intenção de converter e excluir os prompts. A execução depende de plano, tarefas, análise e revisão/autorização correspondentes; a checklist de qualidade não aprova implementação.
- A validação exige acesso a uma sessão do harness de destino. Se isso não estiver disponível, a limitação deve ser registrada e a retirada das fontes permanece bloqueada pelo critério de aceite.