#!/usr/bin/env bash
# Pacote do agente Windows (spec 006 T003): Worker Service .NET 10 self-contained para win-x64, publicado em
# container (constituição: build em container) a partir de um `git archive` do commit atual, sem arquivos
# locais fora do Git. Gera o zip, o SHA-256 e um
# manifesto com commit e versão. O pacote não leva certificado, chave, endpoint nem identidade.
#
# Uso: publish-windows-agent.sh [pasta-de-saída] [rid]
# Saída: <pasta>/dokpod-agent-<versão>-<rid>.zip, .zip.sha256 e .json.
set -euo pipefail
root=$(git -C "$(dirname "$0")" rev-parse --show-toplevel)
out=${1:-$root/artifacts/windows-agent}
rid=${2:-win-x64}
sdk_image=docker.io/lzocateli/dotnet-sdk:10.0.402-noble-v1
engine=$(command -v podman || command -v docker)
commit=$(git -C "$root" rev-parse --short=12 HEAD)
version=$(sed -n 's:.*<VersionPrefix>\(.*\)</VersionPrefix>.*:\1:p' "$root/backend/Directory.Build.props" | head -1)
version=${version:-0.0.0}
name="dokpod-agent-$version-$rid"
mkdir -p "$out"
stage=$(mktemp -d)
trap 'rm -rf "$stage"' EXIT
mkdir -p "$stage/src" "$stage/publish"
git -C "$root" archive HEAD | tar -x -C "$stage/src"

"$engine" run --rm --userns keep-id --user "$(id -u):$(id -g)" -e HOME=/tmp -e DOTNET_CLI_HOME=/tmp -e DOTNET_NOLOGO=1 \
  -v "$stage/src":/src:z -v "$stage/publish":/out/publish:z -w /src "$sdk_image" \
  dotnet publish backend/apps/Dokpod.Agent/Dokpod.Agent.csproj --configuration Release --runtime "$rid" \
    --self-contained true -p:PublishSingleFile=false -p:IncludeSourceRevisionInInformationalVersion=false \
    -p:InformationalVersion="$version+$commit" --output /out/publish --verbosity quiet

[[ -f "$stage/publish/Dokpod.Agent.exe" ]] || { echo "Dokpod.Agent.exe não foi gerado" >&2; exit 1; }
cp "$stage/src/LICENSE" "$stage/publish/LICENSE.txt"
(cd "$stage/publish" && zip -qr -X "$out/$name.zip" .)
(cd "$out" && sha256sum "$name.zip" > "$name.zip.sha256")
printf '{"name":"%s","version":"%s","commit":"%s","rid":"%s","sha256":"%s"}\n' "$name" "$version" "$commit" "$rid" \
  "$(cut -d' ' -f1 "$out/$name.zip.sha256")" > "$out/$name.json"
echo "Pacote: $out/$name.zip"
cat "$out/$name.zip.sha256"
