# Runbook: distribuição da imagem Business

Spec 031 T061, decisão U4 (2026-10-31, Origem: humano, lzocateli): a imagem Business da API vai ao **GHCR privado**
e, para clientes sem acesso ao registry ou em rede isolada, por **arquivo** (`docker save`). Agente, BFF e console são
os mesmos da CE.

## Publicação

- `publish.yml` publica `ghcr.io/altivy-it/dokpod-api-business:<X.Y.Z>` na mesma release da CE, a partir de
  `backend/apps/Dokpod.ControlPlane.Api/Business.Dockerfile` (mesmo Dockerfile da CE, com `DOKPOD_EDITION=Business`;
  `BusinessDockerfileTests` impede divergência).
- A imagem é atestada e assinada por cosign com a **mesma identidade** das imagens da CE (`publish.yml`, `docs/seguranca.md`);
  o digest entra no `release-manifest.json` da release.
- O trust store embarcado é o de `Licensing/TrustStore/` (hoje `dokpod-2026-1`). `test-licensing-image-contents.ps1 -Build`
  confere edição, chaves e ausência de emissor antes de cada release que mudar o trust store.
- O deploy dos ambientes da Altivy ignora a imagem: o hook só aplica os serviços mapeados no host.

## Visibilidade (fazer na primeira release que a publicar)

Na página do pacote (`https://github.com/orgs/altivy-it/packages/container/package/dokpod-api-business`) →
**Package settings**: confirme **Private** em *Danger Zone → Change visibility* e não dê acesso público. Clientes
recebem acesso de leitura individual (conta GitHub do cliente) ou o arquivo abaixo.

## Entrega por arquivo

Com o digest do `release-manifest.json`:

```bash
digest=sha256:...   # dokpod-api-business da release
docker pull ghcr.io/altivy-it/dokpod-api-business@${digest}
cosign verify ghcr.io/altivy-it/dokpod-api-business@${digest} \
  --certificate-identity-regexp '^https://github\.com/altivy-it/dokpod/\.github/workflows/publish\.yml@refs/(heads/main|tags/v[0-9]+\.[0-9]+\.[0-9]+.*)$' \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com
docker save ghcr.io/altivy-it/dokpod-api-business@${digest} -o dokpod-api-business-X.Y.Z.tar
sha256sum dokpod-api-business-X.Y.Z.tar > dokpod-api-business-X.Y.Z.tar.sha256
```

Entregue o `.tar`, o `.sha256` e o digest pelo canal do contrato. No cliente: conferir o SHA-256, `docker load -i`,
conferir que o `RepoDigests` da imagem carregada é o digest informado, e trocar só a imagem da API no compose.
