---
name: speckit-git-validate
description: Valida se a branch atual segue as convenções de nomenclatura de branches de feature
compatibility: Requer a estrutura de projeto do Spec Kit com o diretório .specify/
metadata:
  author: spec-kit-core
  source: extension:git
---

# Skill Git de Validação

# Validar Branch de Feature

Valide se a branch Git atual segue as convenções esperadas de nomenclatura para branches de feature.

## Pré-requisitos

- Verifique se o Git está disponível executando `git rev-parse --is-inside-work-tree 2>/dev/null`
- Se o Git não estiver disponível, exiba um aviso e ignore a validação:
  ```
  [specify] Warning: Git repository not detected; skipped branch validation
  ```

## Regras de Validação

Obtenha o nome da branch atual:

```bash
git rev-parse --abbrev-ref HEAD
```

O segmento final do caminho do nome da branch deve começar com um destes marcadores de feature:

1. **Sequential**: `[0-9]{3,}-` (e.g., `001-feature-name`, `042-fix-bug`, `1000-big-feature`, `jdoe/web/008-guided-tour`)
2. **Timestamp**: `[0-9]{8}-[0-9]{6}-` (e.g., `20260319-143022-feature-name`, `jdoe/web/20260319-143022-feature-name`)

## Execução

Se estiver em uma branch de feature (corresponde a qualquer um dos padrões):
- Output: `✓ On feature branch: <branch-name>`
- Verifique se o diretório de spec correspondente existe em `specs/`:
  - Para branches sequenciais, procure `specs/<prefix>-*`, onde o prefixo corresponde à parte numérica, independentemente dos namespaces da branch
  - Para branches com timestamp, procure `specs/<prefix>-*`, onde o prefixo corresponde à parte `YYYYMMDD-HHMMSS`, independentemente dos namespaces da branch
- Se o diretório da spec existir: `✓ Spec directory found: <path>`
- Se o diretório da spec não existir: `⚠ No spec directory found for prefix <prefix>`

Se NÃO estiver em uma branch de feature:
- Output: `✗ Not on a feature branch. Current branch: <branch-name>`
- Exiba: `Feature branches should be named like: 001-feature-name, 20260319-143022-feature-name, or <namespace>/001-feature-name`

## Degradação Graciosa

Se o Git não estiver instalado ou o diretório não for um repositório Git:
- Verifique a variável de ambiente `SPECIFY_FEATURE` como alternativa
- Se estiver definida, valide o valor conforme os padrões de nomenclatura
- Se não estiver definida, ignore a validação e exiba um aviso
