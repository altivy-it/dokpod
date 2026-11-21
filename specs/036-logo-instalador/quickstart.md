# Validação: Identificação visual do instalador

Tudo roda pela imagem do instalador; o host só precisa do engine rootless, como na 033.

## Pré-requisitos

- Imagem construída da branch: `podman build --format docker -t localhost/dokpod-installer:logo -f deploy/installer/Dockerfile --build-arg RELEASE_VERSION=0.0.0-logo .`
- Um terminal com truecolor e UTF-8 (o padrão do GNOME Terminal, do Windows Terminal e do iTerm2).
- `install.sh` apontando para a imagem local com `DOKPOD_INSTALLER_IMAGE=localhost/dokpod-installer:logo` (o bootstrap aceita só imagens `localhost/...`, sem verificação de assinatura).

## Cenário 1: logo no terminal (Jornada 1)

1. `sh install.sh help` num terminal: aparece o logo em degradê de amarelo para lima e "Instalador do Dokpod 0.0.0-logo · Altivy", depois a ajuda.
2. Repetir com `TERM=xterm-256color COLORTERM=` : as cores são as mais próximas da paleta de 256, sem códigos visíveis.
3. Repetir com `LANG=C LC_ALL=C`: o desenho sai com `#`.
4. Diminuir a janela para menos de 52 colunas: só a linha da versão.

## Cenário 2: automação (Jornada 2)

1. `sh install.sh help --no-logo`: a ajuda começa na primeira linha, sem logo.
2. `sh install.sh help > saida.txt`: o arquivo não contém `█`, `#####` do logo nem `ESC[`.
3. `sh install.sh status --non-interactive --answers <arquivo>`: sem logo; código de saída igual ao da mesma chamada com `--no-logo`.
4. `NO_COLOR=1 sh install.sh help`: logo sem cor.

## Testes automatizados de referência

- `podman build --target test -f deploy/installer/Dockerfile .` e a execução da imagem de testes: `Banner.Tests.ps1`, `Cli.Tests.ps1` e `InstallSh.Tests.ps1` passam, e o restante da suíte continua verde (SC-004).
