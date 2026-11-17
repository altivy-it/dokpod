# Snapshot da marca Altivy

Cópia fiel, sem edição, dos artefatos aprovados do design system Altivy. O portal
não lê o repositório da Altivy durante o build nem em runtime.

- **Origem**: repositório Altivy, branch `main`, commit `55dfa317cd645837136da6bef5eaa5ad793abf23`.
- **Data da cópia**: 2026-11-17.

| Arquivo do snapshot | Origem na Altivy |
|---------------------|------------------|
| `tokens/tokens.css` | `docs/brand/tokens/tokens.css` (gerado de `docs/brand/design-system/tokens.json`) |
| `fonts/Outfit-Variable-Latin.woff2` | `docs/brand/fonts/Outfit-Variable-Latin.woff2` |
| `public/fonts/LICENSE-Outfit-1.1.txt` | `docs/brand/fonts/LICENSE-Outfit-1.1.txt` (SIL OFL 1.1) |
| `public/img/symbol-dark.svg` | `docs/brand/assets/dist/symbol-dark.svg` |
| `public/img/symbol-light.svg` | `docs/brand/assets/dist/symbol-light.svg` |
| `public/img/icon-32.png` | `docs/brand/assets/dist/icon-32.png` |

`SHA256SUMS` cobre todos os arquivos acima e é conferido por `npm run check:artifact`.
Os hashes da fonte, da licença e dos símbolos coincidem com os manifests
`SHA256SUMS` da Altivy no commit de origem.

## Como atualizar

1. Na Altivy, confirme a nova versão aprovada em `main`.
2. Copie os arquivos da tabela com `git -C <altivy> show main:<origem> > <destino>`.
3. Confira cada hash contra `docs/brand/fonts/SHA256SUMS` e
   `docs/brand/assets/dist/SHA256SUMS` da Altivy.
4. Regere `SHA256SUMS` nesta pasta (`sha256sum` dos arquivos, ordenados) e atualize
   o commit e a data acima.
5. Rode o quickstart da feature 034 e revise o contraste nos dois temas.

## Desvios locais

- O Docusaurus usa as próprias variáveis `--ifm-*`. `src/css/custom.css` só faz
  o mapeamento delas para os tokens Altivy; nenhuma cor nova foi criada.
- Liquid glass aparece só na barra de navegação e nos menus suspensos, que são as
  superfícies elevadas do portal.
