# syntax=docker/dockerfile:1
# Spec 033 T024 (D-001, D-002, D-014, D-015): imagem do instalador. PowerShell 7.6 com Docker CLI e Compose v2, as
# bibliotecas .NET do estado SQLite e do YAML, o catálogo SQL e os scripts do Keycloak da mesma versão. Versões e hashes
# registrados em docs/distribuicao.md, seção "Instalador (spec 033)".

FROM lzocateli/dotnet-sdk:10.0.403-noble-v1 AS libraries
USER root
WORKDIR /src
COPY global.json ./
COPY deploy/installer/lib/ deploy/installer/lib/
RUN dotnet publish deploy/installer/lib/Dokpod.Installer.Libraries.csproj --configuration Release --output /out/lib \
    && rm -f /out/lib/Dokpod.Installer.Libraries.*

FROM lzocateli/dotnet-sdk:10.0.403-noble-v1 AS tools
USER root
ARG POWERSHELL_VERSION=7.6.9
ARG POWERSHELL_SHA256=1da6d6d6035d651f20e70ddee7c4c15ac6ab8b9fb40ee92240d91263f28a884b
ARG DOCKER_VERSION=29.9.2
ARG DOCKER_SHA256=9ac225999fd5e863f7694c728ef23d480616b1e5639328fa309befd4243d69eb
ARG COMPOSE_VERSION=v5.6.1
ARG COMPOSE_SHA256=47737f4236e6d3ccea7291995203f8f018058783b4c3a9469119811a0b5db8c0
WORKDIR /download
RUN curl --fail --silent --show-error --location --output powershell.tar.gz \
        "https://github.com/PowerShell/PowerShell/releases/download/v${POWERSHELL_VERSION}/powershell-${POWERSHELL_VERSION}-linux-x64.tar.gz" \
    && echo "${POWERSHELL_SHA256}  powershell.tar.gz" | sha256sum --check --strict \
    && mkdir -p /out/powershell && tar --extract --gzip --file powershell.tar.gz --directory /out/powershell \
    && chmod +x /out/powershell/pwsh \
    && curl --fail --silent --show-error --location --output docker.tgz \
        "https://download.docker.com/linux/static/stable/x86_64/docker-${DOCKER_VERSION}.tgz" \
    && echo "${DOCKER_SHA256}  docker.tgz" | sha256sum --check --strict \
    && mkdir -p /out/bin && tar --extract --gzip --file docker.tgz --strip-components 1 --directory /out/bin docker/docker \
    && curl --fail --silent --show-error --location --output docker-compose \
        "https://github.com/docker/compose/releases/download/${COMPOSE_VERSION}/docker-compose-linux-x86_64" \
    && echo "${COMPOSE_SHA256}  docker-compose" | sha256sum --check --strict \
    && mkdir -p /out/cli-plugins && install -m 0755 docker-compose /out/cli-plugins/docker-compose

FROM lzocateli/dotnet-aspnet:10.0.14-noble-v1 AS runtime
LABEL org.opencontainers.image.title="dokpod-installer" \
    org.opencontainers.image.description="Instalador guiado do Dokpod (spec 033): instala, atualiza e recria a stack em Docker ou Podman rootless." \
    org.opencontainers.image.source="https://github.com/altivy-it/dokpod" \
    org.opencontainers.image.documentation="https://github.com/altivy-it/dokpod/tree/main/deploy/installer" \
    org.opencontainers.image.url="https://github.com/altivy-it/dokpod" \
    org.opencontainers.image.licenses="AGPL-3.0-only"
USER root
COPY --from=tools /out/powershell /opt/microsoft/powershell/7
COPY --from=tools /out/bin/docker /usr/local/bin/docker
COPY --from=tools /out/cli-plugins/docker-compose /usr/local/lib/docker/cli-plugins/docker-compose
RUN ln -s /opt/microsoft/powershell/7/pwsh /usr/local/bin/pwsh
COPY --from=libraries /out/lib /opt/dokpod-installer/lib
COPY deploy/installer/src/ /opt/dokpod-installer/src/
COPY deploy/installer/templates/ /opt/dokpod-installer/templates/
COPY deploy/installer/sql/ /opt/dokpod-installer/sql/
# Catálogo SQL da mesma versão (D-013): bootstrap e verificações pelo psql, e `sql export`.
COPY backend/libs/Dokpod.ControlPlane.Infrastructure/Sql/ /opt/dokpod-installer/catalog/
COPY backend/libs/Dokpod.ControlPlane.Infrastructure/Migrations/Script/ /opt/dokpod-installer/catalog/Migrations/Script/
# Fonte única do contrato do realm (D-001): os scripts de tools/scripts, sem cópia no repositório.
COPY tools/scripts/configure-keycloak.ps1 tools/scripts/provision-environment-authorization.ps1 /opt/dokpod-installer/keycloak/
COPY LICENSE /usr/share/licenses/dokpod/LICENSE
ENV DOKPOD_INSTALLER_HOME=/opt/dokpod-installer \
    POWERSHELL_TELEMETRY_OPTOUT=1 \
    POWERSHELL_UPDATECHECK=Off \
    DOCKER_CLI_HINTS=false
# Usuário não root por padrão. Com engine rootless, o install.sh executa com --user 0:0, que é o próprio usuário do host
# (o root do container é mapeado para ele) e não dá privilégio no host; sem isso, os arquivos de installDir e secretsDir
# ficariam com um UID de subuid.
USER $APP_UID
ENTRYPOINT ["pwsh", "-NoLogo", "-NoProfile", "-NonInteractive", "-File", "/opt/dokpod-installer/src/Dokpod.Installer.ps1"]

FROM runtime AS test
USER root
ARG PESTER_VERSION=6.2.0
ARG PSSCRIPTANALYZER_VERSION=1.25.0
RUN pwsh -NoProfile -Command "\$ProgressPreference = 'SilentlyContinue'; \
        Install-Module Pester -RequiredVersion ${PESTER_VERSION} -Scope AllUsers -Force -SkipPublisherCheck; \
        Install-Module PSScriptAnalyzer -RequiredVersion ${PSSCRIPTANALYZER_VERSION} -Scope AllUsers -Force"
COPY deploy/installer/tests/ /opt/dokpod-installer/tests/
COPY deploy/installer/install.sh /opt/dokpod-installer/install.sh
WORKDIR /opt/dokpod-installer
ENTRYPOINT ["pwsh", "-NoLogo", "-NoProfile", "-Command", "Invoke-Pester -Path /opt/dokpod-installer/tests -Output Detailed -CI"]
