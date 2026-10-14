---
name: speckit-git-remote
description: Detecta a URL remota Git para integração com o GitHub
compatibility: Requer a estrutura de projeto do Spec Kit com o diretório .specify/
metadata:
  author: spec-kit-core
  source: extension:git
---

# Skill Git de Remoto

# Detectar URL Remota Git

Detecte a URL remota Git para integração com serviços do GitHub (por exemplo, criação de issues).

## Pré-requisitos

- Verifique se o Git está disponível executando `git rev-parse --is-inside-work-tree 2>/dev/null`
- Se o Git não estiver disponível, exiba um aviso e retorne um resultado vazio:
  ```
  [specify] Warning: Git repository not detected; cannot determine remote URL
  ```

## Execução

Execute o comando a seguir para obter a URL remota:

```bash
git config --get remote.origin.url
```

## Saída

Analise a URL remota e determine:

1. **Proprietário do repositório**: extraia da URL (por exemplo, `github` de `https://github.com/github/spec-kit.git`)
2. **Nome do repositório**: extraia da URL (por exemplo, `spec-kit` de `https://github.com/github/spec-kit.git`)
3. **É GitHub**: determine se o remoto aponta para um repositório do GitHub

Formatos de URL aceitos:
- HTTPS: `https://github.com/<owner>/<repo>.git`
- SSH: `git@github.com:<owner>/<repo>.git`

> [!CAUTION]
> Informe um repositório do GitHub SOMENTE se a URL remota realmente apontar para github.com.
> NÃO suponha que o remoto seja GitHub se o formato da URL não corresponder.

## Degradação Graciosa

Se o Git não estiver instalado, o diretório não for um repositório Git ou nenhum remoto estiver configurado:
- Retorne um resultado vazio
- NÃO gere erro; outros fluxos devem continuar sem as informações do remoto Git
