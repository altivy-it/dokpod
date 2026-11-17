# Quickstart: validar o portal localmente

Todos os comandos rodam em container a partir da raiz do repositório. Nunca
execute `node` ou `npm` no host. Em hosts com Podman, `docker` é o alias compatível,
mas a imagem do Docker Hub precisa do prefixo `docker.io/`
(`docker.io/lzocateli/angular-cli:...`).

## 1. Build, verificações e artefato

```bash
docker run --rm --user 0 --volume "${PWD}:/work" --workdir /work/docs/portal \
  --mount type=volume,target=/work/docs/portal/node_modules \
  lzocateli/angular-cli:22.2.4-node24.22.1-bookworm-v1 \
  bash -lc "npm ci --no-audit --no-fund \
    && npm run check:translations \
    && npm run build \
    && npm run build:manifest \
    && npm run check:artifact"
```

Resultado esperado: `docs/portal/artifacts/docs-portal-site/` com `site/`,
`SHA256SUMS` e `portal-manifest.json`. Falhas esperadas, cada uma com diagnóstico:
link interno quebrado, tradução obrigatória ausente, fixture sem front matter ou
arquivo proibido no artefato.

## 2. Testes e2e, CSP e acessibilidade

```bash
docker run --rm --ipc=host --volume "${PWD}:/work" --workdir /work/docs/portal \
  --mount type=volume,target=/work/docs/portal/node_modules \
  --env SITE_DIR=artifacts/docs-portal-site/site \
  mcr.microsoft.com/playwright:v1.65.1-noble \
  bash -lc "npm ci --no-audit --no-fund && npm run test:e2e"
```

Os cenários cobrem:

- linha `1.0`/`1.1` com refresh e tópico ausente;
- `navigator.languages` em `pt-BR`, `en-US`, `es-MX` e `fr-FR`;
- link profundo sem redirecionamento;
- tradução ausente com aviso;
- Claro/Escuro/Sistema com `emulateMedia`;
- armazenamento bloqueado;
- busca restrita à linha;
- zero `securitypolicyviolation`;
- axe A/AA nos dois temas, em 320, 768 e 1280 px.

## 3. Servidor de desenvolvimento (opcional)

```bash
docker run --rm -it --user 0 -p 3000:3000 --volume "${PWD}:/work" --workdir /work/docs/portal \
  --mount type=volume,target=/work/docs/portal/node_modules \
  lzocateli/angular-cli:22.2.4-node24.22.1-bookworm-v1 \
  bash -lc "npm ci --no-audit --no-fund && npx docusaurus start --host 0.0.0.0 --locale en"
```

O `start` serve um único locale por vez e não gera o índice da busca. Para
navegar nos três idiomas com busca e a CSP de produção, sirva o artefato do passo 1:

```bash
docker run --rm -it --user 0 -p 4173:4173 --volume "${PWD}:/work" --workdir /work/docs/portal \
  --env SITE_DIR=artifacts/docs-portal-site/site --env HOST=0.0.0.0 \
  lzocateli/angular-cli:22.2.4-node24.22.1-bookworm-v1 \
  bash -lc "npm run serve:test"
```

## Registro de evidências

Registre no PR o comando, a saída resumida e o resultado de cada passo. Um passo
não executado é `NOT RUN` e não conta como sucesso.
