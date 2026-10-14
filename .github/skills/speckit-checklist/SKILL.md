---
name: "speckit-checklist"
description: "Gerar uma checklist personalizada para a feature atual com base nos requisitos do usuário."
compatibility: "Requer a estrutura de projeto do spec-kit com o diretório .specify/"
metadata:
  author: "github-spec-kit"
  source: "templates/commands/checklist.md"
---


## Finalidade da Checklist: "Testes Unitários da Redação"

**CONCEITO CRÍTICO**: Checklists são **TESTES UNITÁRIOS DA REDAÇÃO DOS REQUISITOS**: validam qualidade, clareza e completude dos requisitos de um domínio.

**NÃO são verificações/testes de implementação**:

- ❌ NÃO "Verifique se o clique no botão funciona corretamente".
- ❌ NÃO "Teste se o tratamento de erros funciona".
- ❌ NÃO "Confirme que a API retorna 200".
- ❌ NÃO verificar se código/implementação correspondem à spec.

**Servem para validar a qualidade dos requisitos**:

- ✅ "Há requisitos de hierarquia visual para todos os tipos de cards?" (completude).
- ✅ "A 'exibição em destaque' tem tamanho/posição quantificados?" (clareza).
- ✅ "Os requisitos de hover são consistentes entre os elementos interativos?" (consistência).
- ✅ "Há requisitos de acessibilidade para navegação por teclado?" (cobertura).
- ✅ "A spec define o que acontece se a imagem do logo não carregar?" (casos de borda).

**Metáfora**: Se a spec é código escrito em linguagem natural, a checklist é sua suíte de testes unitários. Você verifica se os requisitos estão bem escritos, completos, inequívocos e prontos para implementar, NÃO se a implementação funciona.

**Responsabilidade e ciclo de vida dos checkboxes**:

- Checklists personalizadas são artefatos de revisão dos requisitos sob responsabilidade do revisor.
- `[x]` significa que o revisor considerou satisfeito o critério de qualidade.
- `[x]` NÃO significa implementação concluída.
- Este comando gera/acrescenta itens; NÃO DEVE marcá-los `[x]`.
- Um agente só pode auxiliar na avaliação quando o revisor pedir explicitamente.
- `checklists/requirements.md` é uma checklist separada, mantida por `/speckit-specify` e `/speckit-clarify`; essa exceção não se aplica às personalizadas geradas aqui.

## Entrada do Usuário

```text
$ARGUMENTS
```

Você **DEVE** considerar a entrada do usuário antes de prosseguir (se não estiver vazia).

## Verificações Antes da Execução

**Verifique os hooks de extensões (antes de gerar a checklist)**:
- Verifique se `.specify/extensions.yml` existe na raiz do projeto.
- Se existir, leia-o e procure entradas na chave `hooks.before_checklist`.
- Se o YAML não puder ser interpretado ou for inválido, não ignore silenciosamente: informe que `.specify/extensions.yml` não pôde ser lido (inclua o erro do parser) e que nenhum hook foi verificado, inclusive hooks obrigatórios (`optional: false`); depois, continue normalmente
- Exclua os hooks cujo `enabled` seja explicitamente `false`. Considere habilitados por padrão aqueles sem o campo `enabled`.
- Para cada hook restante, **não** tente interpretar ou avaliar expressões `condition`:
  - Se não houver `condition`, ou se ela for nula/vazia, considere o hook executável
  - Se houver `condition` não vazia, ignore o hook e deixe a avaliação da condição para a implementação de HookExecutor
