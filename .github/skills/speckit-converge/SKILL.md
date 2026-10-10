---
name: "speckit-converge"
description: "Avaliar o código atual contra a spec, o plano e as tarefas da feature e acrescentar trabalho restante a tasks.md para conclusão pelo fluxo implement."
compatibility: "Requer a estrutura de projeto do spec-kit com o diretório .specify/"
metadata:
  author: "github-spec-kit"
  source: "templates/commands/converge.md"
---


## Entrada do Usuário

```text
$ARGUMENTS
```

Você **DEVE** considerar a entrada do usuário antes de prosseguir (se não estiver vazia).

## Verificações Antes da Execução

**Verifique os hooks de extensões (antes da convergência)**:

- Verifique se `.specify/extensions.yml` existe na raiz do projeto.
- Se existir, leia-o e procure entradas na chave `hooks.before_converge`.
- Se o YAML não puder ser interpretado ou for inválido, não ignore silenciosamente: informe que `.specify/extensions.yml` não pôde ser lido (inclua o erro do parser) e que nenhum hook foi verificado, inclusive hooks obrigatórios (`optional: false`); depois, continue normalmente
- Exclua os hooks cujo `enabled` seja explicitamente `false`. Considere habilitados por padrão aqueles sem o campo `enabled`.
- Para cada hook restante, **não** tente interpretar ou avaliar expressões `condition`:
  - Se não houver `condition`, ou se ela for nula/vazia, considere o hook executável
  - Se houver `condition` não vazia, ignore o hook e deixe a avaliação da condição para a implementação de HookExecutor
- Ao construir invocações a partir dos nomes dos comandos de hooks, substitua pontos (`.`) por hífens (`-`). Por exemplo, `speckit.git.commit` → `/speckit-git-commit`.
- Para cada hook executável, apresente o seguinte conforme seu campo `optional`:
  - **Hook opcional** (`optional: true`):

    ```text
    ## Hooks de Extensões

    **Hook Prévio Opcional**: {extension}
    Comando: `/{command}`
    Descrição: {description}

    Solicitação: {prompt}
    Para executar: `/{command}`
    ```

  - **Hook obrigatório** (`optional: false`):

    ```text
    ## Hooks de Extensões

    **Hook Prévio Automático**: {extension}
    Executando: `/{command}`
    EXECUTE_COMMAND: {command}

    Aguarde o resultado do hook antes de prosseguir para o Objetivo.
    ```
    Após apresentar o bloco, você DEVE invocar o hook e aguardar sua conclusão. Execute-o como executaria o comando nesta sessão (a invocação pode diferir do identificador literal `{command}`; por exemplo, um agente em modo skills usa `/skill:speckit-...` ou `$speckit-...`). Apresentar apenas o bloco não executa o hook.

- Se não houver hooks registrados ou `.specify/extensions.yml` não existir, prossiga sem anunciar essa ausência

## Objetivo

Reduza a distância entre o que a spec, o plano e as tarefas exigem e o que o código implementa.
Leia `spec.md`, `plan.md` e `tasks.md` como **única fonte de intenção**, sob as restrições da
constituição. Avalie o estado atual, identifique requisitos, critérios de aceitação, decisões do
plano e tarefas não atendidos, incompletos ou parcialmente satisfeitos e **acrescente cada trabalho
restante como tarefa nova e rastreável** ao final de `tasks.md`, para que `/speckit-implement`
o conclua. Este comando DEVE executar somente após `/speckit-implement` atuar sobre o `tasks.md`
atual e após `/speckit-tasks` produzir um `tasks.md` completo.

Esta **não** é uma ferramenta de diff e **não** acompanha mudanças. Avalia o código atual
contra os artefatos da feature, sem Git, comparação de branches ou histórico.

## Restrições Operacionais

**SOMENTE ACRESCENTAR, NUNCA REESCREVER**: A **única** escrita permitida é acrescentar
uma seção `## Phase N: Convergence` a `tasks.md`. O comando NÃO DEVE:

- Modificar `spec.md` ou `plan.md` de qualquer forma.
- Reescrever, renumerar, reordenar ou excluir tarefas existentes, inclusive de fases anteriores de convergência.
- Modificar, criar ou excluir código da aplicação; concluir as tarefas acrescidas cabe a `/speckit-implement`.

Se o código já atender a tudo, o comando DEVE manter `tasks.md` **idêntico byte a byte**,
sem cabeçalho vazio de convergência, e informar um resultado sem pendências.

**Autoridade da Constituição**: A constituição (`.specify/memory/constitution.md`) é
**inegociável**. Código que viola um princípio MUST gera um achado de severidade máxima e
uma tarefa de correção correspondente. Se a constituição for um template não preenchido,
ignore essas verificações adequadamente em vez de falhar.

