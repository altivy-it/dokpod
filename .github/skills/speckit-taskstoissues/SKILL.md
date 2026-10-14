---
name: "speckit-taskstoissues"
description: "Converter tarefas existentes da feature em issues executáveis do GitHub, ordenadas por dependências e baseadas nos artefatos de desenho disponíveis."
compatibility: "Requer a estrutura de projeto do spec-kit com o diretório .specify/"
metadata:
  author: "github-spec-kit"
  source: "templates/commands/taskstoissues.md"
---


## Entrada do Usuário

```text
$ARGUMENTS
```

Você **DEVE** considerar a entrada do usuário antes de prosseguir (se não estiver vazia).

## Verificações Antes da Execução

**Verifique os hooks de extensões (antes de converter tarefas em issues)**:
- Verifique se `.specify/extensions.yml` existe na raiz do projeto.
- Se existir, leia-o e procure entradas na chave `hooks.before_taskstoissues`.
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

1. Execute `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` na raiz do repositório e interprete FEATURE_DIR e a lista AVAILABLE_DOCS. Todos os caminhos devem ser absolutos. Para aspas simples em argumentos como "I'm Groot", use escape: por exemplo, 'I'\''m Groot' (ou aspas duplas, se possível: "I'm Groot").
1. **SE EXISTIR**: Carregue `.specify/memory/constitution.md` para consultar princípios e restrições de governança.
1. Extraia do script executado o caminho de **tasks**.
1. Obtenha o remote Git executando:

```bash
git config --get remote.origin.url
```

> [!CAUTION]
> PROSSIGA PARA AS PRÓXIMAS ETAPAS SOMENTE SE O REMOTE FOR UMA URL DO GITHUB.

1. **Busque issues existentes para deduplicar**: Antes de criar qualquer issue, reúna os IDs das tarefas a processar em `tasks.md` (cada um é um `T` seguido de **pelo menos** três dígitos, como `T001`; `/speckit-converge` atribui IDs com `T{M+1:03d}`, que define um mínimo, não um máximo, portanto arquivos com mais de 999 tarefas usam quatro ou mais dígitos). Use a ferramenta `list_issues` do servidor GitHub MCP para buscar issues que já cubram esses IDs. Não passe `state`: sua ausência retorna issues abertas e fechadas. Solicite `perPage: 100` para reduzir chamadas; a paginação usa cursores, portanto solicite páginas com `after`, usando o `endCursor` da resposta anterior. Compare cada título com o padrão `\bT\d{3,}\b` (`{3,}` aceita quatro ou mais dígitos; com `\d{3}`, um título contendo `T1000` não corresponderia, pois o `\b` final não pode estar entre dígitos, e a tarefa não seria deduplicada nem criada. Os limites de palavra impedem correspondência com `ST001` e exigem consumir todos os dígitos, impedindo que `T100` corresponda dentro de `T1000`; o padrão também reconhece `T001 ...`, `T001: ...` e `[T001] ...`). Se corresponder a um ID da lista, marque-o como já associado a uma issue. Interrompa a paginação quando todos os IDs tiverem sido encontrados ou não houver mais páginas, sem buscar todo o histórico desnecessariamente. Isso limita chamadas em repositórios grandes e evita duplicações quando o comando é repetido após regenerar `tasks.md` ou reinvocar a skill.
1. Para cada tarefa, use o servidor GitHub MCP para criar uma issue no repositório correspondente ao remote Git. As linhas em `tasks.md` começam com checkbox Markdown: remova primeiro `- [ ]` e os marcadores `[P]` / `[US#]` para recuperar ID e descrição. Use um único título canônico no formato `T001: <description>`, com o ID apenas uma vez, seguido da descrição (por exemplo, `- [ ] T001 Criar estrutura do projeto` torna-se `T001: Criar estrutura do projeto`).
  - **Ignore** tarefas cujo ID já esteja nas issues existentes da etapa anterior e informe isso (por exemplo, `T001 já possui uma issue; ignorando`).
  - Crie issues somente para tarefas sem uma issue correspondente.

> [!CAUTION]
> NUNCA CRIE ISSUES EM REPOSITÓRIOS QUE NÃO CORRESPONDAM À URL DO REMOTE.

## Verificações Após a Execução

**Verifique os hooks de extensões (após converter tarefas em issues)**:
Verifique se `.specify/extensions.yml` existe na raiz do projeto.
- Se existir, leia-o e procure entradas na chave `hooks.after_taskstoissues`.
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
