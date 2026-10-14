---
name: "speckit-specify"
description: "Criar ou atualizar a especificação de uma feature a partir de sua descrição em linguagem natural."
compatibility: "Requer a estrutura de projeto do spec-kit com o diretório .specify/"
metadata:
  author: "github-spec-kit"
  source: "templates/commands/specify.md"
---


## Entrada do Usuário

```text
$ARGUMENTS
```

Você **DEVE** considerar a entrada do usuário antes de prosseguir (se não estiver vazia).

## Verificações Antes da Execução

**Verifique os hooks de extensões (antes da especificação)**:
- Verifique se `.specify/extensions.yml` existe na raiz do projeto.
- Se existir, leia-o e procure entradas na chave `hooks.before_specify`.
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

O texto digitado após `/speckit-specify` na mensagem que acionou o comando **é** a descrição da feature. Considere-o disponível na conversa mesmo que `$ARGUMENTS` apareça literalmente abaixo. Não peça que o usuário o repita, salvo se o comando estiver vazio.

Com essa descrição, faça o seguinte:

1. **Gere um nome curto e conciso** (2 a 4 palavras) para a feature:
    - Analise a descrição e extraia as palavras-chave mais relevantes.
    - Crie um nome de 2 a 4 palavras que represente a essência da feature.
    - Use o formato ação-substantivo quando possível (como "add-user-auth" e "fix-payment-bug").
    - Preserve termos técnicos e siglas (OAuth2, API, JWT etc.).
    - Seja conciso, mas descritivo o suficiente para entendimento imediato.
    - Exemplos:
       - "Quero adicionar autenticação de usuário" → "user-auth"
       - "Implementar integração OAuth2 para a API" → "oauth2-api-integration"
       - "Criar um dashboard analítico" → "analytics-dashboard"
       - "Corrigir timeout no processamento de pagamentos" → "fix-payment-timeout"

2. **Criação de branch** (opcional, via hook):

   Se um hook `before_specify` executou com sucesso nas verificações anteriores, ele criou/selecionou uma branch Git e retornou JSON com `BRANCH_NAME` e `FEATURE_NUM`. Registre esses valores, mas o nome da branch **não** determina o nome do diretório da spec.

   Se o usuário forneceu `GIT_BRANCH_NAME` explicitamente, repasse-o ao hook para usar exatamente esse nome, sem gerar prefixos ou sufixos.

3. **Crie o diretório da feature**:

   As specs ficam em `specs/` por padrão, salvo se o usuário fornecer `SPECIFY_FEATURE_DIRECTORY` explicitamente.

   **Ordem de resolução de `SPECIFY_FEATURE_DIRECTORY`**:
   1. Se o usuário forneceu `SPECIFY_FEATURE_DIRECTORY` por variável de ambiente, argumento ou configuração, use-o como está.
   2. Caso contrário, gere-o automaticamente em `specs/`:
      - Consulte `.specify/init-options.json` para `feature_numbering` (preferencial) ou `branch_numbering` (obsoleto, apenas para migração; será removido futuramente).
      - Se `"timestamp"`: use o prefixo `YYYYMMDD-HHMMSS` (timestamp atual).
      - Se `"sequential"` ou ausente: use `NNN` (próximo número disponível de três dígitos após examinar os diretórios de `specs/`).
      - Monte o nome: `<prefix>-<short-name>` (por exemplo, `003-user-auth` ou `20260319-143022-user-auth`).
      - Defina `SPECIFY_FEATURE_DIRECTORY` como `specs/<directory-name>`.
      - Se usar `branch_numbering` sem `feature_numbering`, avise em uma linha: "⚠️ `branch_numbering` em init-options.json está obsoleto. Renomeie para `feature_numbering`."

   **Crie o diretório e o arquivo da spec**:
   - `mkdir -p SPECIFY_FEATURE_DIRECTORY`
   - Resolva o `spec-template` ativo pela pilha de presets/templates do Spec Kit (equivalente a `specify preset resolve spec-template`).
   - Copie o `spec-template` resolvido para `SPECIFY_FEATURE_DIRECTORY/spec.md` como ponto de partida.
   - Defina `SPEC_FILE` como `SPECIFY_FEATURE_DIRECTORY/spec.md`.
   - Persista o caminho resolvido em `.specify/feature.json`:
     ```json
     {
       "feature_directory": "<resolved feature dir>"
     }
     ```
   Escreva o caminho real resolvido (por exemplo, `specs/003-user-auth`), não a string literal `SPECIFY_FEATURE_DIRECTORY`.
   Isso permite que comandos posteriores (`/speckit-plan`, `/speckit-tasks` etc.) localizem a feature sem depender de convenções de branch Git.

   **IMPORTANTE**:
   - Crie somente uma feature por invocação de `/speckit-specify`.
   - O nome do diretório e o da branch Git são independentes; podem coincidir por escolha do usuário.
   - O diretório e o arquivo da spec são sempre criados por este comando, nunca pelo hook.