- Ao construir invocações a partir dos nomes dos comandos de hooks, substitua pontos (`.`) por hífens (`-`). Por exemplo, `speckit.git.commit` → `/speckit-git-commit`.
- Para cada hook executável, apresente o seguinte conforme seu campo `optional`:
  - **Hook opcional** (`optional: true`):
    ```
    ## Hooks de Extensões

    **Hook Prévio Opcional**: {extension}
    Comando: `/{command}`
    Descrição: {description}

    Solicitação: {prompt}
    Para executar: `/{command}`
    ```
  - **Hook obrigatório** (`optional: false`):
    ```
    ## Hooks de Extensões

    **Hook Prévio Automático**: {extension}
    Executando: `/{command}`
    EXECUTE_COMMAND: {command}

    Aguarde o resultado do hook antes de prosseguir para as Etapas de Execução.
    ```
    Após apresentar o bloco, você DEVE invocar o hook e aguardar sua conclusão. Execute-o como executaria o comando nesta sessão (a invocação pode diferir do identificador literal `{command}`; por exemplo, um agente em modo skills usa `/skill:speckit-...` ou `$speckit-...`). Apresentar apenas o bloco não executa o hook.
- Se não houver hooks registrados ou `.specify/extensions.yml` não existir, prossiga sem anunciar essa ausência

## Etapas de Execução

1. **Preparação**: Execute `.specify/scripts/powershell/check-prerequisites.ps1 -Json -Template checklist-template` na raiz e interprete FEATURE_DIR, AVAILABLE_DOCS e TEMPLATE_CONTENT do JSON.
  - Todos os caminhos devem ser absolutos.
  - Para aspas simples em argumentos como "I'm Groot", use escape: por exemplo, 'I'\''m Groot' (ou aspas duplas, se possível: "I'm Groot").

2. **SE EXISTIR**: Carregue `.specify/memory/constitution.md` para consultar princípios e restrições de governança.

3. **Esclareça a intenção dinamicamente**: Derive até TRÊS perguntas contextuais iniciais, sem catálogo pré-definido. Elas DEVEM:
  - Resultar da solicitação e dos sinais extraídos de spec/plano/tarefas.
  - Tratar apenas de informações que mudem materialmente a checklist.
  - Ser omitidas individualmente quando `$ARGUMENTS` já for inequívoco.
  - Priorizar precisão, não amplitude.

  Algoritmo de geração:
  1. Extraia palavras-chave do domínio (auth, latência, UX, API), sinais de risco ("crítico", "deve", "conformidade"), público ("QA", "revisão", "equipe de segurança") e entregas explícitas ("a11y", "rollback", "contratos").
  2. Agrupe em até 4 áreas candidatas, ordenadas por relevância.
  3. Identifique público e momento prováveis (autor, revisor, QA, release), se não explícitos.
  4. Detecte dimensões ausentes: amplitude, profundidade/rigor, ênfase de risco, exclusões e critérios mensuráveis.
  5. Formule perguntas destes tipos:
    - Escopo: "Incluímos integrações com X e Y ou somente a correção local do módulo?".
    - Risco: "Quais destas áreas exigem verificações obrigatórias?".
    - Profundidade: "É uma lista simples pré-commit ou uma verificação formal de release?".
    - Público: "Será usada só pelo autor ou pelos pares na revisão do PR?".
    - Exclusões: "Devemos excluir otimização de desempenho nesta rodada?".
    - Lacuna de cenários: "Não há fluxos de recuperação: rollback/falhas parciais estão no escopo?".

  Formatação das perguntas:
  - Se houver opções, use tabela concisa: Opção | Candidata | Por que Importa.
  - Limite a A–E; omita a tabela se a resposta livre for mais clara.
  - Nunca peça repetição do que o usuário já disse.
  - Não invente categorias; se houver dúvida, peça: "Confirme se X pertence ao escopo".

  Padrões quando não for possível interagir:
  - Profundidade: Padrão.
  - Público: Revisor de PR para código; autor nos demais casos.
  - Foco: As 2 áreas mais relevantes.

  Apresente Q1/Q2/Q3. Após as respostas, se ≥2 classes de cenário (alternativo, exceção, recuperação ou não funcional) continuarem indefinidas, PODE fazer até DUAS perguntas adicionais (Q4/Q5), justificadas em uma linha, como "Risco de recuperação não resolvido". Não exceda cinco perguntas. Não amplie se o usuário recusar.

