---
name: speckit-git-initialize
description: Inicializa um repositório Git com um commit inicial
compatibility: Requer a estrutura de projeto do Spec Kit com o diretório .specify/
metadata:
  author: spec-kit-core
  source: extension:git
---

# Skill Git de Inicialização

# Inicializar Repositório Git

Inicialize um repositório Git no diretório atual do projeto, caso ainda não exista um.

## Execução

Execute o script apropriado a partir da raiz do projeto:

- **Bash**: `.specify/extensions/git/scripts/bash/initialize-repo.sh`
- **PowerShell**: `.specify/extensions/git/scripts/powershell/initialize-repo.ps1`

Se os scripts da extensão não forem encontrados, use como alternativa:
- **Bash**: `git init && git add . && git commit -m "Initial commit from Specify template"`
- **PowerShell**: `git init; git add .; git commit -m "Initial commit from Specify template"`

O script realiza todas as verificações internamente:
- Ignora a operação se o Git não estiver disponível
- Ignora a operação se já estiver dentro de um repositório Git
- Executa `git init`, `git add .` e `git commit` com uma mensagem de commit inicial

## Personalização

Substitua o script para adicionar etapas de inicialização do Git específicas do projeto:
- Templates personalizados de `.gitignore`
- Configuração do nome padrão de branch (`git config init.defaultBranch`)
- Configuração do Git LFS
- Instalação de Git hooks
- Configuração da assinatura de commits
- Inicialização do Git Flow

## Saída

Em caso de sucesso:
- `[OK] Git repository initialized`

## Degradação Graciosa

Se o Git não estiver instalado:
- Avise o usuário
- Ignore a inicialização do repositório
- O projeto continuará funcionando sem Git (as specs ainda poderão ser criadas em `specs/`)

Se o Git estiver instalado, mas `git init`, `git add .` ou `git commit` falhar:
- Apresente o erro ao usuário
- Interrompa este comando em vez de continuar com um repositório parcialmente inicializado
