---
name: "speckit-constitution"
description: "Criar ou atualizar a constituição do projeto a partir de princípios fornecidos ou definidos interativamente."
compatibility: "Requer a estrutura de projeto do spec-kit com o diretório .specify/"
metadata:
  author: "github-spec-kit"
  source: "templates/commands/constitution.md"
---


## Entrada do Usuário

```text
$ARGUMENTS
```

Você **DEVE** considerar a entrada do usuário antes de prosseguir (se não estiver vazia).

## Proteção de Escopo

O trabalho deste comando limita-se à atualização da constituição do projeto. Templates e comandos
dependentes leem a constituição em tempo de execução e não são modificados aqui.

- Classifique cada parte da entrada como conteúdo da constituição ou intenção separada, alheia à governança.
- Se houver solicitações de implementação de feature, geração de código, refatoração, build ou deploy,
  você **NÃO DEVE** executá-las. Registre-as como intenções adiadas.
- Você **NÃO DEVE** criar, modificar ou excluir código da aplicação, rotas, componentes, testes,
  arquivos de deploy ou outros artefatos alheios ao fluxo da constituição.
- Se não estiver claro se uma instrução é conteúdo da constituição, peça esclarecimento antes de alterar.
- Após atualizar a constituição, inclua uma seção `Next Actions` (próximas ações) para as intenções adiadas.
  Liste cada intenção original e sugira o comando Spec Kit apropriado, como `/speckit-specify`, sem invocá-lo.
- Se não houver intenções alheias à governança, omita a seção `Next Actions`.

## Verificações Antes da Execução

**Verifique os hooks de extensões (antes da atualização da constituição)**:
- Verifique se `.specify/extensions.yml` existe na raiz do projeto.
- Se existir, leia-o e procure entradas na chave `hooks.before_constitution`.
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

Você está atualizando a constituição em `.specify/memory/constitution.md`. A estrutura ativa
é resolvida no momento da execução a partir de `constitution-template`, pela pilha de resolução
de presets/templates do Spec Kit.

Siga este fluxo de execução:

1. Execute `.specify/scripts/powershell/resolve-template.ps1 constitution-template -Json` na raiz do repositório e interprete `TEMPLATE_CONTENT` como o template ativo.
   - O resolver compartilhado aplica overrides do projeto e compõe camadas de presets e extensões
     antes de recorrer ao template central. Ele DEVE concluir com sucesso antes de prosseguir.
   - Se falhar, pare e informe o erro de resolução; não prossiga com apenas uma camada contribuinte.
   - Se `.specify/memory/constitution.md` existir, carregue os valores e alterações atuais do projeto.
     Preserve as informações ainda aplicáveis ao usar a estrutura recém-resolvida.
   - Se não existir, use o template resolvido como documento inicial.
   - Não escreva em nenhuma camada de template versionada.
   - Identifique todos os placeholders no formato `[ALL_CAPS_IDENTIFIER]`.
   **IMPORTANTE**: O usuário pode exigir mais ou menos princípios que o template. Se indicar uma quantidade, respeite-a e siga a estrutura geral, ajustando o documento.

2. Colete ou derive os valores dos placeholders:
   - Use o valor fornecido na conversa, quando houver.
   - Caso contrário, infira pelo contexto existente (README, docs e versões anteriores incorporadas).
   - Datas de governança: `RATIFICATION_DATE` é a data original de adoção (se desconhecida, pergunte ou marque TODO); `LAST_AMENDED_DATE` é hoje se houver alterações, caso contrário mantenha a anterior.
   - Incremente `CONSTITUTION_VERSION` conforme o versionamento semântico:
     - MAJOR: Remoções ou redefinições incompatíveis de governança/princípios.
     - MINOR: Novo princípio/seção ou ampliação material de orientação.
     - PATCH: Esclarecimentos, redação, erros de digitação ou refinamentos sem mudança semântica.
   - Se o tipo de incremento for ambíguo, apresente a justificativa antes de finalizar.

3. Redija a constituição atualizada usando obrigatoriamente a estrutura do template resolvido:
  - Substitua todos os placeholders por texto concreto (não deixe tokens entre colchetes, exceto campos intencionalmente não definidos pelo projeto; justifique explicitamente cada um).
  - Preserve a hierarquia dos títulos; remova comentários substituídos, salvo se ainda esclarecerem o conteúdo.
  - Em cada princípio, inclua nome conciso, parágrafo ou lista com regras inegociáveis e justificativa explícita quando não for óbvia.
  - A seção de governança deve listar o procedimento de alteração, a política de versões e as expectativas de revisão de conformidade.

4. Produza um relatório de impacto da sincronização em comentário HTML no topo da constituição atualizada.
  O relatório é material temporário para revisão humana, não conteúdo de governança;
  deve ser removido antes do commit da constituição alterada.
  - Mudança de versão: anterior → nova.
  - Princípios modificados (título anterior → novo, se renomeados).
  - Seções adicionadas.
  - Seções removidas.
  - TODOs de acompanhamento para placeholders intencionalmente adiados.

5. Valide antes da resposta final:
  - Nenhum token entre colchetes sem explicação.
  - Linha de versão correspondente ao relatório.
  - Datas ISO no formato YYYY-MM-DD.
  - Princípios declarativos, testáveis e sem linguagem vaga (substitua "deveria" por MUST/SHOULD com justificativa, quando adequado).

6. Escreva a constituição completa em `.specify/memory/constitution.md` (sobrescreva).

7. Apresente um resumo final com:
  - Nova versão e justificativa do incremento.
  - Placeholders TODO ou itens adiados que exijam acompanhamento manual.
  - Mensagem de commit sugerida (por exemplo, `docs: atualizar constituição para vX.Y.Z (novos princípios e revisão de governança)`).
  - Seção `Next Actions` para intenções adiadas alheias à governança.

Requisitos de formatação e estilo:

- Use os títulos Markdown exatamente como no template (não altere os níveis).
- Quebre linhas longas de justificativa para facilitar a leitura (idealmente <100 caracteres), sem impor quebras artificiais.
- Mantenha uma única linha vazia entre seções.
- Evite espaços no final das linhas.

Se o usuário fornecer alterações parciais (como revisar apenas um princípio), execute mesmo assim a validação e a decisão de versão.

Se faltar informação crítica (como data de ratificação realmente desconhecida), insira `TODO(<FIELD_NAME>): explicação` e inclua-a entre os itens adiados no relatório de impacto.

Escreva somente em `.specify/memory/constitution.md`; não crie nem modifique arquivos-fonte dos templates.

## Verificações Após a Execução

**Verifique os hooks de extensões (após a atualização da constituição)**:
Verifique se `.specify/extensions.yml` existe na raiz do projeto.
- Se existir, leia-o e procure entradas na chave `hooks.after_constitution`.
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