4. Carregue o `spec-template` ativo resolvido para compreender as seções obrigatórias.

5. **SE EXISTIR**: Carregue `.specify/memory/constitution.md` para consultar princípios e restrições de governança.

6. Siga este fluxo:
    1. Interprete a descrição dos argumentos.
       Se vazia: ERROR "Nenhuma descrição de feature fornecida".
    2. Extraia os conceitos principais.
       Identifique atores, ações, dados e restrições.
    3. Para pontos indefinidos:
       - Faça inferências fundamentadas no contexto e em padrões do setor.
       - Use [NEEDS CLARIFICATION: specific question] somente se:
         - A escolha afetar significativamente o escopo ou a experiência do usuário.
         - Existirem interpretações razoáveis com implicações diferentes.
         - Não houver padrão razoável.
       - **LIMITE: No máximo 3 marcadores [NEEDS CLARIFICATION] no total**.
       - Priorize por impacto: escopo > segurança/privacidade > experiência do usuário > detalhes técnicos.
    4. Preencha a seção de cenários de usuário e testes.
       Sem fluxo claro: ERROR "Não foi possível determinar os cenários de usuário".
    5. Gere requisitos funcionais.
       Cada requisito deve ser testável.
       Use padrões razoáveis para detalhes não definidos e registre-os na seção de premissas.
    6. Defina critérios de sucesso.
       Crie resultados mensuráveis e independentes de tecnologia.
       Inclua métricas quantitativas (tempo, desempenho, volume) e qualitativas (satisfação, conclusão de tarefas).
       Cada critério deve ser verificável sem detalhes de implementação.
    7. Identifique entidades principais, se houver dados.
    8. Retorne SUCCESS (spec pronta para planejamento).

7. Escreva a especificação em SPEC_FILE usando a estrutura do template, substituindo placeholders por detalhes concretos derivados da descrição, preservando ordem e títulos das seções.

