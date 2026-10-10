---
description: "Validar se um bug corrigido foi resolvido e registrar o relatório de verificação"
---


# Testar Correção de Bug

Valide se a correção registrada por `__SPECKIT_COMMAND_BUG_FIX__` resolve o bug descrito por `__SPECKIT_COMMAND_BUG_ASSESS__`. A saída é um relatório em `.specify/bugs/<slug>/test.md`.

## Entrada do Usuário

```text
$ARGUMENTS
```

A entrada deve identificar o bug a validar. Aceite:

- `slug=<bug-slug>`, `--slug <bug-slug>` ou token isolado semelhante a slug.
- Caminho contendo o slug, como `.specify/bugs/login-timeout/`.
- **Entrada vazia**: Recorra ao contexto, conforme abaixo.

## Resolução do Slug

Resolva `BUG_SLUG` nesta ordem, parando na primeira correspondência:

1. **Entrada explícita**: Slug em `$ARGUMENTS` em uma das formas acima.
2. **Contexto da conversa**: Se `__SPECKIT_COMMAND_BUG_ASSESS__` ou `__SPECKIT_COMMAND_BUG_FIX__` acabou de executar, reutilize o slug sem perguntar. Confirme `.specify/bugs/<slug>/fix.md`; se ausente, avance na resolução.
3. **Candidato único em disco**: Liste `.specify/bugs/*/fix.md`; com exatamente um bug com `fix.md`, use-o.
4. **Desambiguação**:
  - **Interativo**: Pergunte qual bug validar e liste candidatos.
  - **Automatizado**: Pare com erro e liste candidatos, sem adivinhar.

Defina `BUG_SLUG` e `BUG_DIR = .specify/bugs/<BUG_SLUG>` e informe brevemente a origem: explícita, contexto, candidato único ou pergunta.

## Pré-requisitos

- `BUG_DIR/assessment.md` DEVE existir.
- `BUG_DIR/fix.md` DEVE existir; caso contrário, pare e oriente executar `__SPECKIT_COMMAND_BUG_FIX__` primeiro.
- Se `BUG_DIR/test.md` existir, peça autorização para sobrescrever no modo interativo ou recuse no automatizado.
- Leia `assessment.md` e `fix.md` integralmente para conhecer:
  - Sintoma original e passos de reprodução de `assessment.md`.
  - Mudanças reais e testes adicionados de `fix.md`.

## Verificações Antes da Execução

Neste ponto, `BUG_SLUG` e `BUG_DIR` estão resolvidos; hooks desta sessão podem reutilizá-los da conversa, mas nada é repassado automaticamente.

**Verifique os hooks de extensões (antes da verificação)**:
- Verifique se `.specify/extensions.yml` existe na raiz do projeto.
- Se existir, leia-o e procure entradas na chave `hooks.before_bug_test`.
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

1. **Planeje a validação**
   - Determine verificações que provem a resolução:
     - Repita os passos da avaliação ou seu equivalente automatizado.
     - Execute testes adicionados/atualizados na correção.
     - Execute suítes de regressão mais amplas dos arquivos alterados.
   - Determine verificações que comprovem ausência de regressão:
     - Suítes existentes dos módulos alterados.
     - Lint/verificação de tipos, se adotados.

2. **Execute as verificações**
  - Execute cada uma; registre comando, código de saída e trecho relevante, como últimas linhas ou assertion falha.
  - Se destrutiva, dependente de rede ou onerosa, registre `skipped` com motivo; não execute sem consentimento explícito.
  - Se impossível por ferramenta ausente ou framework não configurado, registre `not-run` com motivo, sem inventar resultados.

3. **Avalie o resultado**
   - Classifique como:
     - **verified**: Verificações críticas aprovadas e sintoma original não reproduz mais.
     - **partial**: Sintoma resolvido, mas surgiram regressões alheias ou há verificações inconclusivas.
     - **failed**: Sintoma persiste ou a correção quebrou a suíte de regressão.
   - Não exagere a confirmação. Se a reprodução não ocorreu, como por exigir produção, informe explicitamente.

4. **Escreva o relatório de verificação**

  Escreva em `BUG_DIR/test.md` com esta estrutura:

   ```markdown
  # Verificação de Bug: <título curto>

   - **Slug**: <BUG_SLUG>
  - **Testado em**: <data ISO 8601>
  - **Avaliação**: ./assessment.md
  - **Correção**: ./fix.md
  - **Resultado**: verified | partial | failed

  ## Resumo

  <Uma ou duas frases sobre reprodução, efetividade da correção e regressões encontradas.>

  ## Verificações Executadas

  | Verificação | Comando / Ação | Resultado | Notas |
   |-------|------------------|--------|-------|
  | Reprodução após correção | <comando ou passos manuais> | pass / fail / skipped / not-run | <nota curta> |
  | Testes novos / atualizados | `<command>` | pass / fail | <nota curta> |
  | Suíte de regressão | `<command>` | pass / fail / skipped | <nota curta> |
  | Lint / verificação de tipos | `<command>` | pass / fail / skipped | <nota curta> |

  ## Trechos de Saída

  <Trechos curtos, como resumo final dos testes ou assertion falha; sem logs completos.>

  ## Riscos Residuais

  - <limitação conhecida, ambiente não coberto etc.>

  ## Recomendação

  <Um parágrafo. Exemplos:>
  - "Encerre o bug: verificado ponta a ponta."
  - "Aguarde: reprodução inconclusiva; exige verificação em staging."
  - "Reabra: sintoma persiste; repita `__SPECKIT_COMMAND_BUG_ASSESS__`."
   ```

## Hooks Obrigatórios Após a Execução

**Você DEVE concluir esta seção antes de informar a conclusão ao usuário.**

Neste ponto, `BUG_SLUG` e `BUG_DIR` estão resolvidos e o relatório está disponível; hooks podem reutilizá-los da conversa, sem repasse automático.

Verifique se `.specify/extensions.yml` existe na raiz do projeto.
- Se não existir ou não houver hooks registrados na chave `hooks.after_bug_test`, prossiga para o Relatório de Conclusão.
- Se existir, leia-o e procure entradas na chave `hooks.after_bug_test`.
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

- Slug e caminho `BUG_DIR/test.md`.
- Resultado (`verified`, `partial`, `failed`).
- Com `failed`, recomende repetir `__SPECKIT_COMMAND_BUG_ASSESS__` com as novas evidências de `test.md`.

## Restrições de Segurança

- Este comando NÃO DEVE modificar código. Só executa verificações e escreve em `.specify/bugs/<slug>/`.
- Nunca sobrescreva `test.md` existente sem confirmação.
- Nunca classifique `verified` somente pelos testes se a reprodução prevista não foi executada; use `partial` e informe.
