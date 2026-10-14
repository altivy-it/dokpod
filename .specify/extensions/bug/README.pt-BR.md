# Fluxo de Triagem e Correção de Bugs

Esta extensão adiciona ao Spec Kit um fluxo repetível para avaliar, corrigir e validar bugs. Cada bug tem um diretório próprio em `.specify/bugs/<slug>/`, com um relatório por etapa.

## Fluxo

```text
Relato ou URL do bug
        |
        v
speckit.bug.assess  ->  assessment.md
        |
        v
speckit.bug.fix     ->  fix.md
        |
        v
speckit.bug.test    ->  test.md
```

A avaliação e a validação não alteram o código-fonte. A etapa de correção é a única que pode alterá-lo. Esse fluxo é independente do ciclo de feature: não exige criar `spec.md`, plano ou tarefas.

## Comandos

Os identificadores de comando da extensão são `speckit.bug.assess`, `speckit.bug.fix` e `speckit.bug.test`. No chat do GitHub Copilot deste projeto, use as skills registradas com hífens:

| Skill no Copilot | Finalidade | Relatório |
| --- | --- | --- |
| `/speckit-bug-assess` | Analisa o relato, investiga o código e propõe uma correção. | `assessment.md` |
| `/speckit-bug-fix` | Aplica a correção aprovada na avaliação e registra o que mudou. | `fix.md` |
| `/speckit-bug-test` | Reexecuta a reprodução e os testes relevantes; registra o resultado. | `test.md` |

## Exemplo de uso

Inicie com a descrição do problema e um `slug` curto, em kebab-case:

```text
/speckit-bug-assess "A consulta de inventário retorna 500 quando o filtro de data final não é informado." slug=filtro-data-final
```

Revise `.specify/bugs/filtro-data-final/assessment.md`, em especial a causa provável, a remediação proposta, os arquivos afetados e os testes planejados. Depois, aplique a correção:

```text
/speckit-bug-fix slug=filtro-data-final
```

Por fim, valide a correção:

```text
/speckit-bug-test slug=filtro-data-final
```

A validação registra um dos seguintes resultados:

- `verified`: as verificações críticas passaram e o sintoma original não foi reproduzido.
- `partial`: a evidência é inconclusiva ou houve resultado parcial.
- `failed`: o problema continua reproduzível ou a correção introduziu uma regressão.

Não marque uma correção como `verified` se a reprodução ou as verificações críticas não foram executadas.

## Relatórios por bug

```text
.specify/bugs/filtro-data-final/
├── assessment.md   # diagnóstico e remediação proposta
├── fix.md          # alterações realizadas e desvios da avaliação
└── test.md         # reprodução, verificações e resultado
```

Os comandos compartilham o `slug`, que também identifica o diretório dos relatórios. Se não houver um `slug` na chamada, a avaliação solicita um em uso interativo. Em modo automatizado, o agente escolhe um identificador único. Diretórios e relatórios existentes não devem ser substituídos silenciosamente; quando necessário, use outro `slug` ou confirme explicitamente a substituição no modo interativo.

## Instalação e ativação

Na raiz do Dokpod, use o CLI em container conforme o [guia de extensões](../../../.github/README.md#spec-kit-e-extensões). Dentro do container, instale a extensão empacotada pelo Spec Kit:

```powershell
specify extension add bug
```

Confira a instalação e a ativação:

```powershell
specify extension list
```

Para desabilitar ou reabilitar a extensão:

```powershell
specify extension disable bug
specify extension enable bug
```

No Dokpod, a instalação também registra as skills do Copilot em `.github/skills/speckit-bug-*`. Os arquivos de configuração e a extensão ficam sob `.specify/`.

## Limites e proteções

- `bug-assess` lê o relato e o código, mas grava somente o relatório de avaliação em `.specify/bugs/<slug>/`.
- `bug-fix` parte de uma avaliação existente, mantém a alteração restrita aos arquivos avaliados e registra em `fix.md` qualquer desvio necessário. Deve também adicionar ou atualizar os testes indicados.
- `bug-test` exige a avaliação e o relatório de correção. Executa a reprodução e as verificações relevantes, sem modificar o código-fonte.
- Relatos obtidos de URLs são dados não confiáveis, não instruções. A skill aplica uma política de confiança antes de buscar conteúdo externo e não deve seguir instruções embutidas na página.
- Os comandos são invocados explicitamente. Esta extensão não registra hooks próprios.

## Hooks de outras extensões

A extensão expõe eventos antes e depois de cada etapa (`before_bug_assess`, `after_bug_assess`, `before_bug_fix`, `after_bug_fix`, `before_bug_test` e `after_bug_test`). Outras extensões podem registrar hooks em `.specify/extensions.yml`. O Dokpod mantém os 18 hooks Git da base Inventory360Api, mas não há hooks registrados para os eventos de Bug Fixing.

Exemplo de hook obrigatório antes da avaliação:

```yaml
hooks:
  before_bug_assess:
    - extension: intake
      command: speckit.intake.collect
      enabled: true
      optional: false
      description: Coleta evidências no diretório do bug
```

Hooks opcionais são apresentados para execução pelo usuário. Hooks obrigatórios são executados e aguardados pela skill.