## Etapas de Execução

### 1. Inicializar o Contexto de Convergência

Execute `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireSpec -RequireTasks -IncludeTasks` uma vez na raiz e interprete FEATURE_DIR e AVAILABLE_DOCS do JSON. Derive os caminhos absolutos:

- SPEC = FEATURE_DIR/spec.md
- PLAN = FEATURE_DIR/plan.md
- TASKS = FEATURE_DIR/tasks.md
- CONSTITUTION = `.specify/memory/constitution.md` (se existir).
Se faltar `spec.md`, `plan.md` ou `tasks.md`, PARE com mensagem clara indicando o pré-requisito:
`/speckit-specify` para spec, `/speckit-plan` para plano ou `/speckit-tasks` para tarefas.
Não produza saída parcial.
Para aspas simples em argumentos como "I'm Groot", use escape: por exemplo, 'I'\''m Groot' (ou aspas duplas, se possível: "I'm Groot").

### 2. Carregar os Artefatos Progressivamente

Carregue somente o contexto mínimo de cada artefato:

**De spec.md:**

- Requisitos funcionais (FR-###).
- Critérios de sucesso (SC-###): inclua somente trabalho implementável, excluindo métricas pós-lançamento e KPIs de negócio.
- Histórias de usuário e cenários de aceitação.
- Casos de borda, se houver.

**De plan.md:**

- Escolhas de arquitetura/stack e decisões técnicas.
- Referências ao modelo de dados.
- Fases e arquivos/componentes que o plano prevê criar ou editar.
- Restrições técnicas.

**De tasks.md:**

- IDs das tarefas, para calcular o próximo ID e número da fase.
- Descrições, agrupamento por fase e caminhos referenciados.

**Da constituição, se não for um template vazio:**

- Nomes dos princípios e declarações normativas MUST/SHOULD.

### 3. Construir o Inventário de Intenções

Crie um modelo interno, sem reproduzir os artefatos brutos:

- **Inventário de requisitos**: Uma chave estável por FR-### / SC-### / cenário de aceitação
  (como `US1/AC2`), além de decisões do plano e princípios com obrigações implementáveis.
- **Mapa do escopo de código**: A partir dos caminhos de `plan.md` e `tasks.md` e da busca
  por palavras-chave dos requisitos, derive os arquivos e componentes da avaliação.
  Limite-se a eles; **não** infira escopo além dos artefatos.

### 4. Avaliar o Código e Classificar Achados

Inclua todas as tarefas existentes, independentemente do checkbox ou da fase de convergência:
afirmações de conclusão não são evidência. Verifique o comportamento atual contra spec, plano,
tarefas e constituição; em cadeias de correção, avalie o resultado, não detalhes de implementação
substituídos. Verifique obrigações não atendidas e implementação que contradiga, exceda ou esteja
fora da intenção declarada.

Inspecione o código no escopo de cada item e produza um `Finding` somente se houver lacuna.
Classifique por **tipo de lacuna**:

- **`missing`**: O trabalho exigido está inteiramente ausente.
- **`partial`**: Existe, mas não satisfaz integralmente o requisito, critério ou decisão.
- **`contradicts`**: Conflita com a intenção ou com um princípio MUST da constituição.
- **`unrequested`**: Não foi solicitado pela spec, plano ou tarefas. Sinalize para ciência:
  converge **não** exclui código, apenas acrescenta tarefa para revisar/justificar ou removê-lo.

Cada `Finding` registra ID estável, referência `source-ref`, `gap-type`, severidade e
descrição curta com evidência do arquivo/área observados.

**Casos de borda:**

- **Pouco ou nenhum código**: Considere todo o escopo especificado como trabalho `missing`, em vez de falhar.
- **Nada restante**: Produza zero achados e siga o caminho convergente da etapa 7.

### 5. Atribuir Severidade

- **CRITICAL**: Viola MUST da constituição ou uma lacuna `missing`/`contradicts` bloqueia a funcionalidade básica de história P1.
- **HIGH**: Lacuna `missing` ou `partial` em requisito ou critério central.
- **MEDIUM**: Lacuna `partial` em requisito secundário ou adição `unrequested` sem justificativa clara.
- **LOW**: Lacuna parcial pequena, refinamento ou adição `unrequested` de baixo risco.

### 6. Apresentar o Resumo na Sessão

Antes de acrescentar conteúdo, apresente resumo conciso por severidade, sem escrever arquivos:

## Achados de Convergência

| ID | Tipo de Lacuna | Severidade | Origem | Evidência | Trabalho Restante |
|----|----------|----------|--------|----------|----------------|
| F1 | missing  | HIGH     | FR-008 | Exemplo: sem proteção de escrita append-only em path/to/module.py ao escrever tasks.md | Adicionar proteção append-only |

**Métricas do resumo:**

- Requisitos e critérios verificados.
- Decisões do plano verificadas.
- Princípios verificados, ou "ignorados: template".
- Achados por tipo (missing / partial / contradicts / unrequested).
- Achados por severidade.

### 7. Acrescentar Tarefas ou Informar Convergência

**Se houver achados acionáveis**, resultado `tasks_appended`:

Acrescente ao **final** de `tasks.md`, conforme o contrato:

1. Examine todos os IDs; `M` é o maior. Determine a próxima fase `N` (maior fase + 1).
2. Escreva um único cabeçalho novo `## Phase N: Convergence`.
3. Gere um item por achado acionável, com CRITICAL/HIGH primeiro e IDs com zeros à esquerda `T{M+1:03d}, T{M+2:03d}, …`:

   ```markdown
   - [ ] T042 <imperative description> per <source-ref> (<gap-type>)
   ```

  `<source-ref>` rastreia a origem: por exemplo, `FR-003`, `SC-002`,
  `US1/AC2`, `plan: storage decision`, `Constitution II`.

  `<gap-type>` é `missing`, `partial`, `contradicts` ou `unrequested`.

  Tarefas por violação da constituição DEVEM aparecer primeiro e ser descritas como `CRITICAL`.
4. Nunca reutilize ou renumere IDs. Se já houver fase de convergência, acrescente outra abaixo,
  numerada separadamente; não altere a anterior.

**Se não houver achados acionáveis**, resultado `converged`:

- **Não** modifique `tasks.md`, nem adicione cabeçalho vazio.
- Informe: **"✅ Converged — a implementação atende à spec, ao plano e às tarefas."**
- Inclua as contagens do que foi verificado.

### 8. Indicar Próximas Ações

- Para `tasks_appended`, informe quantidade e fase das tarefas acrescidas e recomende
  `/speckit-implement`; uma nova convergência encontrará menos itens ou nenhuma pendência.
- Para `converged`, recomende revisão/abertura de PR. Não é necessária outra implementação
  para o escopo especificado.

### 9. Verificar Hooks de Extensões

Após produzir o resultado, verifique se `.specify/extensions.yml` existe na raiz.

- Se existir, leia-o e procure entradas na chave `hooks.after_converge`.
- Se o YAML não puder ser interpretado ou for inválido, não ignore silenciosamente: informe que `.specify/extensions.yml` não pôde ser lido (inclua o erro do parser) e que nenhum hook foi verificado, inclusive hooks obrigatórios (`optional: false`); depois, continue normalmente
- Exclua os hooks cujo `enabled` seja explicitamente `false`. Considere habilitados por padrão aqueles sem o campo `enabled`.
- Para cada hook restante, **não** tente interpretar ou avaliar expressões `condition`:
  - Se não houver `condition`, ou se ela for nula/vazia, considere o hook executável
  - Se houver `condition` não vazia, ignore o hook e deixe a avaliação da condição para a implementação de HookExecutor
- Informe o resultado (`converged` ou `tasks_appended`) na sessão antes de listar hooks,
  para que o usuário decida se executará os comandos opcionais seguintes.
- Ao construir invocações a partir dos nomes dos comandos de hooks, substitua pontos (`.`) por hífens (`-`). Por exemplo, `speckit.git.commit` → `/speckit-git-commit`.
- Para cada hook executável, apresente o seguinte conforme seu campo `optional`:
  - **Hook opcional** (`optional: true`):

    ```text
    ## Hooks de Extensões

    **Hook Opcional**: {extension}
    Comando: `/{command}`
    Descrição: {description}

    Solicitação: {prompt}
    Para executar: `/{command}`
    ```

  - **Hook obrigatório** (`optional: false`):

    ```text
    ## Hooks de Extensões

    **Hook Automático**: {extension}
    Executando: `/{command}`
    EXECUTE_COMMAND: {command}
    ```
    Após apresentar o bloco, você DEVE invocar o hook e aguardar sua conclusão. Execute-o como executaria o comando nesta sessão (a invocação pode diferir do identificador literal `{command}`; por exemplo, um agente em modo skills usa `/skill:speckit-...` ou `$speckit-...`). Apresentar apenas o bloco não executa o hook.

- Se não houver hooks registrados ou `.specify/extensions.yml` não existir, prossiga sem anunciar essa ausência
