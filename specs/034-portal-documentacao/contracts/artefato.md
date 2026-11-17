# Contrato: artefato do portal

Nome no CI: `docs-portal-site` (retenção de 14 dias). Gerado somente pelo job
`docs-portal`, sem secrets.

```text
docs-portal-site/
├── site/                  # saída estática do Docusaurus
├── SHA256SUMS             # sha256 de cada arquivo de site/, caminhos relativos
└── portal-manifest.json
```

## `portal-manifest.json`

```json
{
  "schema": 1,
  "product": "dokpod",
  "commit": "<sha do checkout>",
  "generator": { "docusaurus": "3.10.2", "node": "24.22.1" },
  "locales": ["pt-BR", "en", "es"],
  "versions": [
    { "id": "1.1", "state": "demonstracao", "path": "/" },
    { "id": "1.0", "state": "demonstracao", "path": "/1.0/" },
    { "id": "next", "state": "preview", "path": "/next/" }
  ],
  "translations": { "missing": [{ "version": "1.1", "locale": "en", "docId": "recursos-avancados" }] },
  "brand": { "source": "altivy@<commit>", "sha256sums": "<sha256 do SHA256SUMS do snapshot>" },
  "csp": { "scriptHashes": ["sha256-..."] }
}
```

## Regras verificadas por `check:artifact`

- Extensões permitidas: `.html`, `.css`, `.js`, `.json`, `.xml`, `.txt`, `.svg`,
  `.png`, `.ico`, `.woff2`, `.webmanifest`.
- São proibidos: `*.map`, `*.md`, `*.mdx`, `node_modules/`, `.docusaurus/`,
  `test-results/` e `playwright-report/`.
- Também são proibidas URLs absolutas de terceiros em `src`, `href` de
  `<link>`/`<script>` e `url()` de CSS.
- O `SHA256SUMS` deve conferir arquivo a arquivo.
