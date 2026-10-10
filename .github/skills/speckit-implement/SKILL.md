---
name: "speckit-implement"
description: "Executar o plano de implementação processando e realizando todas as tarefas definidas em tasks.md."
compatibility: "Requer a estrutura de projeto do spec-kit com o diretório .specify/"
metadata:
  author: "github-spec-kit"
  source: "templates/commands/implement.md"
---


## Entrada do Usuário

```text
$ARGUMENTS
```

Você **DEVE** considerar a entrada do usuário antes de prosseguir (se não estiver vazia).

## Verificações Antes da Execução

**Verifique os hooks de extensões (antes da implementação)**:
- Verifique se `.specify/extensions.yml` existe na raiz do projeto.
- Se existir, leia-o e procure entradas na chave `hooks.before_implement`.
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

1. Execute `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` na raiz e interprete FEATURE_DIR e a lista AVAILABLE_DOCS. Todos os caminhos devem ser absolutos. Para aspas simples em argumentos como "I'm Groot", use escape: por exemplo, 'I'\''m Groot' (ou aspas duplas, se possível: "I'm Groot").

2. **Verifique as checklists**, se FEATURE_DIR/checklists/ existir:
   - Trate os marcadores como verificação somente leitura: examine o estado, informe-o e pergunte antes de prosseguir quando necessário; NÃO modifique arquivos ou marcadores.
   - `checklists/requirements.md` é a checklist de qualidade mantida por `/speckit-specify` e `/speckit-clarify`; checklists personalizadas de `/speckit-checklist` são artefatos de revisão de requisitos sob responsabilidade do revisor.
   - Nas personalizadas, `[x]` significa que o revisor considerou satisfeito o critério de qualidade dos requisitos; NÃO significa implementação concluída.
   - Examine todos os arquivos em checklists/.
   - Para cada checklist, conte:
     - Total: linhas com `- [ ]`, `- [X]` ou `- [x]`.
     - Marcados: linhas com `- [X]` ou `- [x]`.
     - Desmarcados: linhas com `- [ ]`.
   - Crie uma tabela de estado:

     ```text
    | Checklist | Total | Marcados | Desmarcados | Estado |
     |-----------|-------|---------|-----------|--------|
     | ux.md     | 12    | 12      | 0         | ✓ PASS |
     | test.md   | 8     | 5       | 3         | ✗ FAIL |
     | security.md | 6   | 6       | 0         | ✓ PASS |
     ```

   - Calcule o estado geral:
     - **PASS**: Todas têm zero itens desmarcados.
     - **FAIL**: Pelo menos uma tem itens desmarcados.

   - **Se houver itens desmarcados**:
     - Mostre a tabela com as contagens.
     - **PARE** e pergunte: "Há itens desmarcados nas checklists. Deseja implementar mesmo assim? (sim/não)".
     - Aguarde a resposta antes de continuar.
     - Se o usuário disser "não", "aguarde" ou "pare", interrompa.
     - Se disser "sim", "prossiga" ou "continue", avance para a etapa 3.

   - **Se todas estiverem marcadas**:
     - Mostre a tabela com aprovação de todas.
     - Avance automaticamente para a etapa 3.

3. Carregue e analise o contexto:
  - **OBRIGATÓRIO**: Leia tasks.md para a lista completa e o plano de execução.
  - **OBRIGATÓRIO**: Leia plan.md para stack, arquitetura e estrutura.
  - **SE EXISTIR**: Leia data-model.md para entidades e relacionamentos.
  - **SE EXISTIR**: Leia contracts/ para specs de API e requisitos de testes.
  - **SE EXISTIR**: Leia research.md para decisões e restrições técnicas.
  - **SE EXISTIR**: Leia .specify/memory/constitution.md para governança.
  - **SE EXISTIR**: Leia quickstart.md para cenários de integração.