4. **Compreenda a solicitação**: Combine `$ARGUMENTS` e respostas:
  - Derive o tema (segurança, revisão, deploy, UX).
  - Consolide itens obrigatórios explicitados.
  - Relacione os focos à estrutura de categorias.
  - Infira contexto faltante de spec/plano/tarefas, sem inventar.

5. **Carregue o contexto** de FEATURE_DIR:
  - spec.md: Requisitos e escopo.
  - plan.md, se existir: Detalhes técnicos e dependências.
  - tasks.md, se existir: Tarefas de implementação.

  **Estratégia de contexto**:
  - Leia só os trechos necessários aos focos ativos, sem despejar arquivos inteiros.
  - Resuma seções longas em itens concisos de cenário/requisito.
  - Carregue progressivamente, buscando mais somente quando detectar lacunas.
  - Em documentos grandes, use resumos intermediários, não texto bruto incorporado.

6. **Gere a checklist** usando TEMPLATE_CONTENT e crie "Testes Unitários dos Requisitos":
   - Crie `FEATURE_DIR/checklists/` se não existir.
   - Gere um nome único:
     - Curto e descritivo do domínio, como `ux.md`, `api.md` ou `security.md`.
     - Formato: `[domain].md`.
   - Tratamento do arquivo:
     - Se NÃO existir, crie-o e numere os itens a partir de CHK001.
     - Se existir, acrescente continuando o último CHK; após CHK015, comece em CHK016.
   - Nunca exclua/substitua conteúdo existente; preserve e acrescente.
   - Deixe todo item novo desmarcado (`[ ]`); o estado pertence ao revisor.

  **PRINCÍPIO CENTRAL: Teste os Requisitos, Não a Implementação**:
  Cada item DEVE avaliar OS PRÓPRIOS REQUISITOS quanto a:
  - **Completude**: Todos os necessários estão presentes?
  - **Clareza**: São inequívocos e específicos?
  - **Consistência**: Estão alinhados entre si?
  - **Mensurabilidade**: Podem ser verificados objetivamente?
  - **Cobertura**: Todos os cenários/casos de borda estão contemplados?

  **Categorias**: Agrupe por dimensões de qualidade:
  - **Completude**: Todos os requisitos necessários documentados?
  - **Clareza**: Requisitos específicos e inequívocos?
  - **Consistência**: Alinhados e sem conflitos?
  - **Qualidade da aceitação**: Critérios mensuráveis?
  - **Cobertura de cenários**: Todos os fluxos/casos contemplados?
  - **Casos de borda**: Condições de limite definidas?
  - **Não funcionais**: Desempenho, segurança, acessibilidade etc. especificados?
  - **Dependências e premissas**: Documentadas e validadas?
  - **Ambiguidades e conflitos**: O que precisa de esclarecimento?

  **COMO ESCREVER ITENS: "Testes Unitários da Redação"**:

  ❌ **INCORRETO**, testa implementação:
  - "Verifique se a página exibe 3 cards de episódios".
  - "Teste se hover funciona no desktop".
  - "Confirme que clicar no logo leva ao início".

  ✅ **CORRETO**, testa qualidade dos requisitos:
  - "Quantidade e disposição exatas dos episódios em destaque estão especificadas?" [Completeness]
  - "A 'exibição em destaque' tem tamanho/posição quantificados?" [Clarity]
  - "Os requisitos de hover são consistentes entre elementos interativos?" [Consistency]
  - "Há requisitos de navegação por teclado para toda UI interativa?" [Coverage]
  - "Há comportamento alternativo especificado se o logo não carregar?" [Edge Cases]
  - "Há estados de carregamento definidos para dados assíncronos de episódios?" [Completeness]
  - "A spec define a hierarquia visual entre elementos concorrentes?" [Clarity]

  **ESTRUTURA DO ITEM**:
  Cada item deve seguir este padrão:
  - Pergunta sobre qualidade do requisito.
  - Foco no que está ESCRITO, ou ausente, na spec/plano.
  - Dimensão de qualidade entre colchetes [Completeness/Clarity/Consistency/etc.].
  - Referência `[Spec §X.Y]` para requisitos existentes.
  - Marcador `[Gap]` para requisitos ausentes.

  **EXEMPLOS POR DIMENSÃO**:

  Completude:
  - "Há requisitos de tratamento para todos os modos de falha da API? [Gap]"
  - "Há requisitos de acessibilidade para todos os elementos interativos? [Completeness]"
  - "Há breakpoints mobile definidos para layouts responsivos? [Gap]"

  Clareza:
  - "'Carregamento rápido' tem limites de tempo quantificados? [Clarity, Spec §NFR-2]"
  - "Os critérios de seleção de episódios relacionados estão explícitos? [Clarity, Spec §FR-5]"
  - "'Destaque' tem propriedades visuais mensuráveis? [Ambiguity, Spec §FR-4]"

  Consistência:
  - "Requisitos de navegação estão alinhados entre todas as páginas? [Consistency, Spec §FR-10]"
  - "Requisitos de cards são consistentes entre páginas iniciais e de detalhe? [Consistency]"

  Cobertura:
  - "Há requisitos para o estado vazio, sem episódios? [Coverage, Edge Case]"
  - "Interações concorrentes estão contempladas? [Coverage, Gap]"
  - "Há requisitos para falhas parciais de carregamento? [Coverage, Exception Flow]"

  Mensurabilidade:
  - "Requisitos de hierarquia visual são mensuráveis/testáveis? [Acceptance Criteria, Spec §FR-1]"
  - "'Peso visual equilibrado' é verificável objetivamente? [Measurability, Spec §FR-2]"

  **Classificação e cobertura de cenários**, com foco nos requisitos:
  - Verifique requisitos para cenários principais, alternativos, exceção/erro, recuperação e não funcionais.
  - Para cada classe: "Os requisitos de [tipo de cenário] são completos, claros e consistentes?".
  - Se ausente: "Foram excluídos intencionalmente ou estão faltando? [Gap]".
  - Em mutações, inclua resiliência/rollback: "Há rollback definido para falhas de migração? [Gap]".

  **Rastreabilidade**:
  - MÍNIMO: ≥80% dos itens DEVEM incluir ao menos uma referência.
  - Referencie `[Spec §X.Y]` ou use `[Gap]`, `[Ambiguity]`, `[Conflict]`, `[Assumption]`.
  - Sem sistema de IDs: "Há esquema de IDs para requisitos e critérios de aceitação? [Traceability]".

  **Identifique e resolva problemas de qualidade**:
  Pergunte sobre os próprios requisitos:
  - Ambiguidade: "'Rápido' é quantificado com métricas específicas? [Ambiguity, Spec §NFR-1]".
  - Conflito: "A navegação conflita entre §FR-10 e §FR-10a? [Conflict]".
  - Premissa: "A disponibilidade permanente da API de podcasts foi validada? [Assumption]".
  - Dependência: "Há requisitos documentados para a API externa? [Dependency, Gap]".
  - Definição ausente: "'Hierarquia visual' tem critérios mensuráveis? [Gap]".

  **Consolidação**:
  - Limite flexível: se houver >40 candidatos, priorize por risco/impacto.
  - Una quase duplicatas do mesmo aspecto.
  - Com >5 casos de borda de baixo impacto, reúna: "Os casos X, Y, Z estão contemplados? [Coverage]".

  **🚫 ABSOLUTAMENTE PROIBIDO**, pois testa implementação:
  - ❌ Itens iniciados por "Verifique", "Teste", "Confirme", "Confira" com comportamento implementado.
  - ❌ Referências a execução, ações do usuário ou comportamento do sistema.
  - ❌ "Exibe corretamente", "funciona bem", "funciona como esperado".
  - ❌ "Clique", "navegue", "renderize", "carregue", "execute".
  - ❌ Casos/planos de teste e procedimentos de QA.
  - ❌ Detalhes de implementação (frameworks, APIs, algoritmos).

  **✅ PADRÕES OBRIGATÓRIOS**, para qualidade dos requisitos:
  - ✅ "Há [tipo de requisito] definido/especificado/documentado para [cenário]?".
  - ✅ "[Termo vago] é quantificado/esclarecido com critérios específicos?".
  - ✅ "Os requisitos são consistentes entre [seção A] e [seção B]?".
  - ✅ "[Requisito] é mensurável/verificável objetivamente?".
  - ✅ "[Casos de borda/cenários] estão contemplados nos requisitos?".
  - ✅ "A spec define [aspecto ausente]?".

