# Correção de Bug: candidatas publicam binários com a versão da estável anterior

- **Slug**: versao-candidata-binarios
- **Corrigido em**: 2026-11-04
- **Avaliação**: ./assessment.md
- **Estado**: applied

## Resumo

A versão da tag passa a ser a fonte dos binários publicados: os Dockerfiles aceitam `RELEASE_VERSION`
(passado pelo `Publish-ContainerImage.ps1` do Altivy.Groundwork) e o pacote Windows recebe
`inputs.version` do `publish.yml`. Sem o argumento, o build local continua com a versão do
`VersionPrefix`.

## Alterações

| Arquivo | Alteração | Notas |
|------|--------|-------|
| `backend/apps/Dokpod.ControlPlane.Api/Dockerfile` | modificado | `ARG RELEASE_VERSION`; `/p:Version` quando presente |
| `backend/apps/Dokpod.ControlPlane.Api/Business.Dockerfile` | modificado | idem |
| `backend/apps/Dokpod.Bff/Dockerfile` | modificado | idem |
| `deploy/agent/Dockerfile` | modificado | idem |
| `frontend/web/Dockerfile` | modificado | substitui `CONSOLE_VERSION` antes do build e confere a troca |
| `deploy/agent/publish-windows-agent.sh` | modificado | terceiro argumento opcional `versão`, validado como SemVer; usado no nome, no manifesto e em `-p:Version` |
| `.github/workflows/publish.yml` | modificado | `build-assets` repassa `inputs.version` por variável de ambiente |
| `docs/distribuicao.md` | modificado | registra a origem da versão do pacote e das imagens |
| groundwork `catalog/components/publish-containers/files/tools/scripts/ci/Publish-ContainerImage.ps1` | modificado (outro repositório) | `--build-arg RELEASE_VERSION=<versão da tag>` no build e no dry-run |
| groundwork `tests/Altivy.Groundwork.Tests/CiScriptTests.cs` | teste adicionado (outro repositório) | `Container_publish_passes_the_tag_version_as_build_arg` |

## Destaques do Diff (opcional)

```dockerfile
ARG RELEASE_VERSION=
RUN case "$RELEASE_VERSION" in *[!0-9A-Za-z.-]*) echo "RELEASE_VERSION inválida" >&2; exit 1 ;; esac \
    && dotnet publish ... ${RELEASE_VERSION:+/p:Version=$RELEASE_VERSION}
```

O `case` recusa qualquer caractere fora de `[0-9A-Za-z.-]`, o que impede `;` (separador de
propriedades do MSBuild) e metacaracteres de shell. `/p:Version` com sufixo gera
`AssemblyVersion`/`FileVersion` `X.Y.Z.0` e `InformationalVersion` `X.Y.Z-rc.N`.

## Testes Adicionados ou Atualizados

- groundwork `CiScriptTests.Container_publish_passes_the_tag_version_as_build_arg`: com `docker` simulado,
  o build de `v1.5.0-rc.2` recebe `--build-arg RELEASE_VERSION=1.5.0-rc.2`; falha sem a correção.
- dokpod: nenhum teste automatizado novo. Os Dockerfiles e o script de empacotamento não têm suíte própria
  e foram validados por build real (abaixo).

## Verificação Local

- `podman build --target publish --build-arg RELEASE_VERSION=0.4.0-rc.1` do agente, da API, da API Business
  e do BFF → DLL com `InformationalVersion` `0.4.0-rc.1` e `AssemblyVersion` `0.4.0.0`.
- Mesmo build do agente sem o argumento → `0.3.2` (comportamento anterior preservado).
- `RELEASE_VERSION='1.0.0;Foo=bar'` → build recusado com "RELEASE_VERSION inválida".
- Build do console com o argumento → bundle de produção contém `0.4.0-rc.1`.
- `publish-windows-agent.sh <saída> win-x64 0.4.0-rc.1` → `dokpod-agent-0.4.0-rc.1-win-x64.zip`, manifesto
  com `"version":"0.4.0-rc.1"` e `Dokpod.Agent.dll` com `0.4.0-rc.1+<commit>`; versão `x;y` → saída 2.
- groundwork: `dotnet test` da classe `CiScriptTests` em container → 36 aprovados; o teste novo falha com o
  script anterior.

## Desvios da Avaliação

- A avaliação previa `VersionPrefix`/`VersionSuffix` separados; usei `/p:Version=<versão>`, que o SDK
  decompõe sozinho (`GetAssemblyVersion`) e evita parsing de sufixo no shell. Resultado conferido nos builds.
- A parte do groundwork fica em outro repositório e segue em PR próprio; o dokpod só recebe o argumento
  depois de aplicar a versão do groundwork que o contém.

## Acompanhamento

- Lançar a versão de correção do groundwork e aplicá-la ao dokpod (`gw-container-images.yml` não muda;
  muda só o `Publish-ContainerImage.ps1`).
- Conferir na próxima candidata (`v0.4.0-rc.1`) a versão em Configurações em `quality` e o nome do asset
  Windows.
- `publish-windows-agent.sh` não oferece `--help` (regra de scripts); fica para quando o script for
  refatorado.
