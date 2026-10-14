---
name: "speckit-plan"
description: "Executar o fluxo de planejamento da implementação usando o template de plano para gerar artefatos de desenho."
compatibility: "Requer a estrutura de projeto do spec-kit com o diretório .specify/"
metadata:
  author: "github-spec-kit"
  source: "templates/commands/plan.md"
---


## Entrada do Usuário

```text
$ARGUMENTS
```

Você **DEVE** considerar a entrada do usuário antes de prosseguir (se não estiver vazia).

## Verificações Antes da Execução

**Verifique os hooks de extensões (antes do planejamento)**:
- Verifique se `.specify/extensions.yml` existe na raiz do projeto.
- Se existir, leia-o e procure entradas na chave `hooks.before_plan`.
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

1. **Preparação**: Execute `.specify/scripts/powershell/setup-plan.ps1 -Json` na raiz do repositório e interprete o JSON para obter FEATURE_SPEC, IMPL_PLAN, FEATURE_DIR e BRANCH. Para aspas simples em argumentos como "I'm Groot", use escape: por exemplo, 'I'\''m Groot' (ou aspas duplas, se possível: "I'm Groot").

2. **Carregar contexto**: Leia FEATURE_SPEC e `.specify/memory/constitution.md`. Carregue o template IMPL_PLAN (já copiado).

3. **Executar o planejamento**: Siga a estrutura do template IMPL_PLAN para:
  - Preencher o contexto técnico (marque dúvidas como "NEEDS CLARIFICATION").
  - Preencher a verificação da constituição com base nos seus princípios.
  - Avaliar as verificações obrigatórias (ERROR se houver violações sem justificativa).
  - Fase 0: Gerar research.md (resolver todos os NEEDS CLARIFICATION).
  - Fase 1: Gerar data-model.md, contracts/ e quickstart.md.
  - Reavaliar a verificação da constituição após o desenho.

## Hooks Obrigatórios Após a Execução

**Você DEVE concluir esta seção antes de informar a conclusão ao usuário.**

Verifique se `.specify/extensions.yml` existe na raiz do projeto.
- Se não existir ou não houver hooks registrados na chave `hooks.after_plan`, prossiga para o Relatório de Conclusão.
- Se existir, leia-o e procure entradas na chave `hooks.after_plan`.
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

O comando termina após o desenho da fase 1. Informe a branch, o caminho IMPL_PLAN e os artefatos gerados.

## Fases

### Fase 0: Roteiro e Pesquisa

1. **Extraia as dúvidas do contexto técnico** acima:
  - Para cada NEEDS CLARIFICATION → tarefa de pesquisa.
  - Para cada dependência → tarefa de boas práticas.
  - Para cada integração → tarefa de padrões.

2. **Gere e acione agentes de pesquisa**:

   ```text
   Para cada dúvida do contexto técnico:
     Tarefa: "Pesquise {unknown} para {feature context}"
   Para cada escolha tecnológica:
     Tarefa: "Encontre boas práticas para {tech} em {domain}"
   ```

3. **Consolide os achados** em `research.md` usando o formato:
  - Decisão: [o que foi escolhido]
  - Justificativa: [por que foi escolhido]
  - Alternativas consideradas: [o que mais foi avaliado]

**Saída**: research.md com todos os NEEDS CLARIFICATION resolvidos.

### Fase 1: Desenho e Contratos

**Pré-requisitos:** `research.md` completo.

1. **Extraia as entidades da spec da feature** → `data-model.md`:
  - Nome da entidade, campos e relacionamentos.
  - Regras de validação dos requisitos.
  - Transições de estado, quando aplicáveis.

2. **Defina contratos de interfaces** (se houver interfaces externas no projeto) → `/contracts/`:
  - Identifique as interfaces que o projeto expõe aos usuários ou a outros sistemas.
  - Documente o formato de contrato adequado ao tipo de projeto.
  - Exemplos: APIs públicas de bibliotecas, schemas de comandos CLI, endpoints de serviços web, gramáticas de parsers e contratos de UI.
  - Ignore se o projeto for exclusivamente interno (scripts de build, ferramentas pontuais etc.).

3. **Crie um guia de validação inicial** → `quickstart.md`:
  - Documente cenários executáveis que comprovem o funcionamento ponta a ponta da feature.
  - Inclua pré-requisitos, comandos de preparação, teste/execução e resultados esperados.
  - Use links ou referências a contratos e detalhes do modelo de dados em vez de duplicá-los.
  - Não inclua implementação completa, corpos de modelos/serviços/controllers, migrations ou suítes completas de testes.
  - Mantenha este artefato como guia de validação/execução; detalhes de implementação pertencem a `tasks.md` e à fase de implementação.

**Saída**: data-model.md, /contracts/* e quickstart.md.

## Regras Principais

- Use caminhos absolutos para operações no sistema de arquivos e caminhos relativos ao projeto nas referências da documentação.
- Informe ERROR quando uma verificação obrigatória falhar ou houver esclarecimentos não resolvidos.

## Critérios de Conclusão

- [ ] Fluxo de planejamento executado e artefatos de desenho gerados.
- [ ] Hooks de extensões acionados ou ignorados conforme as regras de Hooks Obrigatórios Após a Execução acima.
- [ ] Conclusão informada ao usuário com branch, caminho do plano e artefatos gerados.
