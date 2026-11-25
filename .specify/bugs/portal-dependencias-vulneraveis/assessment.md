# Avaliação de Bug: dependências transitivas vulneráveis no portal de documentação

- **Slug**: portal-dependencias-vulneraveis
- **Criada em**: 2026-11-25
- **Origem**: alertas abertos do Dependabot (`gh api repos/altivy-it/dokpod/dependabot/alerts`), números 31 a 36
- **Veredito**: valid
- **Severidade**: medium

## Relato (literal ou resumido)

Seis alertas abertos, todos em `docs/portal/package-lock.json`, todos em dependências transitivas:

| Alerta | Pacote | Versão travada | Advisory | Severidade | Corrigido em |
|--------|--------|----------------|----------|------------|--------------|
| 35 | `tinypool` | 1.1.1 | GHSA-5gmw-xhrv-c9v3: prototype pollution nas opções do worker leva a RCE | critical | 2.1.1 |
| 34 | `tinypool` | 1.1.1 | GHSA-85c8-ppgw-ccpr: prototype pollution nas opções de `run()` leva a RCE | critical | 2.1.2 |
| 31 | `serialize-javascript` | 6.0.2 | GHSA-5c6j-r48x-rmvq: RCE via `RegExp.flags` e `Date.prototype.toISOString()` | high | 7.0.3 |
| 33 | `serialize-javascript` | 6.0.2 | GHSA-qj8w-gfj5-8c6v: DoS de CPU com objetos semelhantes a array | medium | 7.0.5 |
| 36 | `serialize-javascript` | 6.0.2 | GHSA-m5mm-p8h7-m29v: XSS por `</script` no fim do corpo de função serializada | low | 7.1.3 |
| 32 | `uuid` | 8.3.2 | GHSA-w5hq-g745-h8pq: falta de verificação de limites do buffer em v3/v5/v6 | medium | 11.1.1 |

## Sintoma

O lockfile do portal trava versões com vulnerabilidades conhecidas, e o Dependabot mantém os alertas abertos. Espera-se
que o lockfile resolva versões corrigidas sem mudar o portal gerado.

## Reprodução

1. `jq '.packages["node_modules/tinypool"].version, .packages["node_modules/serialize-javascript"].version, .packages["node_modules/uuid"].version' docs/portal/package-lock.json` devolve `1.1.1`, `6.0.2` e `8.3.2`.
2. `npm audit` dentro do container do portal lista os mesmos advisories.

## Caminhos de Código Suspeitos

- `docs/portal/package.json` — não tem `overrides`; as versões vêm das faixas dos pacotes do Docusaurus.
- `node_modules/@docusaurus/core` → `tinypool ^1.0.2` (workers da geração estática). O `@docusaurus/core` mais recente (3.10.2, o já usado) ainda pede `^1.0.2`.
- `node_modules/copy-webpack-plugin` e `node_modules/css-minimizer-webpack-plugin` → `serialize-javascript ^6.0.x` (cache do webpack).
- `node_modules/sockjs` → `uuid ^8.3.2` (servidor de desenvolvimento do `docusaurus start`).
- `.github/dependabot.yml` — não tem entrada para `/docs/portal`; por isso nenhum PR automático foi aberto.

## Hipótese de Causa Raiz

Os três pacotes só rodam no build e no servidor de desenvolvimento do portal; o site publicado é estático e não leva
nenhum deles. As faixas declaradas pelo Docusaurus e pelos plugins do webpack não alcançam as versões corrigidas, que
são majors novos, então só um `overrides` resolve sem esperar o upstream. A exploração exigiria entrada controlada pelo
atacante nas opções do pool, nos objetos serializados ou no buffer do `uuid`, o que o build não recebe; daí a
severidade `medium` no projeto, apesar de `critical` no advisory. Confiança: high.

## Correção Proposta

**Preferencial**: adicionar `overrides` em `docs/portal/package.json` com versões exatas e corrigidas
(`tinypool` 2.1.2, `serialize-javascript` 7.1.3 e `uuid` 11.1.1), regenerar o `package-lock.json` com `npm install`
no container padrão do portal e validar com os mesmos passos do CI (`npm ci`, `check:translations`, `build`,
`build:manifest`, `check:artifact` e o E2E Playwright). As três versões são MIT ou BSD-3-Clause e aceitam o Node 24 do
container. O `tinypool` 2 é só ESM; o Node 24 carrega ESM por `require`, mas a geração estática precisa ser validada
pelo build.

**Alternativas**:
- Esperar o upstream do Docusaurus: sem data, e os alertas continuam abertos.
- Fechar os alertas como "risco tolerável" por serem de build: deixa versões vulneráveis travadas sem necessidade.

**Arquivos Prováveis de Alteração**:
- `docs/portal/package.json`
- `docs/portal/package-lock.json`
- `.github/dependabot.yml` (opcional: incluir `/docs/portal` para que futuras correções cheguem por PR)

**Testes a Adicionar ou Atualizar**:
- Nenhum teste novo: o build do portal e a suíte E2E existente (versões, idioma, tema, busca, a11y e CSP) cobrem a
  regressão. A evidência da correção é o lockfile com as versões novas e o `npm audit` sem esses advisories.

## Riscos e Considerações

- `tinypool` 2 e `serialize-javascript` 7 são majors novos; o Docusaurus não foi testado upstream com eles. O build
  completo e o E2E são o gate.
- `overrides` precisa ser revisto quando o Docusaurus subir as faixas, para não fixar versões antigas.
- Nenhum impacto no produto distribuído: o portal não entra nas imagens.

## Perguntas em Aberto

- Nenhuma.