7. **Referência de estrutura**: Siga o template canônico em `.specify/templates/checklist-template.md` para título, metadados, categorias, responsabilidade, notas e IDs. Se indisponível, use H1, finalidade/data, nota explicando que `[x]` é aprovação do revisor sobre qualidade dos requisitos, categorias `##` com linhas `- [ ] CHK### <requirement item>` e IDs globalmente crescentes a partir de CHK001; registre que `/speckit-implement` lê o estado sem alterar marcadores.

8. **Relatório**: Informe caminho completo, quantidade de itens e se criou ou acrescentou ao arquivo. Resuma:
  - Áreas selecionadas.
  - Profundidade.
  - Responsável/momento.
  - Itens obrigatórios explicitados pelo usuário e incorporados.

**Importante**: Cada `/speckit-checklist` usa nome curto e descritivo, criando arquivo ou acrescentando ao existente. Isso permite:

- Várias checklists por tipo, como `ux.md`, `test.md` e `security.md`.
- Nomes simples e memoráveis que indicam a finalidade.
- Identificação e navegação fáceis em `checklists/`.

Use tipos descritivos e, ao concluir, organize checklists obsoletas para evitar acúmulo.

## Tipos de Checklist e Exemplos

**Qualidade de requisitos de UX:** `ux.md`

