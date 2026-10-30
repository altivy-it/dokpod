# CLAUDE.md

Orientação para o Claude Code neste repositório.

As regras do projeto (pt-BR, contexto obrigatório, estrutura do monorepo,
execução em containers, segurança e restrições de publicação) ficam num único
lugar, compartilhado com o Copilot:

@.github/copilot-instructions.md

## Automação do Claude Code

- Skills: `.claude/skills` é um symlink para `.github/skills`, a mesma fonte do
  Copilot. Inclui o fluxo do Spec Kit (`/speckit-specify`, `/speckit-plan`,
  `/speckit-tasks`, `/speckit-implement`, `/speckit-bug-*` e afins) e as skills
  `dokpod-*`, `pull-request-review` e `release-readiness`.
- Hooks de extensões do Spec Kit em `.specify/extensions.yml`.

Ao mudar a estrutura de `.github/copilot-instructions.md`, `.github/instructions`
ou `.github/skills`, mantenha este arquivo coerente.
