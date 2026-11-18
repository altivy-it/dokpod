# Portal de documentação do Dokpod

Manual versionado do Dokpod em Docusaurus 3.10, destinado a
`docs.dokpod.altivy.it`. Especificação: [spec 034](../../specs/034-portal-documentacao/spec.md).

> Primeira versão do manual: linhas `0.8` e `0.7` (prévias, publicadas e
> indexáveis, em PT/EN/ES) e `next` (o que está na `main` depois da 0.8, só em
> português e com `noindex`). O artefato
> `docs-portal-site` é gerado no CI e **só vai ao ar pelo workflow manual
> `docs-deploy.yml`**.

## O que o portal entrega

- **Versões**: `0.8` em `/`, `0.7` em `/0.7/` (banner de linha anterior) e
  preview `next` em `/next/` (banner + `noindex`, fora do sitemap). Trocar de versão mantém o tópico; se ele não existir no
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
- **Rodapé Altivy**: marca, resumo documental, landing do produto no idioma
  ativo, GitHub público do produto e releases, site/GitHub institucional,
  contato e licença. Idiomas são links HTML explícitos e funcionam sem JS.
  Metadados ficam em `customFields.portal` da configuração; rótulos e resumo
  em `src/theme/Footer/index.js` e `i18n/{en,es}/code.json`.
  Ao replicar em outro portal, conferir o slug e a existência da landing
  PT/EN/ES, a visibilidade do repositório e a licença: repositório privado
  usa GitHub da Altivy e não mostra releases; licença só com URL pública.
  O padrão da família é documentado no repositório Altivy em
  `docs/design/portais-documentacao-versionada.md` (seção Rodapé comum).

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
não entra nos assets da release nem é publicado automaticamente.

## Publicação em `docs.dokpod.altivy.it`

O workflow `.github/workflows/docs-deploy.yml` (só `workflow_dispatch`, ambiente
protegido `docs-production`) recebe o SHA de um commit da `main` e:

1. localiza a execução verde do CI daquele commit e baixa o `docs-portal-site`
   (não reconstrói);
2. confere o `SHA256SUMS` e se o manifesto é do commit pedido;
3. lê por SSH a CSP instalada na borda e aborta se faltar algum
   `csp.scriptHashes` do manifesto;
4. envia para `/userapps/altivy/docs/dokpod/releases/<sha>/` e troca os
   symlinks relativos `current`/`previous` de forma atômica (guarda 5 releases);
5. faz smoke em `/`, `/en/`, `/es/` e `/next/` (200 + CSP) e volta para
   `previous` se falhar.

O vhost, a CSP e o TLS pertencem ao repositório Altivy
(`deploy/nginx/docs-portals.conf` e `security-docs-dokpod-prd.conf`). Se o
build mudar o hash do script inline, atualize a CSP lá e publique a borda
**antes** de rodar este workflow.

Secrets do ambiente: `VPS_SSH_KEY`, `VPS_SSH_KNOWN_HOSTS`, `VPS_HOST`,
`VPS_USER`; variável opcional `VPS_SSH_PORT` (padrão 22).

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

## Validação do rodapé (2026-11-18)

- [x] Build PT/EN/ES, `check:translations`, manifesto e integridade (257 arquivos).
- [x] 165 testes E2E pela borda Nginx 1.28 em container com a CSP de produção:
  links localizados/públicos, navegação por teclado e foco, idiomas sem JS,
  axe A/AA e auditoria de marca nos dois temas.
- [x] Seis capturas adicionais do rodapé: claro/escuro em 320/768/1280 px,
  conferidas sem overflow; sem recursos novos de terceiros ou dependências.
- [x] Hash CSP preservado:
  `sha256-0xt7rjlfRsoD7aukiSWSNvMBWU159Lh5cl3WNaV1g+w=`.
- [ ] Revisão visual humana e publicação (não executadas nesta alteração).

Lighthouse não foi reexecutado neste recorte; não há nova mídia, dependência
ou carregamento externo. Os links externos são navegação, não sub-recursos.
