---
description: "Aplicar a correção proposta na avaliação de um bug e registrar as mudanças"
---


# Corrigir Bug

Aplique a correção proposta por `__SPECKIT_COMMAND_BUG_ASSESS__` e registre as mudanças em `.specify/bugs/<slug>/fix.md`. Este comando só é válido **depois** de existir uma avaliação para o slug.

## Entrada do Usuário

```text
$ARGUMENTS
```

A entrada deve identificar o bug a corrigir. Aceite:

- `slug=<bug-slug>`, `--slug <bug-slug>` ou token isolado semelhante a slug.
- Caminho contendo o slug, como `.specify/bugs/login-timeout/`.
- **Entrada vazia**: Recorra ao contexto, conforme abaixo.

## Resolução do Slug

Resolva `BUG_SLUG` nesta ordem, parando na primeira correspondência:

1. **Entrada explícita**: Slug em `$ARGUMENTS` em uma das formas acima.
2. **Contexto da conversa**: Se `__SPECKIT_COMMAND_BUG_ASSESS__` acabou de executar, reutilize o slug informado sem perguntar. Confirme `.specify/bugs/<slug>/assessment.md`; se ausente, avance na resolução.
3. **Candidato único em disco**: Liste `.specify/bugs/*/assessment.md`. Com exatamente um `assessment.md`, use o slug do diretório pai.
4. **Desambiguação**:
  - **Interativo**: Pergunte qual bug corrigir e liste candidatos.
  - **Automatizado**: Pare com erro e liste candidatos, sem adivinhar.

Defina `BUG_SLUG` e `BUG_DIR = .specify/bugs/<BUG_SLUG>` e informe brevemente a origem: explícita, contexto, candidato único ou pergunta.

## Pré-requisitos

- `BUG_DIR/assessment.md` DEVE existir; caso contrário, pare e oriente executar `__SPECKIT_COMMAND_BUG_ASSESS__` primeiro.
- Se `BUG_DIR/fix.md` existir, peça autorização para sobrescrever no modo interativo ou recuse no automatizado.
- Leia `BUG_DIR/assessment.md` integralmente. O contrato são as seções **Correção Proposta**, **Arquivos Prováveis de Alteração**, **Testes a Adicionar ou Atualizar** e **Riscos e Considerações**; em relatórios anteriores, correspondem a Proposed Remediation, Files likely to change, Tests to add or update e Risks & Considerations.

## Verificações Antes da Execução

Neste ponto, `BUG_SLUG` e `BUG_DIR` estão resolvidos; hooks desta sessão podem reutilizá-los da conversa, mas nada é repassado automaticamente.

