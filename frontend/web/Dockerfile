# syntax=docker/dockerfile:1
FROM lzocateli/angular-cli:22.2.4-node24.22.1-bookworm-v1 AS build
WORKDIR /src

COPY --chown=node:node frontend/web/package.json frontend/web/package-lock.json ./
RUN npm ci --no-audit --no-fund

COPY --chown=node:node frontend/web/ ./
COPY --chown=node:node contracts/openapi/ /contracts/openapi/

ARG BASE_HREF=/dokpod/

FROM build AS dev

ENV NG_CLI_ANALYTICS=false

ENTRYPOINT ["npm", "run", "start", "--", "--host", "0.0.0.0", "--port", "4200"]

FROM build AS publish

RUN npm run generate:api \
    && npm run build -- --configuration production --base-href ${BASE_HREF}

FROM lzocateli/nginx:1.28.0-bookworm-v1 AS runtime
LABEL org.opencontainers.image.title="dokpod-web" \
    org.opencontainers.image.description="Aplicação web estática do Dokpod." \
    org.opencontainers.image.source="https://github.com/altivy-it/dokpod" \
    org.opencontainers.image.documentation="https://github.com/altivy-it/dokpod/tree/main/deploy/e2e" \
    org.opencontainers.image.url="https://github.com/altivy-it/dokpod"
COPY deploy/nginx/conf.d/web.conf /etc/nginx/conf.d/default.conf
COPY --from=publish /src/dist/dokpod-web/browser/ /usr/share/nginx/html/
# Spec 005 FR-003: aplica as correções de segurança já publicadas pelo Debian sobre a base.
RUN apt-get update \
    && apt-get upgrade --yes --no-install-recommends \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && sed -i 's#^[[:space:]]*pid[[:space:]].*#pid /tmp/nginx.pid;#' /etc/nginx/nginx.conf \
    && chown -R nginx:nginx /usr/share/nginx/html /var/cache/nginx /var/log/nginx /var/run
# Spec 005 FR-008: licença do Dokpod dentro da imagem e no rótulo OCI.
LABEL org.opencontainers.image.licenses="AGPL-3.0-only"
COPY LICENSE /usr/share/licenses/dokpod/LICENSE
# Avisos de terceiros extraídos pelo build (o Angular os remove do JavaScript): na imagem e servidos com o console.
COPY --from=publish /src/dist/dokpod-web/3rdpartylicenses.txt /usr/share/licenses/dokpod/THIRD-PARTY-console.txt
COPY --from=publish /src/dist/dokpod-web/3rdpartylicenses.txt /usr/share/nginx/html/3rdpartylicenses.txt
USER nginx
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=3s --start-period=10s --retries=6 CMD curl --fail --silent http://127.0.0.1:8080/ || exit 1
