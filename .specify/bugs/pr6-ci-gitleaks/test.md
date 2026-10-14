# Bug Verification: Falhas de CI e falsos positivos no PR 6

- **Slug**: pr6-ci-gitleaks
- **Tested**: 2026-10-06
- **Assessment**: ./assessment.md
- **Fix**: ./fix.md
- **Result**: verified

## Summary

As reproducoes locais dos tres defeitos avaliados passaram apos a remediacao.
Os testes backend e frontend nao apresentaram regressoes. Este resultado e
restrito ao escopo avaliado; nao declara o PR inteiro aprovado ou desbloqueado.

## Checks Performed

| Check | Command / Action | Result | Notes |
| ------- | ------------------ | -------- | ------- |
| Backend sem assets | Restore da solucao e metadata EF em container | pass | Ausencia de NETSDK1004 apos restore. |
| Migrations reais | Passo dotnet ef database update da CI com PostgreSQL efemero | pass | 12 migrations aplicadas, sem secrets externos. |
| Regressao backend | `dotnet test Dokpod.slnx --configuration Release --verbosity minimal` | pass | 206 testes passaram, nenhum falhou ou foi ignorado. |
| Dependencias frontend | `npm ci --no-audit --no-fund` no container da CI | pass | Lockfile preservado, usuario 0 como no workflow. |
| Cliente gerado | Duas geracoes e comparacao SHA-256 dos arquivos | pass | Saida identica, sem alterar OpenAPI. |
| Guard frontend | `npm run generate:api` e `git -c safe.directory=/workspace diff --exit-code` em container | pass | Indice preparado com a remediacao; nenhuma diferenca apos regenerar. |
| Regressao frontend | `ng test --watch=false` via alias containerizado | pass | 12 testes em cinco arquivos. |
| Build frontend | `ng build --configuration production --base-href /dokpod/` via alias | pass | Build concluido; aviso de CSS preexistente. |
| Regressao Gitleaks | `./tools/scripts/test-gitleaks.ps1` | pass | 26 casos, incluindo literais concatenados e regras padrao. |
| Historico completo | `git log --all --full-history -p --` enviado ao Gitleaks 8.30.1 por stdin | pass | Nenhum finding; exit code normal de bloqueio, redaction total. |
| Ajuda | `./tools/scripts/test-gitleaks.ps1 --help` | pass | Finalidade, dependencias e exemplos exibidos, sem fixtures ou Docker. |
| Sintaxe PowerShell | `System.Management.Automation.Language.Parser.ParseFile` | pass | Nenhum erro de parsing, sem executar o script. |
| Diagnosticos | Diagnosticos do editor nos arquivos alterados | pass | Nenhum erro. |
| Integridade do patch | `git diff --check` e `git diff --cached --check` | pass | Sem erros de whitespace, incluindo arquivos novos. |
| CI remota | Nova execucao dos workflows apos push | not-run | Resultado remoto ainda nao observado neste registro. |

## Output Excerpts

```text
Backend: 206 passed, 0 failed, 0 skipped
Test Files  5 passed (5)
Tests  12 passed (12)
Application bundle generation complete.
Gitleaks: 26 casos passaram; literais bloqueados e referencias runtime aceitas.
INF no leaks found
PowerShell syntax: pass
```

## Residual Risks

- O scan historico inclui todas as refs locais, inclusive branches de backup.
  A nova revisao deve ser escaneada novamente antes de publicar.
- Os checks remotos dependem de uma nova execucao; nao se inferiu seu resultado
  a partir das validacoes locais.
- O job Images continua fora do escopo, com vulnerabilidades criticas ja
  identificadas. Result depende desse job, alem de Backend e Frontend.
- Aprovacoes humanas e Code Owner continuam obrigatorios; nenhuma protecao foi alterada.
- CSS de container-list.page excede o budget de aviso em 34 bytes, sem falhar o build.
- Nenhuma credencial real foi confirmada. Historico nao foi reescrito e nenhum
  finding foi aceito por exclusao de arquivo ou commit.
- Container PostgreSQL e rede temporarios foram removidos; nenhum deploy ou merge ocorreu.

## Recommendation

Considerar verificada a remediacao local de pr6-ci-gitleaks e confirmar os
checks Backend, Frontend e Full History apos publicacao. Manter o merge
bloqueado enquanto Images ou as aprovacoes obrigatorias nao forem satisfeitos.
