---
name: "speckit-tasks"
description: "Gerar tasks.md executável para a feature, ordenado por dependências e baseado nos artefatos de desenho disponíveis."
compatibility: "Requer a estrutura de projeto do spec-kit com o diretório .specify/"
metadata:
  author: "github-spec-kit"
  source: "templates/commands/tasks.md"
---


## Entrada do Usuário

```text
$ARGUMENTS
```

Você **DEVE** considerar a entrada do usuário antes de prosseguir (se não estiver vazia).

## Verificações Antes da Execução

**Verifique os hooks de extensões (antes de gerar tarefas)**:
- Verifique se `.specify/extensions.yml` existe na raiz do projeto.
- Se existir, leia-o e procure entradas na chave `hooks.before_tasks`.
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

    Aguarde o resultado do hook antes de prosseguir para o Roteiro.
    ```
    Após apresentar o bloco, você DEVE invocar o hook e aguardar sua conclusão. Execute-o como executaria o comando nesta sessão (a invocação pode diferir do identificador literal `{command}`; por exemplo, um agente em modo skills usa `/skill:speckit-...` ou `$speckit-...`). Apresentar apenas o bloco não executa o hook.
- Se não houver hooks registrados ou `.specify/extensions.yml` não existir, prossiga sem anunciar essa ausência

## Roteiro

1. **Preparação**: Execute `.specify/scripts/powershell/setup-tasks.ps1 -Json` na raiz do repositório e interprete FEATURE_DIR, TASKS_TEMPLATE_CONTENT, TASKS_TEMPLATE e a lista AVAILABLE_DOCS. `FEATURE_DIR` e `TASKS_TEMPLATE` devem ser caminhos absolutos quando fornecidos. `AVAILABLE_DOCS` lista nomes/caminhos relativos disponíveis em `FEATURE_DIR` (por exemplo, `research.md` ou `contracts/`). Para aspas simples em argumentos como "I'm Groot", use escape: por exemplo, 'I'\''m Groot' (ou aspas duplas, se possível: "I'm Groot").

2. **Carregar documentos de desenho**: Leia de FEATURE_DIR:
  - **Obrigatórios**: plan.md (stack, bibliotecas, estrutura) e spec.md (histórias de usuário com prioridades).
  - **Opcionais**: data-model.md (entidades), contracts/ (contratos de interfaces), research.md (decisões) e quickstart.md (cenários de teste).
  - **SE EXISTIR**: Carregue `.specify/memory/constitution.md` para consultar princípios e restrições de governança.
  - Nota: Nem todos os projetos possuem todos os documentos. Gere tarefas conforme os disponíveis.

3. **Executar a geração de tarefas**:
  - Carregue plan.md e extraia stack, bibliotecas e estrutura do projeto.
  - Carregue spec.md e extraia histórias com prioridades (P1, P2, P3 etc.).
  - Se data-model.md existir, extraia entidades e relacione-as às histórias.
  - Se contracts/ existir, relacione contratos de interfaces às histórias.
  - Se research.md existir, extraia decisões para tarefas de preparação.
  - Gere tarefas organizadas por história (consulte as Regras de Geração de Tarefas abaixo).
  - Gere um grafo de dependências com a ordem de conclusão das histórias.
  - Crie exemplos de execução paralela por história.
  - Valide a completude (cada história possui todas as tarefas necessárias e é testável independentemente).

4. **Gerar tasks.md**: Use TASKS_TEMPLATE_CONTENT (do JSON acima) como estrutura. Para compatibilidade com scripts antigos que omitem TASKS_TEMPLATE_CONTENT, leia TASKS_TEMPLATE. Preencha com:
  - Nome correto da feature de plan.md.
  - Fase 1: Preparação (inicialização do projeto).
  - Fase 2: Fundação (pré-requisitos bloqueantes para todas as histórias).
  - Fase 3+: Uma fase por história, na ordem de prioridade de spec.md.
  - Cada fase inclui objetivo, critérios de teste independente, testes (se solicitados) e tarefas de implementação.
  - Fase final: Refinamento e aspectos transversais.
  - Todas as tarefas seguem estritamente o formato de checklist (consulte as regras abaixo).
  - Caminhos claros para os arquivos de cada tarefa.
  - Seção de dependências com a ordem de conclusão das histórias.
  - Exemplos de execução paralela por história.
  - Estratégia de implementação (MVP primeiro, entrega incremental).

## Hooks Obrigatórios Após a Execução

**Você DEVE concluir esta seção antes de informar a conclusão ao usuário.**

Verifique se `.specify/extensions.yml` existe na raiz do projeto.
- Se não existir ou não houver hooks registrados na chave `hooks.after_tasks`, prossiga para o Relatório de Conclusão.
- Se existir, leia-o e procure entradas na chave `hooks.after_tasks`.
- Se o YAML não puder ser interpretado ou for inválido, não ignore silenciosamente: informe que `.specify/extensions.yml` não pôde ser lido (inclua o erro do parser) e que nenhum hook foi verificado, inclusive hooks obrigatórios (`optional: false`); depois, prossiga para o Relatório de Conclusão.
- Exclua os hooks cujo `enabled` seja explicitamente `false`. Considere habilitados por padrão aqueles sem o campo `enabled`.
- Para cada hook restante, **não** tente interpretar ou avaliar expressões `condition`:
  - Se não houver `condition`, ou se ela for nula/vazia, considere o hook executável
  - Se houver `condition` não vazia, ignore o hook e deixe a avaliação da condição para a implementação de HookExecutor
- Ao construir invocações a partir dos nomes dos comandos de hooks, substitua pontos (`.`) por hífens (`-`). Por exemplo, `speckit.git.commit` → `/speckit-git-commit`.
- Para cada hook executável, apresente o seguinte conforme seu campo `optional`:
  - **Hook obrigatório** (`optional: false`) — **Você DEVE apresentar `EXECUTE_COMMAND:` para cada hook obrigatório**:
    ```
    ## Hooks de Extensões

    **Hook Automático**: {extension}
    Executando: `/{command}`
    EXECUTE_COMMAND: {command}
    ```
    Após apresentar o bloco, você DEVE invocar o hook e aguardar sua conclusão. Execute-o como executaria o comando nesta sessão (a invocação pode diferir do identificador literal `{command}`; por exemplo, um agente em modo skills usa `/skill:speckit-...` ou `$speckit-...`). Apresentar apenas o bloco não executa o hook.
  - **Hook opcional** (`optional: true`):
    ```
    ## Hooks de Extensões

    **Hook Opcional**: {extension}
    Comando: `/{command}`
    Descrição: {description}

    Solicitação: {prompt}
    Para executar: `/{command}`
    ```

## Relatório de Conclusão

Informe o caminho de tasks.md gerado e um resumo:
- Total de tarefas.
- Quantidade de tarefas por história.
- Oportunidades de paralelismo identificadas.
- Critérios de teste independente de cada história.
- Escopo de MVP sugerido (normalmente apenas a história 1).
- Validação de formato: confirme que TODAS as tarefas seguem o formato de checklist (checkbox, ID, rótulos e caminhos).

Contexto para geração das tarefas: $ARGUMENTS

tasks.md deve ser imediatamente executável: cada tarefa precisa ser específica o suficiente para que um LLM a conclua sem contexto adicional.

## Regras de Geração de Tarefas

**CRITICAL**: As tarefas DEVEM ser organizadas por história de usuário para permitir implementação e testes independentes.

**Testes são OPCIONAIS**: Gere tarefas de teste somente se forem solicitadas explicitamente na spec ou se o usuário pedir TDD.

### Formato de Checklist (OBRIGATÓRIO)

Cada tarefa DEVE seguir estritamente este formato:

```text
- [ ] [TaskID] [P?] [Story?] Descrição com caminho do arquivo
```

**Componentes do formato**:

1. **Checkbox**: SEMPRE comece com `- [ ]` (checkbox Markdown).
2. **ID da tarefa**: Número sequencial (T001, T002, T003...) na ordem de execução.
3. **Marcador [P]**: Inclua SOMENTE se a tarefa permitir paralelismo (arquivos diferentes, sem dependência de tarefas incompletas).
4. **Rótulo [Story]**: OBRIGATÓRIO somente nas tarefas das fases de histórias.
  - Formato: [US1], [US2], [US3] etc. (corresponde às histórias de spec.md).
  - Preparação: SEM rótulo de história.
  - Fundação: SEM rótulo de história.
  - Fases de histórias: DEVEM ter rótulo de história.
  - Refinamento: SEM rótulo de história.
5. **Descrição**: Ação clara com caminho exato do arquivo.

**Exemplos**:

- ✅ CORRETO: `- [ ] T001 Criar estrutura do projeto conforme o plano de implementação`
- ✅ CORRETO: `- [ ] T005 [P] Implementar middleware de autenticação em src/middleware/auth.py`
- ✅ CORRETO: `- [ ] T012 [P] [US1] Criar modelo User em src/models/user.py`
- ✅ CORRETO: `- [ ] T014 [US1] Implementar UserService em src/services/user_service.py`
- ❌ INCORRETO: `- [ ] Criar modelo User` (sem ID nem rótulo de história).
- ❌ INCORRETO: `T001 [US1] Criar modelo` (sem checkbox).
- ❌ INCORRETO: `- [ ] [US1] Criar modelo User` (sem ID da tarefa).
- ❌ INCORRETO: `- [ ] T001 [US1] Criar modelo` (sem caminho de arquivo).

### Organização das Tarefas

1. **A partir das histórias (spec.md)** - ORGANIZAÇÃO PRINCIPAL:
   - Cada história (P1, P2, P3...) possui sua própria fase.
   - Relacione todos os componentes à respectiva história:
     - Modelos necessários.
     - Serviços necessários.
     - Interfaces/UI necessárias.
     - Se houver solicitação de testes: testes específicos da história.
   - Marque dependências entre histórias (a maioria deve ser independente).

2. **A partir dos contratos**:
  - Relacione cada contrato de interface → à história atendida.
  - Se houver solicitação de testes: cada contrato → tarefa de teste de contrato [P] antes da implementação na fase da história.

3. **A partir do modelo de dados**:
  - Relacione cada entidade às histórias que precisam dela.
  - Se atender várias histórias, coloque-a na primeira ou na fase de preparação.
  - Relacionamentos → tarefas da camada de serviços na fase adequada.
  - Para cada campo com restrições em data-model.md (tamanho máximo, nullable/obrigatório, valores de enum e validação), cite a restrição literalmente na descrição da tarefa, sem deixar a decisão para a implementação.

4. **A partir da preparação/infraestrutura**:
  - Infraestrutura compartilhada → preparação (fase 1).
  - Tarefas fundamentais/bloqueantes → fundação (fase 2).
  - Preparação específica de uma história → dentro de sua fase.

### Estrutura das Fases

- **Fase 1**: Preparação (inicialização do projeto).
- **Fase 2**: Fundação (pré-requisitos bloqueantes; DEVEM estar completos antes das histórias).
- **Fase 3+**: Histórias na ordem de prioridade (P1, P2, P3...).
  - Em cada história: Testes (se solicitados) → Modelos → Serviços → Endpoints → Integração.
  - Cada fase deve ser um incremento completo e testável independentemente.
- **Fase final**: Refinamento e aspectos transversais.

## Critérios de Conclusão

- [ ] tasks.md gerado com todas as fases, IDs de tarefas e caminhos de arquivos.
- [ ] Hooks de extensões acionados ou ignorados conforme as regras de Hooks Obrigatórios Após a Execução acima.
- [ ] Conclusão informada ao usuário com quantidade de tarefas, distribuição por história e escopo de MVP.