**Verifique os hooks de extensões (antes da correção)**:
- Verifique se `.specify/extensions.yml` existe na raiz do projeto.
- Se existir, leia-o e procure entradas na chave `hooks.before_bug_fix`.
- Se o YAML não puder ser interpretado ou for inválido, não ignore silenciosamente: informe que `.specify/extensions.yml` não pôde ser lido (inclua o erro do parser) e que nenhum hook foi verificado, inclusive hooks obrigatórios (`optional: false`); depois, continue normalmente
- Exclua os hooks cujo `enabled` seja explicitamente `false`. Considere habilitados por padrão aqueles sem o campo `enabled`.
- Para cada hook restante, **não** tente interpretar ou avaliar expressões `condition`:
  - Se não houver `condition`, ou se ela for nula/vazia, considere o hook executável
  - Se houver `condition` não vazia, ignore o hook e deixe a avaliação da condição para a implementação de HookExecutor
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

    Aguarde o resultado do hook antes de prosseguir para a Execução.
    ```
    Após apresentar o bloco, você DEVE invocar o hook e aguardar sua conclusão. Execute-o como executaria o comando nesta sessão (a invocação pode diferir do identificador literal `{command}`; por exemplo, um agente em modo skills usa `/skill:speckit-...` ou `$speckit-...`). Apresentar apenas o bloco não executa o hook.
- Se não houver hooks registrados ou `.specify/extensions.yml` não existir, prossiga sem anunciar essa ausência

## Execução

1. **Confirme o plano**
  - Resuma em 3 a 6 itens o que mudará e onde, conforme a avaliação.
  - Com veredito `invalid`, pare: nada a corrigir. Informe o usuário e encerre.
  - Com `likely valid, needs reproduction` e itens `[NEEDS CLARIFICATION]` não resolvidos, sinalize-os e peça autorização para prosseguir no modo interativo; no automatizado, pare.

2. **Aplique a correção**
  - Faça as mudanças da proposta preferencial nos arquivos listados; se novas evidências exigirem ampliar o escopo, registre a ampliação explicitamente no relatório.
  - Adicione/atualize os testes citados para impedir regressões silenciosas.
  - Mantenha a mudança mínima: sem refatorações alheias nem dependências não previstas.
  - Se a avaliação estiver errada, a correção não funcionar ou a causa for outra, PARE de editar, registre em **Desvios da Avaliação** (Deviations from Assessment em relatórios anteriores) e recomende repetir `__SPECKIT_COMMAND_BUG_ASSESS__`.

3. **Execute verificações locais**
  - Se houver comandos claros, como `pytest`, `npm test` ou `cargo test`, execute testes dos caminhos alterados e registre aprovação/falha e saída principal.
  - Não execute suítes destrutivas ou dependentes de rede sem consentimento.

4. **Escreva o relatório de correção**

  Escreva em `BUG_DIR/fix.md` com esta estrutura:

   ```markdown
  # Correção de Bug: <título curto>

   - **Slug**: <BUG_SLUG>
  - **Corrigido em**: <data ISO 8601>
  - **Avaliação**: ./assessment.md
  - **Estado**: applied | partial | not-applied

  ## Resumo

  <Uma ou duas frases sobre o que mudou e por quê.>

  ## Alterações

  | Arquivo | Alteração | Notas |
   |------|--------|-------|
  | `path/to/file.py` | <adicionado / modificado / removido> | <nota curta> |
  | `path/to/test_file.py` | teste adicionado | <nota curta> |

  ## Destaques do Diff (opcional)

  <Trechos curtos e ilustrativos das mudanças principais, sem reproduzir todo o diff.>

  ## Testes Adicionados ou Atualizados

  - `path/to/test_file.py::test_name` — <o que protege>

  ## Verificação Local

  - Comandos executados: `<command>` → <resultado breve>
  - Verificações manuais: <o que foi conferido, se houver>

  ## Desvios da Avaliação

  <Vazio se não houver. Caso contrário, liste desvios da proposta e seus motivos.>

  ## Acompanhamento

  - <limpeza sugerida, monitoramento, atualização de docs etc.>
   ```

## Hooks Obrigatórios Após a Execução

**Você DEVE concluir esta seção antes de informar a conclusão ao usuário.**

Neste ponto, `BUG_SLUG` e `BUG_DIR` estão resolvidos e o relatório está disponível; hooks podem reutilizá-los da conversa, sem repasse automático.

Verifique se `.specify/extensions.yml` existe na raiz do projeto.
- Se não existir ou não houver hooks registrados na chave `hooks.after_bug_fix`, prossiga para o Relatório de Conclusão.
- Se existir, leia-o e procure entradas na chave `hooks.after_bug_fix`.
- Se o YAML não puder ser interpretado ou for inválido, não ignore silenciosamente: informe que `.specify/extensions.yml` não pôde ser lido (inclua o erro do parser) e que nenhum hook foi verificado, inclusive hooks obrigatórios (`optional: false`); depois, prossiga para o Relatório de Conclusão.
- Exclua os hooks cujo `enabled` seja explicitamente `false`. Considere habilitados por padrão aqueles sem o campo `enabled`.
- Para cada hook restante, **não** tente interpretar ou avaliar expressões `condition`:
  - Se não houver `condition`, ou se ela for nula/vazia, considere o hook executável
  - Se houver `condition` não vazia, ignore o hook e deixe a avaliação da condição para a implementação de HookExecutor
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

**Informe**:

- Slug e caminho `BUG_DIR/fix.md`.
- Estado (`applied`, `partial`, `not-applied`).
- Próxima etapa sugerida: `__SPECKIT_COMMAND_BUG_TEST__ slug=<BUG_SLUG>`.

## Restrições de Segurança

- Nunca modifique arquivos fora do workspace do projeto.
- Nunca edite `assessment.md`, pois é o contrato. Registre divergências em `fix.md`, em **Desvios da Avaliação** (Deviations from Assessment em relatórios anteriores).
- Nunca exclua arquivos sem exigência explícita da avaliação.
- Nunca sobrescreva `fix.md` existente sem confirmação.