Exemplos que testam requisitos, NÃO implementação:

- "A hierarquia visual tem critérios mensuráveis? [Clarity, Spec §FR-1]"
- "Quantidade e posição dos elementos estão explícitas? [Completeness, Spec §FR-1]"
- "Estados de interação (hover, foco, ativo) têm requisitos consistentes? [Consistency]"
- "Há acessibilidade especificada para todos os elementos interativos? [Coverage, Gap]"
- "Há alternativa definida para falhas de imagens? [Edge Case, Gap]"
- "'Exibição em destaque' é mensurável objetivamente? [Measurability, Spec §FR-4]"

**Qualidade de requisitos de API:** `api.md`

Exemplos:

- "Há formatos de erro para todos os cenários de falha? [Completeness]"
- "Há limites de taxa quantificados? [Clarity]"
- "A autenticação é consistente entre endpoints? [Consistency]"
- "Há retry/timeout definido para dependências externas? [Coverage, Gap]"
- "A estratégia de versionamento está documentada? [Gap]"

**Qualidade de requisitos de desempenho:** `performance.md`

Exemplos:

- "Há métricas específicas de desempenho? [Clarity]"
- "Há metas para todas as jornadas críticas? [Coverage]"
- "Há requisitos para cargas diferentes? [Completeness]"
- "O desempenho exigido é mensurável objetivamente? [Measurability]"
- "Há degradação definida sob carga alta? [Edge Case, Gap]"

**Qualidade de requisitos de segurança:** `security.md`

Exemplos:

- "Há autenticação para todos os recursos protegidos? [Coverage]"
- "Há proteção definida para informações sensíveis? [Completeness]"
- "O modelo de ameaças está documentado e alinhado aos requisitos? [Traceability]"
- "A segurança é consistente com as obrigações de conformidade? [Consistency]"
- "Há resposta definida para falhas/violações de segurança? [Gap, Exception Flow]"

