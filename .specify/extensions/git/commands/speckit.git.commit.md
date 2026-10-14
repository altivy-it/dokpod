---
description: "Faz commit automático das alterações após a conclusão de um comando do Spec Kit"
---

# Fazer Commit Automático das Alterações

Adiciona ao stage e faz commit automático de todas as alterações após a conclusão de um comando do Spec Kit.

## Comportamento

Este comando é invocado como hook antes ou depois dos comandos centrais. Ele:

1. Determina o nome do evento pelo contexto do hook (por exemplo, se for invocado pelo hook `after_specify`, o evento será `after_specify`; se for `before_plan`, será `before_plan`)
2. Verifica a seção `auto_commit` em `.specify/extensions/git/git-config.yml`
3. Consulta a chave específica do evento para verificar se o commit automático está habilitado
4. Usa `auto_commit.default` como alternativa quando não houver uma chave específica para o evento
5. Determina a mensagem de commit com base em `commit_style` (veja abaixo)
6. Se estiver habilitado e houver alterações sem commit, executa `git add .` e `git commit`

## Estilos de Mensagem de Commit

Controlados pela chave `commit_style` em `.specify/extensions/git/git-config.yml`:

- **`fixed`** (padrão): usa `message` por comando, se configurada; caso contrário, usa uma mensagem genérica `[Spec Kit] Auto-commit <phase> <command>`.
- **`conventional`**: inspeciona as alterações reais (`git diff` / `git status`) desde o último commit e gera uma mensagem Conventional Commit de uma linha (`type(scope): subject`, por exemplo, `feat: add OAuth specification` ou `docs: update implementation plan`) que resuma corretamente a alteração. Grava essa mensagem em um arquivo temporário e passa o caminho do arquivo ao script (veja Execução abaixo). Neste modo, os valores configurados em `message` são ignorados.

## Execução

Determine o nome do evento que acionou este comando e execute o script:

- **Bash**: `.specify/extensions/git/scripts/bash/auto-commit.sh <event_name> [--message-file <path>]`
- **PowerShell**: `.specify/extensions/git/scripts/powershell/auto-commit.ps1 <event_name> [-MessageFile <path>]`

Substitua `<event_name>` pelo evento real do hook (por exemplo, `after_specify`, `before_plan`, `after_implement`). Só passe uma mensagem gerada quando `commit_style: conventional` estiver configurado; primeiro confira o valor de `commit_style` em `.specify/extensions/git/git-config.yml`:

- Se for `conventional`: inspecione o diff e gere uma mensagem Conventional Commit. **Não interpole a mensagem gerada diretamente em uma string de comando do shell**; seu conteúdo deriva das alterações do repositório e pode conter caracteres (aspas, `$(...)`, crases) que o shell executaria ou que quebrariam as aspas do comando. Em vez disso, grave a mensagem em um arquivo temporário usando sua ferramenta de edição de arquivos (não use `echo`/`printf` no shell) e passe o caminho desse arquivo por `--message-file <path>` (Bash) ou `-MessageFile <path>` (PowerShell).
- Se for `fixed` ou estiver ausente: execute o script somente com `<event_name>`; ele usará a mensagem configurada ou estática.

## Configuração

Em `.specify/extensions/git/git-config.yml`:

```yaml
# "fixed" (padrão) usa as mensagens abaixo; "conventional" pede ao agente
# que gere uma mensagem Conventional Commit com base no diff.
commit_style: fixed

auto_commit:
  default: false          # Controle global: true habilita para todos os comandos
  after_specify:
    enabled: true          # Sobrescrita específica do comando
    message: "[Spec Kit] Add specification"
  after_plan:
    enabled: false
    message: "[Spec Kit] Add implementation plan"
```

## Degradação Graciosa

- Se o Git não estiver disponível ou o diretório atual não for um repositório: ignora a operação com um aviso
- Se não houver arquivo de configuração: ignora a operação (desabilitada por padrão)
- Se não houver alterações para commit: ignora a operação e exibe uma mensagem
- Se `commit_style: conventional` estiver definido e nenhuma mensagem gerada for fornecida: falha com um erro claro em vez de usar silenciosamente o formato de mensagem fixa
