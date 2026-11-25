# Verificação de Bug: dependências transitivas vulneráveis no portal de documentação

- **Slug**: portal-dependencias-vulneraveis
- **Testado em**: 2026-11-25
- **Avaliação**: ./assessment.md
- **Correção**: ./fix.md
- **Resultado**: verified

## Resumo

O lockfile não trava mais nenhuma das versões vulneráveis, e o `npm audit` do portal sai limpo. O build e os 165
testes E2E do portal passam com as versões novas, nos mesmos containers do CI.

## Verificações Executadas

| Verificação | Comando / Ação | Resultado | Notas |
|-------|------------------|--------|-------|
| Reprodução após correção | versões de `tinypool`, `serialize-javascript` e `uuid` no `package-lock.json` | pass | 2.1.2, 7.1.3 e 11.1.1; antes, 1.1.1, 6.0.2 e 8.3.2 |
| Auditoria | `npm audit` após `npm ci` | pass | `found 0 vulnerabilities` |
| Árvore resolvida | `npm ls tinypool serialize-javascript uuid` | pass | as três versões aparecem como `overridden` |
| Build do portal | `npm run check:translations`, `npm run build`, `npm run build:manifest` e `npm run check:artifact` em `docker.io/lzocateli/angular-cli:22.2.4-node24.22.1-bookworm-v1` | pass | 3 idiomas e 39 páginas indexadas; `check:artifact ok: 257 arquivos conferidos`. O `build:manifest` precisou de `GITHUB_SHA` porque o container não tinha `.git`, como no CI |
| Suíte de regressão | `npm run test:e2e` em `mcr.microsoft.com/playwright:v1.65.1-noble`, com `SITE_DIR=artifacts/docs-portal-site/site` | pass | `165 passed` |
| Alertas do Dependabot | fechamento automático no GitHub | not-run | só acontece depois do merge na `main` |

## Trechos de Saída

```text
found 0 vulnerabilities
build:manifest ok: 257 arquivos, 1 hash(es) de script inline em artifacts/docs-portal-site
check:artifact ok: 257 arquivos conferidos
165 passed (29.3s)
```

## Riscos Residuais

- O `tinypool` 2 e o `serialize-javascript` 7 são majors que o Docusaurus 3.10.2 não declara. O build e o E2E passaram,
  mas o `docusaurus start` (servidor de desenvolvimento, onde roda o `sockjs`) não foi exercitado.
- Os alertas só fecham depois do merge.

## Recomendação

Encerre o bug: verificado com build e E2E nos containers do CI. Confirme o fechamento dos alertas 31 a 36 depois do
merge.
