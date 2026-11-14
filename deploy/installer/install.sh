#!/bin/sh
# Instalador do Dokpod (spec 033 T024, D-001, D-010, D-015): bootstrap POSIX. Não tem lógica de instalação. Detecta o
# engine rootless, coleta fatos do host que o container não enxerga, verifica a imagem do instalador pelo digest gravado
# aqui na publicação e a executa com o socket do usuário, installDir, secretsDir e os arquivos citados nas respostas.
# Exceção registrada no ADR 2026-0011: único script POSIX fora de imagem, porque o host do usuário não tem PowerShell.
set -eu

# Substituídos pelo publish.yml (T051). Com o marcador, só uma imagem local de desenvolvimento é aceita.
INSTALLER_IMAGE_DEFAULT="ghcr.io/altivy-it/dokpod-installer@sha256:__DOKPOD_INSTALLER_DIGEST__"
COSIGN_IMAGE="ghcr.io/sigstore/cosign/cosign:v3.1.4@sha256:0f06154836d2de34f887a5c273506d52ca43edaa1d9e53e6510fa78a48f69c2d"
CERTIFICATE_IDENTITY='^https://github\.com/altivy-it/dokpod/\.github/workflows/publish\.yml@refs/(heads/main|tags/v[0-9]+\.[0-9]+\.[0-9]+.*)$'
OIDC_ISSUER='https://token.actions.githubusercontent.com'

usage() {
    cat <<'EOF'
Uso: sh install.sh [comando] [opções]

Instala, atualiza e recria o Dokpod em Docker ou Podman rootless (spec 033). Este script só prepara e executa a imagem
do instalador; os comandos e opções são os do instalador:

  sh install.sh help                 ajuda completa do instalador
  sh install.sh plan --answers <arquivo>
  sh install.sh install
  sh install.sh status

Pré-requisitos: Docker Engine >= 27 rootless ou Podman >= 5 rootless, com Compose v2, e XDG_RUNTIME_DIR definido.
Variáveis:
  DOKPOD_ENGINE=docker|podman   escolhe o engine quando os dois existem
  DOKPOD_INSTALLER_IMAGE        imagem alternativa; só localhost/... (desenvolvimento), sem verificação de assinatura

Documentação: docs/runbooks/instalacao.md
EOF
}

fail() {
    printf 'install.sh: %s\n' "$1" >&2
    exit "${2:-3}"
}

case "${1:-}" in
    --help|-h) usage; exit 0 ;;
esac

[ -n "${XDG_RUNTIME_DIR:-}" ] || fail 'XDG_RUNTIME_DIR não está definido; o instalador só usa engine rootless (D-015).'

engine="${DOKPOD_ENGINE:-}"
if [ -z "$engine" ]; then
    if [ -S "$XDG_RUNTIME_DIR/podman/podman.sock" ] && command -v podman >/dev/null 2>&1; then
        engine=podman
    elif [ -S "$XDG_RUNTIME_DIR/docker.sock" ] && command -v docker >/dev/null 2>&1; then
        engine=docker
    else
        fail "Nenhum socket rootless em $XDG_RUNTIME_DIR (podman/podman.sock ou docker.sock). Ative: systemctl --user enable --now podman.socket, ou dockerd-rootless-setuptool.sh install."
    fi
fi

case "$engine" in
    podman) socket="$XDG_RUNTIME_DIR/podman/podman.sock"; cli=podman ;;
    docker) socket="$XDG_RUNTIME_DIR/docker.sock"; cli=docker ;;
    *) fail "DOKPOD_ENGINE inválido: $engine (use docker ou podman)." 2 ;;
esac

