# Extensão de Fluxo de Branches Git

Inicialização de repositório Git, criação de branches de feature, numeração (sequencial ou timestamp), validação, detecção de repositório remoto e commits automáticos para o Spec Kit.

## Overview

Esta extensão fornece operações Git como um módulo opcional e independente. Ela gerencia:

- **Inicialização de repositório** com mensagens de commit configuráveis
- **Criação de branches de feature** com numeração sequencial (`001-feature-name`) ou por timestamp (`20260319-143022-feature-name`) e templates opcionais para namespaces de branches
- **Validação de branches** para garantir que sigam as convenções de nomenclatura
- **Detecção de repositório remoto Git** para integração com GitHub (por exemplo, criação de issues)
- **Commit automático** após comandos centrais (configurável por comando, com mensagens personalizadas ou mensagens Conventional Commit geradas pelo agente)

## Commands

| Comando | Descrição |
|---------|-------------|
| `speckit.git.initialize` | Inicializa um repositório Git com uma mensagem de commit configurável |
| `speckit.git.feature` | Cria uma branch de feature com numeração sequencial ou por timestamp |
| `speckit.git.validate` | Valida se a branch atual segue as convenções de nomenclatura de branches de feature |
| `speckit.git.remote` | Detecta a URL remota Git para integração com o GitHub |
| `speckit.git.commit` | Faz commit automático das alterações (ativação e mensagens configuráveis por comando) |

## Hooks

| Evento | Comando | Opcional | Descrição |
|-------|---------|----------|-------------|
| `before_constitution` | `speckit.git.initialize` | Não | Inicializa o repositório Git antes de configurar a constituição |
| `before_specify` | `speckit.git.feature` | Não | Cria a branch de feature antes da especificação |
| `before_clarify` | `speckit.git.commit` | Sim | Faz commit das alterações pendentes antes do esclarecimento |
| `before_plan` | `speckit.git.commit` | Sim | Faz commit das alterações pendentes antes do planejamento |
| `before_tasks` | `speckit.git.commit` | Sim | Faz commit das alterações pendentes antes de gerar as tarefas |
| `before_implement` | `speckit.git.commit` | Sim | Faz commit das alterações pendentes antes da implementação |
| `before_checklist` | `speckit.git.commit` | Sim | Faz commit das alterações pendentes antes da checklist |
| `before_analyze` | `speckit.git.commit` | Sim | Faz commit das alterações pendentes antes da análise |
| `before_taskstoissues` | `speckit.git.commit` | Sim | Faz commit das alterações pendentes antes de sincronizar issues |
| `after_constitution` | `speckit.git.commit` | Sim | Faz commit automático após atualizar a constituição |
| `after_specify` | `speckit.git.commit` | Sim | Faz commit automático após gerar a especificação |
| `after_clarify` | `speckit.git.commit` | Sim | Faz commit automático após o esclarecimento |
| `after_plan` | `speckit.git.commit` | Sim | Faz commit automático após o planejamento |
| `after_tasks` | `speckit.git.commit` | Sim | Faz commit automático após gerar as tarefas |
| `after_implement` | `speckit.git.commit` | Sim | Faz commit automático após a implementação |
| `after_checklist` | `speckit.git.commit` | Sim | Faz commit automático após gerar a checklist |
| `after_analyze` | `speckit.git.commit` | Sim | Faz commit automático após a análise |
| `after_taskstoissues` | `speckit.git.commit` | Sim | Faz commit automático após sincronizar issues |

## Configuration

A configuração fica em `.specify/extensions/git/git-config.yml`:

```yaml
# Estratégia de numeração das branches: "sequential" ou "timestamp"
branch_numbering: sequential

# Template opcional para o nome da branch. Deixe vazio para usar o padrão "{number}-{slug}".
# Tokens aceitos: {author}, {app}, {number}, {slug}; {slug} não pode aparecer
# antes de {number}, e o segmento final do caminho deve começar com {number}-.
# Exemplo para monorepos: "{author}/{app}/{number}-{slug}"
branch_template: ""

# Namespace abreviado opcional. Deixe vazio para usar branch_template ou o comportamento padrão.
# Exemplo: "features/{app}" é expandido para "features/{app}/{number}-{slug}"
branch_prefix: ""

# Mensagem de commit personalizada para git init
init_commit_message: "[Spec Kit] Commit inicial"

# Estilo de mensagem para hooks de commit automático: "fixed" (padrão) usa as
# mensagens abaixo; "conventional" pede ao agente que gere uma mensagem Conventional
# Commit (por exemplo, "feat: add OAuth spec") com base no diff.
commit_style: fixed

# Commit automático por comando (todos desabilitados por padrão)
# Exemplo: habilita o commit automático após specify
auto_commit:
  default: false
  after_specify:
    enabled: true
    message: "[Spec Kit] Adicionar especificação"
```

`{author}` é obtido da configuração do Git e sanitizado para uso em nomes de branch. `{app}` é obtido do nome do diretório inicializado pelo Spec Kit. Templates personalizados não podem colocar `{slug}` antes de `{number}` e devem iniciar o segmento final do caminho com `{number}-`, para que os nomes gerados continuem sendo branches de feature válidas. Em um monorepo no caminho `apps/web/.specify/`, um template como `{author}/{app}/{number}-{slug}` gera branches como `jdoe/web/008-guided-tour`.

Para personalizar apenas o namespace, também é possível usar `branch_prefix` como forma abreviada, expandida para `<branch_prefix>/{number}-{slug}`.

## Installation

```bash
# Instala a extensão Git incluída no pacote (não requer acesso à rede)
specify extension add git
```

## Disabling

```bash
# Desabilita a extensão Git (a criação de specs continua sem criar branches)
specify extension disable git

# Reabilita a extensão
specify extension enable git
```

## Graceful Degradation

Quando o Git não estiver instalado ou o diretório não for um repositório Git:
- Os diretórios de spec ainda serão criados em `specs/`
- A criação de branch será ignorada, com um aviso
- A validação da branch será ignorada, com um aviso
- A detecção do repositório remoto retornará resultados vazios

## Scripts

Esta extensão inclui scripts multiplataforma:

- `scripts/bash/create-new-feature-branch.sh` — implementação em Bash (somente criação de branch)
- `scripts/bash/git-common.sh` — utilitários Git compartilhados (Bash)
- `scripts/powershell/create-new-feature-branch.ps1` — implementação em PowerShell (somente criação de branch)
- `scripts/powershell/git-common.ps1` — utilitários Git compartilhados (PowerShell)