4. **Verifique a configuração do projeto**:
  - **OBRIGATÓRIO**: Crie/verifique arquivos de exclusão conforme a configuração real:

  **Lógica de detecção e criação**:
  - Verifique o sucesso do comando abaixo para identificar um repositório Git; nesse caso, crie/verifique .gitignore:

     ```sh
     git rev-parse --git-dir 2>/dev/null
     ```

  - Dockerfile* existente ou Docker em plan.md → crie/verifique .dockerignore.
  - .eslintrc* existente → crie/verifique .eslintignore.
  - eslint.config.* existente → garanta que `ignores` cubra os padrões necessários.
  - .prettierrc* existente → crie/verifique .prettierignore.
  - .npmrc ou package.json existente → crie/verifique .npmignore, se houver publicação.
  - Arquivos Terraform (*.tf) existentes → crie/verifique .terraformignore.
  - Charts Helm presentes → crie/verifique .helmignore, se necessário.

  **Se já existir**: Confira os padrões essenciais e acrescente somente os críticos ausentes.
  **Se estiver ausente**: Crie com o conjunto completo para a tecnologia detectada.

  **Padrões comuns por tecnologia**, conforme a stack de plan.md:
   - **Node.js/JavaScript/TypeScript**: `node_modules/`, `dist/`, `build/`, `*.log`, `.env*`
   - **Python**: `__pycache__/`, `*.pyc`, `.venv/`, `venv/`, `dist/`, `*.egg-info/`
   - **Java**: `target/`, `*.class`, `*.jar`, `.gradle/`, `build/`
   - **C#/.NET**: `bin/`, `obj/`, `*.user`, `*.suo`, `packages/`
   - **Go**: `*.exe`, `*.test`, `vendor/`, `*.out`
   - **Ruby**: `.bundle/`, `log/`, `tmp/`, `*.gem`, `vendor/bundle/`
   - **PHP**: `vendor/`, `*.log`, `*.cache`, `*.env`
   - **Rust**: `target/`, `debug/`, `release/`, `*.rs.bk`, `*.rlib`, `*.prof*`, `.idea/`, `*.log`, `.env*`
   - **Kotlin**: `build/`, `out/`, `.gradle/`, `.idea/`, `*.class`, `*.jar`, `*.iml`, `*.log`, `.env*`
   - **C++**: `build/`, `bin/`, `obj/`, `out/`, `*.o`, `*.so`, `*.a`, `*.exe`, `*.dll`, `.idea/`, `*.log`, `.env*`
   - **C**: `build/`, `bin/`, `obj/`, `out/`, `*.o`, `*.a`, `*.so`, `*.exe`, `*.dll`, `autom4te.cache/`, `config.status`, `config.log`, `.idea/`, `*.log`, `.env*`
   - **Swift**: `.build/`, `DerivedData/`, `*.swiftpm/`, `Packages/`
   - **R**: `.Rproj.user/`, `.Rhistory`, `.RData`, `.Ruserdata`, `*.Rproj`, `packrat/`, `renv/`
   - **Universal**: `.DS_Store`, `Thumbs.db`, `*.tmp`, `*.swp`, `.vscode/`, `.idea/`

  **Padrões específicos de ferramentas**:
   - **Docker**: `node_modules/`, `.git/`, `Dockerfile*`, `.dockerignore`, `*.log*`, `.env*`, `coverage/`
   - **ESLint**: `node_modules/`, `dist/`, `build/`, `coverage/`, `*.min.js`
   - **Prettier**: `node_modules/`, `dist/`, `build/`, `coverage/`, `package-lock.json`, `yarn.lock`, `pnpm-lock.yaml`
   - **Terraform**: `.terraform/`, `*.tfstate*`, `*.tfvars`, `.terraform.lock.hcl`
   - **Kubernetes/k8s**: `*.secret.yaml`, `secrets/`, `.kube/`, `kubeconfig*`, `*.key`, `*.crt`

5. Interprete tasks.md e extraia:
  - **Fases**: Preparação, testes, núcleo, integração e refinamento.
  - **Dependências**: Regras de execução sequencial e paralela.
  - **Detalhes**: ID, descrição, caminhos e marcadores [P].
  - **Fluxo**: Ordem e requisitos de dependência.

6. Implemente conforme o plano de tarefas:
  - **Por fase**: Conclua uma antes de avançar.
  - **Respeite dependências**: Execute tarefas sequenciais em ordem; [P] podem executar juntas.
  - **Siga TDD**: Execute tarefas de testes antes da implementação correspondente.
  - **Coordene por arquivo**: Tarefas que afetam os mesmos arquivos devem executar sequencialmente.
  - **Valide as etapas**: Confira a conclusão de cada fase antes de prosseguir.

7. Regras de implementação:
  - **Preparação primeiro**: Estrutura, dependências e configuração.
  - **Testes antes do código**: Quando precisar testar contratos, entidades e integração.
  - **Desenvolvimento central**: Modelos, serviços, comandos CLI e endpoints.
  - **Integração**: Conexões de banco, middleware, logs e serviços externos.
  - **Refinamento e validação**: Testes unitários, otimização e documentação.

8. Acompanhamento e erros:
  - Informe o progresso após cada tarefa concluída.
  - Interrompa se uma tarefa não paralela falhar.
  - Para [P], prossiga com as bem-sucedidas e informe as falhas.
  - Forneça erros claros com contexto para depuração.
  - Sugira próximos passos se não puder prosseguir.
  - **IMPORTANTE**: Marque tarefas concluídas como [X] no arquivo de tarefas.

9. Validação de conclusão:
  - Confira todas as tarefas obrigatórias.
  - Verifique aderência à especificação original.
  - Valide aprovação dos testes e cobertura exigida.
  - Confirme aderência ao plano técnico.

Nota: O comando pressupõe decomposição completa em tasks.md. Se faltar ou estiver incompleta, sugira `/speckit-tasks` primeiro para regenerar a lista.

## Hooks Obrigatórios Após a Execução

**Você DEVE concluir esta seção antes de informar a conclusão ao usuário.**

Verifique se `.specify/extensions.yml` existe na raiz do projeto.
- Se não existir ou não houver hooks registrados na chave `hooks.after_implement`, prossiga para o Relatório de Conclusão.
- Se existir, leia-o e procure entradas na chave `hooks.after_implement`.
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

Informe o estado final com resumo do trabalho concluído.

## Critérios de Conclusão

- [ ] Todas as tarefas de tasks.md concluídas e marcadas `[X]`.
- [ ] Implementação validada contra spec, plano e cobertura de testes.
- [ ] Hooks de extensões acionados ou ignorados conforme as regras de Hooks Obrigatórios Após a Execução acima.
- [ ] Conclusão informada com resumo do trabalho realizado.
