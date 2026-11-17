# Portal de documentação do Dokpod

Manual versionado do Dokpod em Docusaurus 3.10, destinado a
`docs.dokpod.altivy.it`. Especificação: [spec 034](../../specs/034-portal-documentacao/spec.md).

> O conteúdo atual (linhas `1.0` e `1.1`) é **fixture de demonstração**: não descreve
> uma release do Dokpod e todas as páginas saem com `noindex`. O artefato
> `docs-portal-site` é gerado no CI, mas **não é publicado automaticamente**.

## O que o portal entrega

- **Versões**: `1.1` em `/`, `1.0` em `/1.0/`, preview `next` em `/next/`
  (banner + `noindex`). Trocar de versão mantém o tópico; se ele não existir no
  destino, a página inicial da versão mostra o aviso `?topico-ausente=<docId>`.
- **Idiomas**: `pt-BR` (raiz), `en` (`/en/`) e `es` (`/es/`). Só a entrada neutra
  `/` escolhe pelo navegador (`locale-entry.js`); links profundos nunca
  redirecionam. A escolha manual fica em `localStorage["dokpod.docs.locale"]`.
  Tradução ausente mostra aviso, link para o português e `noindex`.
- **Tema**: menu Claro/Escuro/Sistema (`localStorage["altivy.docs.theme"]`);
  sem escolha, escuro. Tokens, fonte Outfit e símbolos vêm do snapshot da marca
  Altivy em `src/brand/` (origem e hashes em `src/brand/SOURCE.md`).
- **Busca**: Pagefind (local, WebAssembly), índice por idioma e filtro pela
  versão aberta. Nenhuma requisição sai da origem.
- **CSP estrita**: sem `style=""` nem recurso externo (o plugin `strict-csp`
  reprova o build). Único script inline é o de cor do Docusaurus, liberado por
  hash (`portal-manifest.json`). A busca exige `'wasm-unsafe-eval'`.

## Comandos (sempre em container)

Nunca rode `node`/`npm` no host. Veja o
[quickstart](../../specs/034-portal-documentacao/quickstart.md) para os comandos
completos. Scripts disponíveis:

| Script | Uso |
|--------|-----|
| `npm run check:translations` | Front matter obrigatório e política de tradução (`translation-policy.json`) |
| `npm run build` | Build dos três idiomas + índice Pagefind em `build/` |
| `npm run build:manifest` | Empacota `artifacts/docs-portal-site/` (`site/`, `SHA256SUMS`, `portal-manifest.json`) |
| `npm run check:artifact` | Extensões, terceiros, `SHA256SUMS` e snapshot da marca |
| `npm run serve:test` | Servidor estático com a CSP de produção (`SITE_DIR`, `PORT`, `HOST`) |
| `npm run test:e2e` | Playwright: versões, idioma, tema, busca, axe, marca e CSP em 320/768/1280 px |

## Artefato `docs-portal-site`

O job `docs-portal` do CI gera o artefato `docs-portal-site` (retenção de 14
dias, sem secrets): o site estático em `site/`, o `SHA256SUMS` de cada arquivo e
o `portal-manifest.json` (commit, versões, idiomas, traduções ausentes, snapshot
da marca e hashes CSP). O job `docs-portal-e2e` testa esse mesmo artefato. Ele
**não é publicado automaticamente** nem entra nos assets da release; publicar em
`docs.dokpod.altivy.it` é uma etapa separada, com aprovação explícita.

## Estrutura

```text
docs/ · versioned_docs/ · versioned_sidebars/ · versions.json   conteúdo por linha
i18n/{en,es}/                                                   traduções e rótulos
src/brand/                                                      snapshot da marca Altivy
src/css/custom.css                                              --ifm-* mapeados para tokens
src/theme/ · src/components/ · src/hooks/                       componentes ejetados
static/js/                                                      theme-init, locale-entry, search-loader
plugins/translation-status · plugins/strict-csp                 plugins locais
scripts/ · tests/                                               manifesto, verificações e e2e
```

## Nova versão e traduções

1. `npx docusaurus docs:version <x.y>` (em container) congela `docs/` na linha nova.
2. Acrescente a linha em `versions` de `docusaurus.config.js` e em
   `translation-policy.json` (tópicos opcionais por idioma).
3. Traduza em `i18n/<locale>/docusaurus-plugin-content-docs/version-<x.y>/` e
   rode `check:translations`.

## Atualizar a marca

Copie os arquivos do commit desejado da Altivy (`git show <commit>:<caminho>`),
atualize `src/brand/SOURCE.md` e regenere `src/brand/SHA256SUMS`. O
`check:artifact` reprova divergências entre o snapshot e o site.
