# Avaliação de Bug: candidatas publicam binários com a versão da estável anterior

- **Slug**: versao-candidata-binarios
- **Criada em**: 2026-11-04
- **Origem**: texto colado (pendência D11, registrada na conclusão da spec 024 em 2026-11-02; autorizada pelo mantenedor em 2026-11-02: "Sim, siga como sugerido")
- **Veredito**: valid
- **Severidade**: medium

## Relato (literal ou resumido)

> D11 — candidatas vX.Y.Z-rc.N publicam imagens (dokpod-api, -bff, -web, -agent, -api-business) e o pacote
> Windows do agente com a versão anterior nos binários: o publish usa o VersionPrefix do commit da tag
> (ainda o da última estável), e o agente/BFF/API reportam essa versão (AssemblyInformationalVersion),
> afetando N/N-1 e a tela de configurações.

## Sintoma

A candidata `v0.4.0-rc.1` é marcada no commit-base do Release PR, onde `backend/Directory.Build.props`
(`VersionPrefix`), `frontend/web/package.json` e `CONSOLE_VERSION` ainda valem a última estável (`0.3.2`).
As imagens recebem a tag `0.4.0-rc.1` e o rótulo OCI correto, mas os binários dentro delas e o pacote
Windows (`dokpod-agent-0.3.2-win-x64.zip`) reportam `0.3.2`. Esperado: binários, console e pacote com a
versão da tag (`0.4.0-rc.1` na candidata, `0.4.0` na estável).

## Reprodução

1. Com o Release PR da 0.4.0 aberto, dispare `gw-preview.yml` na `main` (gera `v0.4.0-rc.1`).
2. Em `quality`, abra Configurações: a versão da API, do BFF, do console e dos agentes aparece como `0.3.2`.
3. Baixe o asset Windows da prerelease: o nome e o manifesto JSON indicam `0.3.2`.

A reprodução é por leitura do código (os caminhos abaixo); não houve candidata publicada desde a
spec 024 para observar em `quality`.

## Caminhos de Código Suspeitos

- `backend/Directory.Build.props:11` — `VersionPrefix` é a única fonte da versão dos assemblies; o release-please só o atualiza no Release PR, depois da candidata.
- `backend/apps/Dokpod.ControlPlane.Api/Dockerfile:31`, `Business.Dockerfile:33`, `backend/apps/Dokpod.Bff/Dockerfile:19`, `deploy/agent/Dockerfile:36-40` — `dotnet publish` sem versão explícita.
- `frontend/web/src/app/features/settings/settings-api.service.ts:16` — `CONSOLE_VERSION` fixo, gerido pelo release-please (`x-release-please-version`).
- `frontend/web/Dockerfile` — build do console sem receber a versão.
- `deploy/agent/publish-windows-agent.sh:16` — lê `VersionPrefix` do props para o nome do zip, o manifesto e `InformationalVersion`.
- `.github/workflows/publish.yml` (job `build-assets`) — não repassa `inputs.version` ao script.
- `tools/scripts/ci/Publish-ContainerImage.ps1:125` (gerenciado pelo Altivy.Groundwork) — `docker buildx build` só aplica rótulos; não passa a versão como `--build-arg`, então o Dockerfile não tem como conhecê-la.
- Consumidores (somente exibição e agrupamento, sem parsing): `AgentControlWorker.cs:861`, `BffHost.cs:166`, `SettingsEndpoints.cs:34` e `:97`.

## Hipótese de Causa Raiz

A estratégia de release preview (spec 024) marca a candidata antes de o release-please aplicar a nova
versão aos arquivos; o pipeline continuou derivando a versão dos arquivos do commit em vez da tag. O
PixelPointer e o DotnetScope não têm o problema porque calculam `VersionPrefix`/`VersionSuffix` a partir
da tag no `publish.yml`. Confiança: high.

## Correção Proposta

**Preferencial**: a versão da tag passa a ser a fonte dos binários publicados, sem mudar os arquivos
versionados:

1. **Altivy.Groundwork** (catálogo `publish-containers`): `Publish-ContainerImage.ps1` passa
   `--build-arg RELEASE_VERSION=<versão da tag>`; argumento não declarado é ignorado pelo BuildKit, logo
   compatível com projetos que não o usam. Sai numa versão de correção do groundwork e é aplicado ao dokpod.
2. **Dockerfiles .NET** (API, API Business, BFF, agente): `ARG RELEASE_VERSION=` vazio por padrão; quando
   presente, `dotnet publish` recebe `VersionPrefix` (parte `X.Y.Z`) e `VersionSuffix` (parte após o
   primeiro `-`). Sem o argumento, o comportamento local fica igual (versão do props).
3. **Dockerfile do console**: com `RELEASE_VERSION`, substitui o valor de `CONSOLE_VERSION` antes do
   `npm run build`.
4. **Pacote Windows**: `publish-windows-agent.sh` aceita a versão (terceiro argumento opcional, validada
   como SemVer `X.Y.Z[-rc.N]`) e a usa no nome do zip, no manifesto e em `VersionPrefix`/`VersionSuffix`;
   `publish.yml` repassa `inputs.version`.

Os itens 2–4 são independentes do 1: entram já no dokpod, e as imagens passam a sair corretas assim que o
groundwork com o item 1 for aplicado.

**Alternativas**:
- Fazer o release-please gravar a versão antes da candidata (commit na `main` por candidata): contraria a
  estratégia, que exige que a estável seja o mesmo código da candidata validada, com diff só no Release PR.
- Construir as imagens num job próprio do `publish.yml` em vez do reutilizável gerenciado: duplica
  lógica de publicação única, atestação e entradas do manifesto.

**Arquivos Prováveis de Alteração**:
- `backend/apps/Dokpod.ControlPlane.Api/Dockerfile`, `Business.Dockerfile`
- `backend/apps/Dokpod.Bff/Dockerfile`
- `deploy/agent/Dockerfile`, `deploy/agent/publish-windows-agent.sh`
- `frontend/web/Dockerfile`
- `.github/workflows/publish.yml`
- groundwork: `catalog/components/publish-containers/files/tools/scripts/ci/Publish-ContainerImage.ps1` e teste do catálogo

**Testes a Adicionar ou Atualizar**:
- Build local da imagem do agente com e sem `--build-arg RELEASE_VERSION=0.4.0-rc.1` e leitura do
  `AssemblyInformationalVersion` da DLL publicada (`0.4.0-rc.1+…` e `0.3.2+…`).
- Build do console com o argumento e conferência do valor no bundle.
- `publish-windows-agent.sh` com versão `0.4.0-rc.1`: nome do zip, manifesto e versão do executável;
  versão inválida recusada.
- Groundwork: teste do catálogo que confere o `--build-arg RELEASE_VERSION` no dry-run/script.

## Riscos e Considerações

- `VersionSuffix` `rc.1` gera `AssemblyVersion`/`FileVersion` `0.4.0.0`, sem sufixo, e
  `InformationalVersion` `0.4.0-rc.1+<sha>`; os consumidores cortam em `+` e só exibem/agrupam. O teste
  de arquitetura da spec 022 (versões iguais nos arquivos) não é afetado, porque os arquivos não mudam.
- O valor vem de input de workflow; validar o formato antes de usar no shell evita injeção.
- Sem o groundwork corrigido, as imagens continuam com a versão anterior; só o pacote Windows fica
  correto. Registrar isso no PR.

## Perguntas em Aberto

- Nenhuma bloqueante.
