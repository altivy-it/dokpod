---
name: speckit-git-feature
description: Cria uma branch de feature com numeração sequencial ou por timestamp
compatibility: Requer a estrutura de projeto do Spec Kit com o diretório .specify/
metadata:
  author: spec-kit-core
  source: extension:git
---

# Skill Git de Feature

# Criar Branch de Feature

Crie uma branch Git de feature e mude para ela para a especificação fornecida. Este comando cuida **somente da criação da branch**; o diretório da spec e seus arquivos são criados pelo fluxo central `/speckit-specify`.

## Entrada do Usuário

```text
$ARGUMENTS
```

Você **DEVE** considerar a entrada do usuário antes de prosseguir (se não estiver vazia).

## Sobrescrita por Variável de Ambiente

Se o usuário fornecer explicitamente `GIT_BRANCH_NAME` (por exemplo, por variável de ambiente, argumento ou na solicitação), repasse o valor ao script definindo a variável de ambiente `GIT_BRANCH_NAME` antes de invocá-lo. Quando `GIT_BRANCH_NAME` estiver definido:
- O script usa exatamente esse valor como nome da branch, sem gerar prefixos ou sufixos
- As opções `--short-name`, `--number` e `--timestamp` são ignoradas
- `FEATURE_NUM` é extraído quando o segmento final do caminho começa com um marcador numérico ou de timestamp para feature (por exemplo, `042-name`, `feat/042-name` ou `jdoe/app/042-name`); caso contrário, recebe o nome completo da branch

## Pré-requisitos

- Verifique se o Git está disponível executando `git rev-parse --is-inside-work-tree 2>/dev/null`
- Se o Git não estiver disponível, avise o usuário e ignore a criação da branch

## Modo de Numeração da Branch

Determine a estratégia de numeração da branch verificando a configuração nesta ordem:

1. Verifique o valor de `branch_numbering` em `.specify/extensions/git/git-config.yml`
2. Verifique o valor de `feature_numbering` em `.specify/init-options.json` (herdado do núcleo)
3. Verifique o valor de `branch_numbering` em `.specify/init-options.json` (obsoleto, mantido para compatibilidade retroativa e será removido em uma versão futura)
4. Use `sequential` como padrão se nenhum dos valores anteriores existir

## Template do Nome da Branch

Verifique se há um valor opcional de `branch_template` em `.specify/extensions/git/git-config.yml`. Se estiver vazio ou ausente, use o formato padrão `{number}-{slug}`. Se estiver definido, `{slug}` não pode aparecer antes de `{number}`, o segmento final do caminho deve começar com `{number}-`, e o script expande estes tokens:

- `{author}`: autor da configuração Git sanitizado (`user.name`, usando a parte local do e-mail como alternativa)
- `{app}`: nome sanitizado do diretório inicializado pelo Spec Kit
- `{number}`: número sequencial ou timestamp
- `{slug}`: slug curto gerado para a branch

Em monorepos, um template como `{author}/{app}/{number}-{slug}` cria nomes como `jdoe/web/008-guided-tour` e mantém a numeração de features independente por projeto.

O script também aceita `branch_prefix` como forma abreviada para namespaces simples; ele é expandido para `<branch_prefix>/{number}-{slug}`.

## Execução

Gere um nome curto e conciso (2 a 4 palavras) para a branch:
- Analise a descrição da feature e extraia as palavras-chave mais significativas
- Quando possível, use o formato ação-substantivo (por exemplo, "add-user-auth", "fix-payment-bug")
- Preserve termos técnicos e siglas (OAuth2, API, JWT etc.)

Execute o script apropriado para sua plataforma:

- **Bash**: `.specify/extensions/git/scripts/bash/create-new-feature-branch.sh --json --short-name "<short-name>" "<feature description>"`
- **Bash (timestamp)**: `.specify/extensions/git/scripts/bash/create-new-feature-branch.sh --json --timestamp --short-name "<short-name>" "<feature description>"`
- **PowerShell**: `.specify/extensions/git/scripts/powershell/create-new-feature-branch.ps1 -Json -ShortName "<short-name>" "<feature description>"`
- **PowerShell (timestamp)**: `.specify/extensions/git/scripts/powershell/create-new-feature-branch.ps1 -Json -Timestamp -ShortName "<short-name>" "<feature description>"`

**IMPORTANTE**:
- NÃO informe `--number`; o script determina automaticamente o próximo número correto
- Sempre inclua a opção JSON (`--json` para Bash, `-Json` para PowerShell) para que a saída possa ser analisada de forma confiável
- Execute este script somente uma vez por feature
- A saída JSON conterá `BRANCH_NAME` e `FEATURE_NUM`
- Não expanda `branch_template` manualmente; o script lê a configuração da extensão Git e a aplica de forma consistente

## Degradação Graciosa

Se o Git não estiver instalado ou o diretório atual não for um repositório Git:
- A criação da branch será ignorada com o aviso: `[specify] Warning: Git repository not detected; skipped branch creation`
- O script ainda exibirá `BRANCH_NAME` e `FEATURE_NUM` para que o chamador possa referenciá-los

## Saída

O script gera JSON com:
- `BRANCH_NAME`: nome da branch (por exemplo, `003-user-auth`, `20260319-143022-user-auth` ou `jdoe/web/003-user-auth`)
- `FEATURE_NUM`: prefixo numérico ou timestamp utilizado
