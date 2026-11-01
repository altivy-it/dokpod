# syntax=docker/dockerfile:1
# Spec 031 T061 (U4): variante Business do Dockerfile ao lado. O workflow gerenciado de imagens não aceita
# build-arg; este arquivo só muda o padrão de DOKPOD_EDITION e os rótulos (BusinessDockerfileTests impede outra divergência).
FROM lzocateli/dotnet-sdk:10.0.402-noble-v1 AS build
USER root
WORKDIR /src

COPY global.json ./
COPY backend/Directory.Build.props backend/Directory.Packages.props ./backend/
COPY contracts/agent/ contracts/agent/
# Spec 027 D-005: mensagens OTLP de logs compiladas no build.
COPY contracts/vendor/opentelemetry-proto/ contracts/vendor/opentelemetry-proto/
COPY backend/libs/Dokpod.Agent.Contracts/Dokpod.Agent.Contracts.csproj backend/libs/Dokpod.Agent.Contracts/
COPY backend/libs/Dokpod.ControlPlane.Application/Dokpod.ControlPlane.Application.csproj backend/libs/Dokpod.ControlPlane.Application/
COPY backend/libs/Dokpod.ControlPlane.Infrastructure/Dokpod.ControlPlane.Infrastructure.csproj backend/libs/Dokpod.ControlPlane.Infrastructure/
COPY backend/libs/Dokpod.Domain/Dokpod.Domain.csproj backend/libs/Dokpod.Domain/
COPY backend/libs/Dokpod.Licensing/Dokpod.Licensing.csproj backend/libs/Dokpod.Licensing/
COPY backend/apps/Dokpod.ControlPlane.Api/Dokpod.ControlPlane.Api.csproj backend/apps/Dokpod.ControlPlane.Api/
RUN dotnet restore backend/apps/Dokpod.ControlPlane.Api/Dokpod.ControlPlane.Api.csproj

COPY backend/ backend/

FROM build AS dev

ENV ASPNETCORE_ENVIRONMENT=Development

ENTRYPOINT ["dotnet", "watch", "run", "--no-restore", "--project", "backend/apps/Dokpod.ControlPlane.Api/Dokpod.ControlPlane.Api.csproj"]

FROM build AS publish

# Spec 031 T061 (D-009): edição fixada no build. Vazio = CE; `--build-arg DOKPOD_EDITION=Business` compõe o licenciamento.
ARG DOKPOD_EDITION=Business
RUN dotnet publish backend/apps/Dokpod.ControlPlane.Api/Dokpod.ControlPlane.Api.csproj --configuration Release --no-restore --output /app/publish /p:UseAppHost=false ${DOKPOD_EDITION:+/p:DokpodEdition=$DOKPOD_EDITION}

FROM lzocateli/dotnet-aspnet:10.0.13-noble-v1 AS runtime
LABEL org.opencontainers.image.title="dokpod-api-business" \
    org.opencontainers.image.description="API do plano de controle Dokpod, edição Business (licenciamento offline, spec 031)." \
    org.opencontainers.image.source="https://github.com/altivy-it/dokpod" \
    org.opencontainers.image.documentation="https://github.com/altivy-it/dokpod/tree/main/deploy/e2e" \
    org.opencontainers.image.url="https://github.com/altivy-it/dokpod"
WORKDIR /app
USER root
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=publish /app/publish ./
# Spec 005 FR-008: licença do Dokpod dentro da imagem e no rótulo OCI.
LABEL org.opencontainers.image.licenses="AGPL-3.0-only"
COPY LICENSE /usr/share/licenses/dokpod/LICENSE
COPY contracts/vendor/opentelemetry-proto/LICENSE /usr/share/licenses/dokpod/opentelemetry-proto/LICENSE
USER $APP_UID
EXPOSE 7443 8080
HEALTHCHECK --interval=10s --timeout=3s --start-period=10s --retries=6 CMD curl --fail --silent http://127.0.0.1:8080/health/live || exit 1
ENTRYPOINT ["dotnet", "Dokpod.ControlPlane.Api.dll"]