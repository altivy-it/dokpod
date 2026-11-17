# Contrato: rotas do portal

Base: `baseUrl = '/'` (o domínio alvo futuro é `docs.dokpod.altivy.it`).

| Rota | Conteúdo |
|------|----------|
| `/` | Índice da linha mais recente (`1.1`) em pt-BR. **Única entrada neutra.** |
| `/<docId>/` | Tópico da linha `1.1` em pt-BR |
| `/1.0/` e `/1.0/<docId>/` | Linha `1.0` em pt-BR |
| `/next/` e `/next/<docId>/` | Preview, `noindex` |
| `/en/...` e `/es/...` | Mesma estrutura por locale |
| `/<linha>/?topico-ausente=<docId>` | Índice da linha com aviso de tópico inexistente |

Todas as rotas terminam em `/` (`trailingSlash: true`); o servidor de teste
redireciona `rota` → `rota/` com 301. A busca não tem página própria: os
resultados aparecem no painel do campo da navbar (Pagefind, índice em `/pagefind/`).

## Regra da entrada neutra

1. Só se aplica quando `location.pathname === '/'` e não há `?locale=`.
2. Se `dokpod.docs.locale` existe e é válido, vale a escolha salva: redireciona
   para `/en/` ou `/es/`, ou permanece em `/` para `pt-BR`.
3. Senão, a primeira entrada de `navigator.languages` com prefixo `pt`, `en` ou
   `es` decide.
4. Sem correspondência, permanece em pt-BR.
5. O redirecionamento usa `location.replace`, para não gerar entrada extra no
   histórico.

Rotas de locale ou tópico explícitas (`/en/...`, `/1.0/...`, `/<docId>`) nunca
são redirecionadas.

## Troca de idioma e linha

- A troca de idioma preserva a linha e o `docId`. Se a tradução não existe, a
  página do locale mostra o aviso de tradução ausente com link para o tópico em pt-BR.
- A troca de linha preserva o `docId` quando ele existe na linha de destino.
  Caso contrário, abre `/<linha>/?topico-ausente=<docId>`.