8. **Validação de qualidade da especificação**: Após a primeira redação, valide os critérios de qualidade:

   a. **Crie a checklist de qualidade**: Gere `SPECIFY_FEATURE_DIRECTORY/checklists/requirements.md` usando a estrutura do template e estes itens:

      ```markdown
      # Checklist de Qualidade da Especificação: [FEATURE NAME]

      **Finalidade**: Validar completude e qualidade antes de planejar
      **Criada em**: [DATE]
      **Feature**: [Link para spec.md]

      ## Qualidade do Conteúdo

      - [ ] Sem detalhes de implementação (linguagens, frameworks, APIs)
      - [ ] Foco no valor para o usuário e nas necessidades de negócio
      - [ ] Redação voltada a stakeholders não técnicos
      - [ ] Todas as seções obrigatórias preenchidas

      ## Completude dos Requisitos

      - [ ] Nenhum marcador [NEEDS CLARIFICATION] restante
      - [ ] Requisitos testáveis e inequívocos
      - [ ] Critérios de sucesso mensuráveis
      - [ ] Critérios de sucesso independentes de tecnologia (sem detalhes de implementação)
      - [ ] Todos os cenários de aceitação definidos
      - [ ] Casos de borda identificados
      - [ ] Escopo claramente delimitado
      - [ ] Dependências e premissas identificadas

      ## Prontidão da Feature

      - [ ] Todos os requisitos funcionais possuem critérios claros de aceitação
      - [ ] Cenários de usuário cobrem os fluxos principais
      - [ ] Feature atende aos resultados mensuráveis dos critérios de sucesso
      - [ ] Nenhum detalhe de implementação indevido na especificação

      ## Notas

      - Itens incompletos exigem revisão da spec antes de `/speckit-clarify` ou `/speckit-plan`
      ```

   b. **Execute a validação**: Revise a spec contra cada item:
      - Determine se cada item passa ou falha.
      - Documente problemas específicos, citando as seções relevantes.

   c. **Trate os resultados**:

      - **Se todos passarem**: Marque a checklist como completa e prossiga para Hooks Obrigatórios Após a Execução.

         - **Se houver falhas, exceto [NEEDS CLARIFICATION]**:
            1. Liste itens reprovados e problemas específicos.
            2. Atualize a spec para corrigir cada problema.
            3. Revalide até todos passarem (máximo de 3 iterações).
            4. Se ainda houver falhas após 3 iterações, registre-as nas notas da checklist e avise o usuário.

         - **Se restarem marcadores [NEEDS CLARIFICATION]**:
            1. Extraia todos os marcadores [NEEDS CLARIFICATION: ...] da spec.
            2. **VERIFIQUE O LIMITE**: Se houver mais de 3, mantenha os 3 mais críticos por impacto em escopo/segurança/UX e faça inferências fundamentadas para os demais.
            3. Para cada esclarecimento (máximo de 3), apresente opções neste formato:

           ```markdown
           ## Pergunta [N]: [Tema]

           **Contexto**: [Cite a seção relevante da spec]

           **O que precisamos saber**: [Pergunta específica do marcador NEEDS CLARIFICATION]

           **Respostas Sugeridas**:

           | Opção | Resposta | Implicações |
           |--------|--------|--------------|
           | A      | [Primeira resposta sugerida] | [O que isso implica para a feature] |
           | B      | [Segunda resposta sugerida] | [O que isso implica para a feature] |
           | C      | [Terceira resposta sugerida] | [O que isso implica para a feature] |
           | Custom | Forneça sua própria resposta | [Explique como fornecer uma resposta personalizada] |

           **Sua escolha**: _[Aguarde a resposta do usuário]_
           ```

        4. **CRITICAL - Formatação de tabelas**: Garanta formatação Markdown correta:
           - Use espaçamento consistente e barras alinhadas.
           - Deixe espaços ao redor do conteúdo: `| Content |`, não `|Content|`.
           - O separador do cabeçalho deve ter pelo menos 3 hífens: `|--------|`.
           - Confira a renderização na prévia Markdown.
        5. Numere sequencialmente (Q1, Q2, Q3; máximo de 3).
        6. Apresente todas as perguntas juntas antes de aguardar respostas.
        7. Aguarde as escolhas para todas (por exemplo, "Q1: A, Q2: Custom - [detalhes], Q3: B").
        8. Substitua cada [NEEDS CLARIFICATION] pela resposta escolhida ou fornecida.
        9. Revalide quando todos os esclarecimentos estiverem resolvidos.

   d. **Atualize a checklist**: Após cada iteração, registre o estado atual de aprovação/reprovação.

## Hooks Obrigatórios Após a Execução

**Você DEVE concluir esta seção antes de informar a conclusão ao usuário.**

Verifique se `.specify/extensions.yml` existe na raiz do projeto.
- Se não existir ou não houver hooks registrados na chave `hooks.after_specify`, prossiga para o Relatório de Conclusão.
- Se existir, leia-o e procure entradas na chave `hooks.after_specify`.
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

