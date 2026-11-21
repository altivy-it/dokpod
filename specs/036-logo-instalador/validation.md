# Validação: Identificação visual do instalador

**Data**: 2026-11-20 · **Origem**: IA assistida · **Ambiente**: host de desenvolvimento Linux, Podman rootless, imagem do instalador construída da branch `036-logo-instalador`.

O terminal real foi simulado com `script -qec` (pseudo-terminal) e `stty cols`. A conferência visual das cores num emulador (GNOME Terminal, Windows Terminal, iTerm2) fica para o revisor humano: ela depende do emulador, não do instalador.

## Testes automatizados (T012)

| Verificação | Comando | Resultado |
| --- | --- | --- |
| Suíte completa do instalador | `podman build --target test -t localhost/dokpod-installer:ci-test -f deploy/installer/Dockerfile .` e `podman run --rm localhost/dokpod-installer:ci-test` | 323 aprovados, 0 falhas, 69 pulados (os que exigem o socket do engine, como na CI) |
| `Banner.Tests.ps1` | idem | 23 aprovados |
| Logo em `Cli.Tests.ps1` e `InstallSh.Tests.ps1` | idem | aprovados (ordem logo → ajuda, `--no-logo` em qualquer posição, sem logo redirecionado, códigos iguais, variáveis só pelo nome) |
| PSScriptAnalyzer da CI | `Invoke-ScriptAnalyzer -Path /i/src -Recurse -Settings /i/PSScriptAnalyzerSettings.psd1 -EnableExit` | código 0, nenhum aviso |
| Ajuda da CI | `docker run --rm dokpod-installer:ci --help \| grep -q 'Instalador do Dokpod'` (com Podman) | passa; sem TTY não há logo |

## Versão na imagem (T001)

| Build | `DOKPOD_INSTALLER_VERSION` |
| --- | --- |
| `--build-arg RELEASE_VERSION=0.0.0-logo` | `0.0.0-logo` |
| sem o argumento | `dev` |
| `--build-arg 'RELEASE_VERSION=1.0;rm'` | build recusado: "RELEASE_VERSION inválida" |

## Cenário 1: logo no terminal (quickstart)

`script -qec "stty cols 100; … podman run --rm -it … localhost/dokpod-installer:logo help"`:

| Ambiente | Resultado |
| --- | --- |
| `COLORTERM=truecolor LANG=pt_BR.UTF-8` | seis linhas com `ESC[38;2;255;242;122m` … `ESC[38;2;159;217;60m` (Y1) e a versão com `ESC[2m` |
| `TERM=xterm-256color LANG=pt_BR.UTF-8`, sem `COLORTERM` | `ESC[38;5;228m`, `227`, `191`, `191`, `155`, `149`, sem truecolor |
| `TERM=xterm LANG=C`, sem `COLORTERM` | desenho com `#`, cinco linhas, `ESC[93m` (amarelo) nas três primeiras e `ESC[92m` (verde) nas duas restantes |
| `NO_COLOR=1` | blocos sem nenhum código de cor |
| 40 colunas | só "Instalador do Dokpod 0.0.0-logo · Altivy" |
| `sh deploy/installer/install.sh help` com `COLORTERM=truecolor` e `DOKPOD_INSTALLER_IMAGE=localhost/dokpod-installer:logo` | logo em truecolor (a variável chegou ao container pelo `install.sh`), depois da linha de aviso do bootstrap sobre a imagem local, como o FR-001 permite |

## Cenário 2: automação

| Execução | Resultado |
| --- | --- |
| `install.sh help --no-logo` no pseudo-terminal | a saída começa por "Instalador do Dokpod (spec 033)…", sem logo |
| `install.sh help \| head` (saída redirecionada) | sem logo e sem códigos de cor |

## SC-003: tempo acrescentado

Mediana de 9 execuções de `help`, com e sem `--no-logo`, alternadas, num processo novo do `pwsh` por execução, dentro do container com pseudo-terminal:

| Rodada | Com logo | Com `--no-logo` | Diferença |
| --- | --- | --- | --- |
| 1 | 561 ms | 516 ms | 45 ms |
| 2 | 602 ms | 557 ms | 45 ms |

A primeira versão do módulo, que montava as cores na hora, acrescentava 66 ms. As cores passaram a uma tabela fixa e o regex virou comparação de texto, e a diferença caiu para 45 ms, dentro da meta de 50 ms, mas com pouca folga.

## Troca de cor (2026-11-20)

O solicitante trocou o degradê D1 (ciano para azul) pelo Y1 (amarelo para o lima `#b7f34a` do logo do Dokpod). Depois da troca: suíte completa com 323 aprovados, 0 falhas e 69 pulados; PSScriptAnalyzer com código 0; as três linhas de cor do cenário 1 repetidas pelo `install.sh` no pseudo-terminal, com os códigos da tabela acima. A medição de SC-003 não foi repetida: a troca só muda as constantes da tabela, não o caminho executado.

## Achados

- Numa das primeiras execuções no pseudo-terminal, com `LANG=C`, só a linha da versão apareceu, como se a largura do terminal ainda fosse desconhecida. Não se repetiu em 8 execuções seguidas nem nas demais. Se a largura não estiver disponível no início, o instalador cai para a linha da versão (FR-005), sem erro.

## Conferência visual

- Feita pelo solicitante no próprio terminal, com o degradê Y1, em 2026-11-20: "Ficou bom".