[ -S "$socket" ] || fail "O socket $socket não existe."
# Resolve também um link simbólico no próprio socket: o alvo precisa estar em XDG_RUNTIME_DIR.
resolved=$(readlink -f "$socket" 2>/dev/null || true)
[ -n "$resolved" ] || resolved=$(cd "$(dirname "$socket")" && pwd -P)/$(basename "$socket")
case "$resolved" in
    "$XDG_RUNTIME_DIR"/*) ;;
    *) fail "O socket $resolved está fora de $XDG_RUNTIME_DIR; só o engine rootless do usuário é aceito." ;;
esac

# installDir, secretsDir e o arquivo de respostas, das opções do instalador.
install_dir="$HOME/.local/share/dokpod"
secrets_dir="$HOME/.config/dokpod/secrets"
answers=""
extra_dirs=""
previous=""
for argument in "$@"; do
    case "$previous" in
        --install-dir) install_dir="$argument" ;;
        --secrets-dir) secrets_dir="$argument" ;;
        --answers) answers="$argument" ;;
        --offline|--sql-dir|--out) extra_dirs="$extra_dirs $argument" ;;
    esac
    previous="$argument"
done

if [ -n "$answers" ]; then
    case "$answers" in /*) ;; *) fail "Informe o arquivo de respostas com caminho absoluto: $answers" 2 ;; esac
fi

for directory in "$install_dir" "$secrets_dir"; do
    case "$directory" in
        /*) ;;
        *) fail "O caminho $directory precisa ser absoluto." 2 ;;
    esac
done

mkdir -p "$install_dir" "$secrets_dir"
chmod 0700 "$secrets_dir"

image="${DOKPOD_INSTALLER_IMAGE:-$INSTALLER_IMAGE_DEFAULT}"
if [ -n "${DOKPOD_INSTALLER_IMAGE:-}" ]; then
    case "$image" in
        localhost/*) printf 'install.sh: aviso: imagem de desenvolvimento %s, sem verificação de assinatura.\n' "$image" >&2 ;;
        *) fail 'DOKPOD_INSTALLER_IMAGE só aceita imagem local (localhost/...); a imagem publicada é a gravada neste script.' 5 ;;
    esac
else
    case "$image" in
        # Marcador escrito em partes, para a substituição da publicação não alcançar esta verificação.
        *__DOKPOD_INSTALLER_"DIGEST"__*) fail 'Este install.sh não veio de uma release (digest do instalador ausente).' 5 ;;
    esac
    "$cli" run --rm --log-driver none "$COSIGN_IMAGE" verify \
        --certificate-identity-regexp "$CERTIFICATE_IDENTITY" --certificate-oidc-issuer "$OIDC_ISSUER" "$image" >/dev/null 2>&1 \
        || fail "A assinatura da imagem do instalador não confere; nada foi alterado." 5
fi

# Fatos do host que o container não enxerga (Prerequisites.psm1).
port_start=$(cat /proc/sys/net/ipv4/ip_unprivileged_port_start 2>/dev/null || true)
linger=$(loginctl show-user "$(id -un)" --property Linger --value 2>/dev/null || true)
listening=""
if command -v ss >/dev/null 2>&1; then
    listening=$(ss -Htln 2>/dev/null | awk '{ n = split($4, a, ":"); print a[n] }' | sort -un | paste -sd, - || true)
fi
free_bytes=$(df -P -B1 "$install_dir" 2>/dev/null | awk 'NR == 2 { print $4 }' || true)
host_name=$(uname -n 2>/dev/null || true)
# O nome público resolvido no host: o Docker não copia o /etc/hosts do host para o container do instalador.
public_name=""
public_addresses=""
if [ -n "$answers" ] && [ -f "$answers" ]; then
    public_name=$(sed -n 's#.*publicUrl"\{0,1\}[[:space:]]*:[[:space:]]*"\{0,1\}https://\([^:/"]*\).*#\1#p' "$answers" | head -n 1)
    if [ -n "$public_name" ] && command -v getent >/dev/null 2>&1; then
        public_addresses=$(getent ahosts "$public_name" 2>/dev/null | awk '{ print $1 }' | sort -u | paste -sd, - || true)
    fi
fi

# As opções do container entram antes da imagem e dos argumentos originais, que ficam intactos no fim de "$@".
set -- "$image" "$@"
set -- --rm --interactive --log-driver none --user 0:0 \
    "--name=dokpod-installer-$(date +%s)-$$" \
    "--volume=$resolved:/run/engine.sock" \
    "--volume=$install_dir:$install_dir" \
    "--volume=$secrets_dir:$secrets_dir" \
    --env=DOCKER_HOST=unix:///run/engine.sock \
    "--env=DOKPOD_HOST_ENGINE_SOCKET=$resolved" \
    "--env=DOKPOD_HOST_ENGINE=$engine" \
    "--env=DOKPOD_INSTALLER_IMAGE_REF=$image" \
    "--env=DOKPOD_HOST_UNPRIVILEGED_PORT_START=$port_start" \
    "--env=DOKPOD_HOST_LINGER=$linger" \
    "--env=DOKPOD_HOST_LISTENING_PORTS=$listening" \
    "--env=DOKPOD_HOST_FREE_BYTES=$free_bytes" \
    "--env=DOKPOD_HOST_NAME=$host_name" \
    "--env=DOKPOD_HOST_PUBLIC_NAME=$public_name" \
    "--env=DOKPOD_HOST_PUBLIC_ADDRESSES=$public_addresses" \
    "--env=HOME=$HOME" \
    "$@"
if [ -t 0 ] && [ -t 1 ]; then set -- --tty "$@"; fi
if [ "$engine" = podman ]; then set -- --security-opt=label=disable "$@"; fi

# Arquivos citados nas respostas (segredos { file }, licença, certificados): somente leitura, no mesmo caminho.
# Variáveis citadas em { env }: repassadas só pelo nome, sem o valor na linha de comando.
if [ -n "$answers" ]; then
    [ -f "$answers" ] || fail "Arquivo de respostas não encontrado: $answers" 2
    set -- "--volume=$answers:$answers:ro" "$@"
    for path in $(grep -oE '(["'"'"' :]|^)/[A-Za-z0-9._/@+-]+' "$answers" | sed -E 's/^["'"'"' :]//' | sort -u); do
        case "$path" in "$install_dir"|"$install_dir"/*|"$secrets_dir"|"$secrets_dir"/*|"$answers") continue ;; esac
        if [ -e "$path" ]; then set -- "--volume=$path:$path:ro" "$@"; fi
    done
    for name in $(grep -oE '"?env"?[[:space:]]*:[[:space:]]*"?[A-Z][A-Z0-9_]*' "$answers" | grep -oE '[A-Z][A-Z0-9_]*$' | sort -u); do
        set -- "--env=$name" "$@"
    done
fi

for path in $extra_dirs; do
    case "$path" in /*) if [ -e "$path" ]; then set -- "--volume=$path:$path:ro" "$@"; fi ;; esac
done

exec "$cli" run "$@"
