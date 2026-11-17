# Modelo de dados: Portal de documentação do Dokpod

Não há banco de dados. As entidades são arquivos de conteúdo, metadados de build e
estado no navegador.

## Linha de documentação

| Campo | Origem | Regra |
|-------|--------|-------|
| `id` | `versions.json` ou `current` | `X.Y` (regex `^\d+\.\d+$`) ou `next` |
| `rotulo` | `docusaurus.config.js` (`versions.<id>.label`) | `0.7`: "0.7 (prévia)"; `next`: "Preview (não publicado)" |
| `caminho` | Derivado | `lastVersion` na raiz; demais em `/<id>/` |
| `estado` | Configuração | `preview` ou `publicada` (`demonstracao` saiu com as fixtures) |
| `banner` | Configuração | `unreleased` para `next` |
| `indexavel` | Configuração | `false` para `next` (fora do sitemap); `true` para linhas publicadas |

## Tópico

| Campo | Origem | Regra |
|-------|--------|-------|
| `docId` | Caminho do arquivo | Estável entre linhas e idiomas; kebab-case |
| `linha` | Diretório | Uma das linhas acima |
| `titulo`, `descricao` | Front matter | Obrigatórios; validados por `check-translations` |
| `traducoes` | Plugin `translation-status` | Mapa `locale → presente/ausente` |
| `obrigatoria` | `translation-policy.json` | Ausência de tradução obrigatória falha o build |

Transições: um tópico novo nasce em `pt-BR`. A tradução passa de `ausente` para
`presente` quando o arquivo correspondente existe em `i18n/<loc>/`.

## Preferência de leitura

| Campo | Armazenamento | Valores | Padrão |
|-------|---------------|---------|--------|
| Idioma escolhido | `localStorage["dokpod.docs.locale"]` | `pt-BR`, `en`, `es` | ausente → autodetecção na entrada neutra |
| Tema escolhido | `localStorage["altivy.docs.theme"]` | `dark`, `light`, `system` | ausente ou inválido → `dark` |
| Tema interno do Docusaurus | `localStorage["theme"]` | `dark`, `light` ou ausente | sincronizado por `theme-init.js` |

Valores inválidos são ignorados. Exceções de armazenamento não interrompem a
leitura. Nada é enviado à rede.

## Artefato do portal

| Campo | Onde | Regra |
|-------|------|-------|
| `site/` | Saída `build/` | Somente HTML, CSS, JS, fontes, imagens e índices de busca |
| `SHA256SUMS` | Raiz do artefato | Cobre todos os arquivos de `site/` |
| `portal-manifest.json` | Raiz do artefato | Ver [contracts/artefato.md](./contracts/artefato.md) |
