# Correção de Bug: dependências transitivas vulneráveis no portal de documentação

- **Slug**: portal-dependencias-vulneraveis
- **Corrigido em**: 2026-11-25
- **Avaliação**: ./assessment.md
- **Estado**: applied

## Resumo

O portal passou a forçar, por `overrides`, as versões corrigidas de `tinypool`, `serialize-javascript` e `uuid`. O
lockfile foi regenerado no container padrão do portal.

## Alterações

| Arquivo | Alteração | Notas |
|------|--------|-------|
| `docs/portal/package.json` | modificado | `overrides` com `serialize-javascript` 7.1.3, `tinypool` 2.1.2 e `uuid` 11.1.1, em versão exata |
| `docs/portal/package-lock.json` | modificado | as três versões novas; `randombytes`, que só o `serialize-javascript` 6 usava, saiu da árvore |

## Destaques do Diff (opcional)

```json
"overrides": {
  "serialize-javascript": "7.1.3",
  "tinypool": "2.1.2",
  "uuid": "11.1.1"
}
```

## Testes Adicionados ou Atualizados

- Nenhum teste novo, como previsto na avaliação: o build do portal e a suíte E2E existente cobrem a regressão.

## Verificação Local

- `npm install --package-lock-only` e `npm ci` em `docker.io/lzocateli/angular-cli:22.2.4-node24.22.1-bookworm-v1` → lockfile regenerado.
- `npm audit` → `found 0 vulnerabilities`.
- `npm ls tinypool serialize-javascript uuid` → `tinypool@2.1.2`, `serialize-javascript@7.1.3` e `uuid@11.1.1`, todos `overridden`.

## Desvios da Avaliação

- Nenhum. A entrada opcional de `/docs/portal` em `.github/dependabot.yml` ficou de fora para manter a mudança mínima.

## Acompanhamento

- Remover os `overrides` quando o Docusaurus aceitar essas versões nas próprias faixas.
- Avaliar, por decisão humana, incluir `/docs/portal` no `.github/dependabot.yml`.