Informe a conclusão com:
- `SPECIFY_FEATURE_DIRECTORY`: caminho da feature.
- `SPEC_FILE`: caminho da spec.
- Resumo dos resultados da checklist.
- Prontidão para a próxima fase (`/speckit-clarify` ou `/speckit-plan`).

**NOTA:** A criação de branch cabe ao hook `before_specify` (extensão Git). O diretório e o arquivo da spec são sempre criados por este comando central.

## Diretrizes Rápidas

- Foque **NO QUE** os usuários precisam e **POR QUÊ**.
- Evite COMO implementar (stack, APIs e estrutura de código).
- Escreva para stakeholders de negócio, não desenvolvedores.
- NÃO incorpore checklists à spec. Isso pertence a um comando separado.

### Requisitos das Seções

- **Seções obrigatórias**: Preencha para todas as features.
- **Seções opcionais**: Inclua somente se forem relevantes.
- Remova integralmente seções não aplicáveis, sem deixar "N/A".

### Orientações para Geração por IA

Ao criar a spec a partir de uma solicitação:

1. **Faça inferências fundamentadas**: Use contexto, padrões do setor e práticas comuns para preencher lacunas.
2. **Documente premissas**: Registre padrões razoáveis na seção de premissas.
3. **Limite esclarecimentos**: No máximo 3 [NEEDS CLARIFICATION], apenas para decisões críticas que:
   - Afetem significativamente escopo ou experiência do usuário.
   - Admitam interpretações razoáveis com implicações distintas.
   - Não possuam padrão razoável.
4. **Priorize esclarecimentos**: escopo > segurança/privacidade > experiência do usuário > detalhes técnicos.
5. **Pense como quem testa**: Todo requisito vago deve reprovar o item "testável e inequívoco".
6. **Áreas comuns de esclarecimento**, apenas sem padrão razoável:
   - Escopo e limites (incluir/excluir casos de uso).
   - Tipos de usuário e permissões (quando houver interpretações conflitantes).
   - Segurança/conformidade (quando houver relevância jurídica/financeira).

**Exemplos de padrões razoáveis** (não pergunte sobre eles):

- Retenção de dados: práticas padrão do setor para o domínio.
- Metas de desempenho: expectativas usuais de aplicações web/mobile, salvo indicação contrária.
- Tratamento de erros: mensagens compreensíveis e alternativas adequadas.
- Autenticação: sessão convencional ou OAuth2 para aplicações web.
- Integrações: padrões apropriados (REST/GraphQL para serviços web, chamadas de funções para bibliotecas, argumentos CLI para ferramentas etc.).

### Diretrizes dos Critérios de Sucesso

Os critérios de sucesso devem ser:

1. **Mensuráveis**: Inclua métricas específicas (tempo, percentual, contagem, taxa).
2. **Independentes de tecnologia**: Não mencione frameworks, linguagens, bancos ou ferramentas.
3. **Voltados ao usuário**: Descreva resultados para usuário/negócio, não detalhes internos.
4. **Verificáveis**: Testáveis sem conhecer a implementação.

**Bons exemplos**:

- "Usuários conseguem concluir a compra em menos de 3 minutos"
- "O sistema suporta 10.000 usuários simultâneos"
- "95% das buscas retornam resultados em menos de 1 segundo"
- "A taxa de conclusão das tarefas aumenta em 40%"

**Exemplos inadequados** (foco na implementação):

- "O tempo de resposta da API é inferior a 200ms" (técnico demais; use "Usuários veem resultados instantaneamente").
- "O banco suporta 1000 TPS" (detalhe de implementação; use uma métrica voltada ao usuário).
- "Componentes React renderizam eficientemente" (específico de framework).
- "Taxa de acerto do cache Redis acima de 80%" (específico de tecnologia).

## Critérios de Conclusão

- [ ] Especificação escrita em `SPEC_FILE` e validada contra a checklist de qualidade.
- [ ] Hooks de extensões acionados ou ignorados conforme as regras de Hooks Obrigatórios Após a Execução acima.
- [ ] Conclusão informada com diretório da feature, caminho da spec e resultados da checklist.
