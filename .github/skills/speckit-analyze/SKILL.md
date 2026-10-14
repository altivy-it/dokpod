---
name: "speckit-analyze"
description: "Realizar uma análise não destrutiva de consistência e qualidade entre spec.md, plan.md e tasks.md após a geração das tarefas."
compatibility: "Requer a estrutura de projeto do spec-kit com o diretório .specify/"
metadata:
  author: "github-spec-kit"
  source: "templates/commands/analyze.md"
---


## Entrada do Usuário

```text
$ARGUMENTS
```

Você **DEVE** considerar a entrada do usuário antes de prosseguir (se não estiver vazia).

## Verificações Antes da Execução

**Verifique os hooks de extensões (antes da análise)**:
- Verifique se `.specify/extensions.yml` existe na raiz do projeto.
- Se existir, leia-o e procure entradas na chave `hooks.before_analyze`.
- Se o YAML não puder ser interpretado ou for inválido, não ignore silenciosamente: informe que `.specify/extensions.yml` não pôde ser lido (inclua o erro do parser) e que nenhum hook foi verificado, inclusive hooks obrigatórios (`optional: false`); depois, continue normalmente.
- Exclua os hooks cujo `enabled` seja explicitamente `false`. Considere habilitados por padrão aqueles sem o campo `enabled`.
- Para cada hook restante, **não** tente interpretar ou avaliar expressões `condition`:
  - Se não houver `condition`, ou se ela for nula/vazia, considere o hook executável.
  - Se houver `condition` não vazia, ignore o hook e deixe a avaliação da condição para a implementação de HookExecutor.
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

    Aguarde o resultado do hook antes de prosseguir para o Objetivo.
    ```
    Após apresentar o bloco, você DEVE invocar o hook e aguardar sua conclusão. Execute-o como executaria o comando nesta sessão (a invocação pode diferir do identificador literal `{command}`; por exemplo, um agente em modo skills usa `/skill:speckit-...` ou `$speckit-...`). Apresentar apenas o bloco não executa o hook.
  - Se não houver hooks registrados ou `.specify/extensions.yml` não existir, prossiga sem anunciar essa ausência.

## Objetivo

Identifique inconsistências, duplicações, ambiguidades e itens insuficientemente especificados entre os três artefatos centrais (`spec.md`, `plan.md`, `tasks.md`) antes da implementação. Este comando DEVE executar somente depois que `/speckit-tasks` produzir um `tasks.md` completo com sucesso.

## Restrições Operacionais

**ESTRITAMENTE SOMENTE LEITURA**: **Não** modifique arquivos. Apresente um relatório estruturado de análise. Ofereça um plano opcional de correção (o usuário deve aprová-lo explicitamente antes de invocar manualmente comandos posteriores de edição).

**Autoridade da Constituição**: A constituição do projeto (`.specify/memory/constitution.md`) é **inegociável** neste escopo. Conflitos com ela são automaticamente CRITICAL e exigem ajuste da spec, do plano ou das tarefas, não enfraquecimento, reinterpretação ou omissão silenciosa do princípio. Se um princípio precisar mudar, isso deve ocorrer em uma atualização explícita e separada da constituição, fora de `/speckit-analyze`.

## Etapas de Execução

### 1. Inicializar o Contexto da Análise

Execute `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireSpec -RequireTasks -IncludeTasks` uma vez na raiz do repositório e interprete o JSON para obter FEATURE_DIR e AVAILABLE_DOCS. Derive os caminhos absolutos:

- SPEC = FEATURE_DIR/spec.md
- PLAN = FEATURE_DIR/plan.md
- TASKS = FEATURE_DIR/tasks.md

Interrompa com uma mensagem de erro se algum arquivo obrigatório estiver ausente (oriente o usuário a executar o comando de pré-requisito correspondente).
Para aspas simples em argumentos como "I'm Groot", use escape: por exemplo, 'I'\''m Groot' (ou aspas duplas, se possível: "I'm Groot").

### 2. Carregar os Artefatos Progressivamente

Carregue somente o contexto mínimo necessário de cada artefato:

**De spec.md:**

- Visão geral/contexto
- Requisitos funcionais
- Critérios de sucesso (resultados mensuráveis, como desempenho, segurança, disponibilidade, sucesso do usuário e impacto de negócio)
- Histórias de usuário
- Casos de borda (se houver)

**De plan.md:**

- Escolhas de arquitetura e stack
- Referências ao modelo de dados
- Fases
- Restrições técnicas

**De tasks.md:**

- Identificadores das tarefas
- Descrições
- Agrupamento por fase
- Marcadores de paralelismo [P]
- Caminhos de arquivos referenciados

**Da constituição:**

- Carregue `.specify/memory/constitution.md` para validar os princípios.

### 3. Construir Modelos Semânticos

Crie representações internas (não inclua os artefatos brutos na saída):

- **Inventário de requisitos**: Para cada requisito funcional (FR-###) e critério de sucesso (SC-###), registre uma chave estável. Use o identificador FR-/SC- explícito como chave primária quando existir e, opcionalmente, derive um slug de frase imperativa para facilitar a leitura (por exemplo, "O usuário pode enviar arquivo" → `user-can-upload-file`). Inclua somente critérios de sucesso que exijam trabalho implementável, como infraestrutura de testes de carga e ferramentas de auditoria de segurança; exclua métricas pós-lançamento e KPIs de negócio, como "Reduzir chamados de suporte em 50%".
- **Inventário de histórias/ações**: Ações distintas do usuário com critérios de aceitação.
- **Mapeamento de cobertura das tarefas**: Relacione cada tarefa a um ou mais requisitos ou histórias (inferência por palavras-chave ou referências explícitas, como IDs e frases-chave).
- **Conjunto de regras da constituição**: Extraia os nomes dos princípios e as declarações normativas MUST/SHOULD (DEVE/DEVERIA).

### 4. Verificar Problemas com Eficiência de Contexto

Priorize achados relevantes. Limite o total a 50 achados; agregue o restante em um resumo.

#### A. Detecção de Duplicações

- Identifique requisitos quase duplicados.
- Marque as redações de menor qualidade para consolidação.

#### B. Detecção de Ambiguidades

- Sinalize adjetivos vagos (rápido, escalável, seguro, intuitivo, robusto) sem critérios mensuráveis.
- Sinalize placeholders não resolvidos (TODO, TKTK, ???, `<placeholder>` etc.).

#### C. Especificação Insuficiente

- Requisitos com verbos, mas sem objeto ou resultado mensurável.
- Histórias de usuário sem alinhamento dos critérios de aceitação.
- Tarefas que referenciam arquivos ou componentes não definidos na spec/plano.

#### D. Alinhamento com a Constituição

- Qualquer requisito ou elemento do plano que conflite com um princípio MUST.
- Seções obrigatórias ou verificações de qualidade da constituição ausentes.

#### E. Lacunas de Cobertura

- Requisitos sem tarefas associadas.
- Tarefas sem requisito/história mapeados.
- Critérios de sucesso com trabalho implementável (desempenho, segurança, disponibilidade) não refletidos nas tarefas.

#### F. Inconsistências

- Variação de terminologia (o mesmo conceito com nomes diferentes entre arquivos).
- Entidades de dados referenciadas no plano e ausentes na spec, ou vice-versa.
- Contradições na ordem das tarefas (por exemplo, integração antes da preparação fundamental, sem nota de dependência).
- Requisitos conflitantes (por exemplo, um exige Next.js e outro especifica Vue).

### 5. Atribuir Severidade

Use a seguinte heurística para priorizar os achados:

- **CRITICAL**: Violação de MUST da constituição, artefato central da spec ausente ou requisito sem cobertura que bloqueie a funcionalidade básica.
- **HIGH**: Requisito duplicado ou conflitante, atributo ambíguo de segurança/desempenho ou critério de aceitação não testável.
- **MEDIUM**: Variação de terminologia, cobertura ausente de tarefas não funcionais ou caso de borda insuficientemente especificado.
- **LOW**: Melhoria de estilo/redação ou redundância pequena sem efeito na ordem de execução.

### 6. Produzir um Relatório Conciso de Análise

Apresente um relatório Markdown (sem escrever arquivos) com esta estrutura:

## Relatório de Análise da Especificação

| ID | Categoria | Severidade | Localização | Resumo | Recomendação |
|----|-----------|------------|-------------|--------|--------------|
| A1 | Duplicação | HIGH | spec.md:L120-134 | Dois requisitos semelhantes ... | Consolidar a redação; manter a versão mais clara |

(Adicione uma linha por achado; gere IDs estáveis com prefixo correspondente à inicial da categoria.)

**Tabela de Resumo da Cobertura:**

| Chave do Requisito | Possui Tarefa? | IDs das Tarefas | Notas |
|-------------------|----------------|----------------|-------|

**Problemas de Alinhamento com a Constituição:** (se houver)

**Tarefas Não Mapeadas:** (se houver)

**Métricas:**

- Total de requisitos
- Total de tarefas
- Cobertura % (requisitos com >=1 tarefa)
- Quantidade de ambiguidades
- Quantidade de duplicações
- Quantidade de problemas críticos

### 7. Indicar Próximas Ações

Ao final do relatório, apresente um bloco conciso de próximas ações:

- Se houver problemas CRITICAL, recomende resolvê-los antes de `/speckit-implement`.
- Se houver somente LOW/MEDIUM, o usuário pode prosseguir, mas apresente sugestões de melhoria.
- Sugira comandos explicitamente: por exemplo, "Execute /speckit-specify para refinar", "Execute /speckit-plan para ajustar a arquitetura", "Edite tasks.md manualmente para cobrir 'performance-metrics'".

### 8. Oferecer Correções

Pergunte: "Deseja que eu sugira edições concretas para corrigir os N problemas mais importantes?" (NÃO aplique automaticamente.)

### 9. Verificar Hooks de Extensões

Após apresentar o relatório, verifique se `.specify/extensions.yml` existe na raiz do projeto.
- Se existir, leia-o e procure entradas na chave `hooks.after_analyze`.
- Se o YAML não puder ser interpretado ou for inválido, não ignore silenciosamente: informe que `.specify/extensions.yml` não pôde ser lido (inclua o erro do parser) e que nenhum hook foi verificado, inclusive hooks obrigatórios (`optional: false`); depois, continue normalmente.
- Exclua os hooks cujo `enabled` seja explicitamente `false`. Considere habilitados por padrão aqueles sem o campo `enabled`.
- Para cada hook restante, **não** tente interpretar ou avaliar expressões `condition`:
  - Se não houver `condition`, ou se ela for nula/vazia, considere o hook executável.
  - Se houver `condition` não vazia, ignore o hook e deixe a avaliação da condição para a implementação de HookExecutor.
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
  - Se não houver hooks registrados ou `.specify/extensions.yml` não existir, prossiga sem anunciar essa ausência.

## Princípios Operacionais

### Eficiência de Contexto

- **Contexto mínimo e relevante**: Priorize achados acionáveis, não documentação exaustiva.
- **Carregamento progressivo**: Carregue os artefatos incrementalmente; não despeje todo o conteúdo na análise.
- **Saída eficiente**: Limite a tabela a 50 linhas e resuma os demais achados.
- **Resultados determinísticos**: Uma nova execução sem mudanças deve produzir IDs e contagens consistentes.

### Diretrizes de Análise

- **NUNCA modifique arquivos** (esta análise é somente leitura).
- **NUNCA invente seções ausentes** (informe corretamente sua ausência).
- **Priorize violações da constituição** (sempre CRITICAL).
- **Prefira exemplos a regras exaustivas** (cite ocorrências específicas, não padrões genéricos).
- **Informe adequadamente a ausência de problemas** (apresente um relatório de sucesso com estatísticas de cobertura).

## Contexto

$ARGUMENTS