## Exemplos do que NÃO Fazer

**❌ INCORRETO: Testa implementação, não requisitos:**

```markdown
- [ ] CHK001 - Verifique se a página exibe 3 cards de episódios [Spec §FR-001]
- [ ] CHK002 - Teste se hover funciona no desktop [Spec §FR-003]
- [ ] CHK003 - Confirme que clicar no logo leva ao início [Spec §FR-010]
- [ ] CHK004 - Confira se episódios relacionados exibe 3-5 itens [Spec §FR-005]
```

**✅ CORRETO: Testa qualidade dos requisitos:**

```markdown
- [ ] CHK001 - Quantidade e disposição dos episódios estão explícitas? [Completeness, Spec §FR-001]
- [ ] CHK002 - Há requisitos consistentes de hover para todos os elementos interativos? [Consistency, Spec §FR-003]
- [ ] CHK003 - A navegação está clara para elementos clicáveis da marca? [Clarity, Spec §FR-010]
- [ ] CHK004 - A seleção dos episódios relacionados está documentada? [Gap, Spec §FR-005]
- [ ] CHK005 - Há estados de carregamento para dados assíncronos de episódios? [Gap]
- [ ] CHK006 - "Hierarquia visual" é mensurável objetivamente? [Measurability, Spec §FR-001]
```

**Diferenças principais:**

- Incorreto: Testar se o sistema funciona.
- Correto: Testar se os requisitos estão bem escritos.
- Incorreto: Verificar comportamento.
- Correto: Validar qualidade dos requisitos.
- Incorreto: "Faz X?".
- Correto: "X está claramente especificado?".

## Verificações Após a Execução

**Verifique os hooks de extensões (após gerar a checklist)**:
Verifique se `.specify/extensions.yml` existe na raiz do projeto.
- Se existir, leia-o e procure entradas na chave `hooks.after_checklist`.
- Se o YAML não puder ser interpretado ou for inválido, não ignore silenciosamente: informe que `.specify/extensions.yml` não pôde ser lido (inclua o erro do parser) e que nenhum hook foi verificado, inclusive hooks obrigatórios (`optional: false`); depois, continue normalmente
- Exclua os hooks cujo `enabled` seja explicitamente `false`. Considere habilitados por padrão aqueles sem o campo `enabled`.
- Para cada hook restante, **não** tente interpretar ou avaliar expressões `condition`:
  - Se não houver `condition`, ou se ela for nula/vazia, considere o hook executável
  - Se houver `condition` não vazia, ignore o hook e deixe a avaliação da condição para a implementação de HookExecutor
- Ao construir invocações a partir dos nomes dos comandos de hooks, substitua pontos (`.`) por hífens (`-`). Por exemplo, `speckit.git.commit` → `/speckit-git-commit`.
- Para cada hook executável, apresente o seguinte conforme seu campo `optional`:
  - **Hook opcional** (`optional: true`):
    ```
    ## Hooks de Extensões

    **Hook Opcional**: {extension}
    Comando: `/{command}`
    Descrição: {description}

    Solicitação: {prompt}
    Para executar: `/{command}`
    ```
  - **Hook obrigatório** (`optional: false`):
    ```
    ## Hooks de Extensões

    **Hook Automático**: {extension}
    Executando: `/{command}`
    EXECUTE_COMMAND: {command}
    ```
    Após apresentar o bloco, você DEVE invocar o hook e aguardar sua conclusão. Execute-o como executaria o comando nesta sessão (a invocação pode diferir do identificador literal `{command}`; por exemplo, um agente em modo skills usa `/skill:speckit-...` ou `$speckit-...`). Apresentar apenas o bloco não executa o hook.
- Se não houver hooks registrados ou `.specify/extensions.yml` não existir, prossiga sem anunciar essa ausência
