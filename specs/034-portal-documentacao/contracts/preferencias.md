# Contrato: preferências locais

As preferências ficam somente no `localStorage` do domínio do portal. Não são
compartilhadas com a landing (`altivy.it`) e nunca são transmitidas.

| Chave | Valores | Escrito por | Lido por |
|-------|---------|-------------|----------|
| `dokpod.docs.locale` | `pt-BR`, `en`, `es` | Wrapper do seletor de idioma (clique explícito) | `static/js/locale-entry.js` |
| `altivy.docs.theme` | `dark`, `light`, `system` | Menu de tema (eject de `ColorModeToggle`) | `static/js/theme-init.js` |
| `theme` | `dark`, `light` ou ausente | Docusaurus e `theme-init.js` | Script de cor do Docusaurus |

## Precedência

- **Idioma**: URL explícita > `dokpod.docs.locale` > `navigator.languages` > `pt-BR`.
- **Tema**: `altivy.docs.theme` > padrão `dark`. No modo `system`, o tema efetivo
  segue `prefers-color-scheme` e acompanha mudanças ao vivo. Nos modos `dark` e
  `light`, mudanças do sistema não têm efeito.

## Robustez

- `getItem` e `setItem` ficam em `try/catch`. Com o armazenamento indisponível, o
  idioma é decidido pela URL e pelo navegador. O tema segue `prefers-color-scheme`
  (comportamento do script de cor do Docusaurus com `respectPrefersColorScheme`),
  e a página continua legível.
- Um valor fora da lista é tratado como ausente e não é regravado.
