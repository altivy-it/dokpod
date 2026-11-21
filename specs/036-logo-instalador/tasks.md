---

description: "Tarefas da identificação visual do instalador"
---

# Tarefas: Identificação visual do instalador

**Entrada**: Documentos de design em `/specs/036-logo-instalador/`

> Redija todas as tarefas e descrições em português brasileiro (pt-BR), preservando identificadores, comandos e caminhos técnicos.

**Pré-requisitos**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [contracts/installer-cli.md](contracts/installer-cli.md), [quickstart.md](quickstart.md)

**Testes**: obrigatórios (constituição, princípio VI): Pester 6.2.0 na imagem de testes do instalador e validação manual em terminal real.

**Organização**: agrupadas por história da spec para implementação e validação independentes.

**Gate**: `/speckit-implement` exige a aprovação da spec e a autorização humana explícita.

## Formato: `[ID] [P?] [Story] Descricao`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência pendente)
- **[Story]**: história da spec (US1, US2)

## Convencoes de Caminhos

- Instalador: `deploy/installer/src/`, testes em `deploy/installer/tests/`, bootstrap em `deploy/installer/install.sh`
- Testes rodam na imagem: `podman build --format docker --target test -t localhost/dokpod-installer:test-runner -f deploy/installer/Dockerfile .` e `podman run --rm localhost/dokpod-installer:test-runner`

---

## Fase 1: Preparação

**Objetivo**: a imagem conhece a própria versão.

- [x] T001 `ARG RELEASE_VERSION=` no estágio `runtime` de `deploy/installer/Dockerfile`, recusando valores fora de `0-9A-Za-z.-` como no `backend/apps/Dokpod.ControlPlane.Api/Dockerfile`, e `ENV DOKPOD_INSTALLER_VERSION=${RELEASE_VERSION:-dev}` junto das variáveis existentes ([D-001](research.md#d-001-versão-exibida)). Conferir com `podman build --build-arg RELEASE_VERSION=0.0.0-logo` e `podman inspect` que a variável vale `0.0.0-logo`, e sem o argumento, `dev`. O workflow não muda: `tools/scripts/ci/Publish-ContainerImage.ps1` já passa `--build-arg RELEASE_VERSION=<versão da tag>` a toda imagem do `publish.yml` (conferido no planejamento).

---

## Fase 2: Fundação

**Objetivo**: o módulo do logo, testável sem terminal real.

**CRÍTICO**: nenhuma história começa antes desta fase.

- [x] T002 [P] Testes em `deploy/installer/tests/Banner.Tests.ps1` (cabeçalho `#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '6.2.0' }` e comentário da spec 036, como em `Cli.Tests.ps1`), chamando as funções com o ambiente injetado:
  - `Get-InstallerColorMode`: `NO_COLOR=1` dá `None` mesmo com `COLORTERM=truecolor`; `TERM=dumb` dá `None`; `COLORTERM` `truecolor` ou `24bit` dá `TrueColor`; `TERM=xterm-256color` sem `COLORTERM` dá `Ansi256`; `TERM=xterm` dá `Ansi16` ([D-004](research.md#d-004-cor));
  - `Test-InstallerUtf8`: `LC_ALL` tem precedência sobre `LC_CTYPE`, e este sobre `LANG`; `pt_BR.UTF-8` e `C.utf8` são UTF-8; `C` e `POSIX` não são;
  - `Format-InstallerBanner`: em `TrueColor`, seis linhas com `ESC[38;2;255;242;122m` … `ESC[38;2;159;217;60m` e a linha "Instalador do Dokpod 0.0.0-logo · Altivy" com `ESC[2m`; em `None`, nenhum `ESC`; sem UTF-8, nenhum `█` nem caractere de sombra (`╗╔╝╚═║`) e as mesmas seis linhas com `#`; com largura 51, só a linha da versão; com largura nula (indisponível), só a linha da versão; versão vazia vira `dev`.
- [x] T003 Módulo `deploy/installer/src/Banner.psm1` até T002 passar, com comentário de cabeçalho no estilo dos outros módulos ("Logo do instalador (spec 036)"):
  - constante com as seis linhas aprovadas (50 colunas) de [contracts/installer-cli.md](contracts/installer-cli.md);
  - `Get-InstallerColorMode -Environment <IDictionary>`, `Test-InstallerUtf8 -Environment <IDictionary>` e `Format-InstallerBanner -Version <string> -ColorMode <...> -Utf8 <bool> -Width <int?>` puras, sem ler o ambiente real;
  - cores do Y1 em truecolor; no `Ansi256`, o índice do cubo 6×6×6 mais próximo de cada cor (`16 + 36r + 6g + b`); no `Ansi16`, amarelo (`ESC[93m`) nas três primeiras linhas e verde (`ESC[92m`) nas três últimas; `ESC[0m` no fim de cada linha;
  - `Export-ModuleMember` só das funções públicas.
- [x] T004 `Write-InstallerBanner -Version <string>` em `deploy/installer/src/Banner.psm1`: lê o ambiente real (variáveis do processo e `[Console]::WindowWidth`, tratando exceção como largura indisponível) e escreve o resultado de `Format-InstallerBanner` numa única escrita UTF-8 na saída padrão, seguida de uma linha em branco. Importar o módulo no `deploy/installer/src/Cli.psm1` junto dos demais.

**Checkpoint**: o logo é gerado corretamente para todos os ambientes nos testes.

---

## Fase 3: História de Usuário 1 - Reconhecer o instalador ao executá-lo (Prioridade: P1) MVP

**Objetivo**: o logo aparece no terminal interativo, uma vez, antes de qualquer mensagem, inclusive na ajuda.

**Teste independente**: executar `help` e um comando num terminal interativo e ver o logo com a versão; nos testes, simular a sessão interativa.

### Testes da História 1

- [x] T005 [P] [US1] Testes em `deploy/installer/tests/Cli.Tests.ps1`:
  - com `Test-InteractiveSession` simulada como verdadeira (`Mock` no módulo `Cli`), `Invoke-InstallerCli -Arguments @('help')` chama `Write-InstallerBanner` uma vez, antes de `Write-InstallerHelp`, com a versão de `DOKPOD_INSTALLER_VERSION`;
  - o mesmo para dois erros de uso que o parser recusa antes de qualquer pergunta: comando desconhecido (`instalar`) e `plan --confirm-migrations abc` (planHash inválido). O logo vem antes e o código 2 continua o mesmo. Nenhum dos dois precisa de engine nem de mock além de `Test-InteractiveSession`; comandos que perguntariam algo ficam de fora, porque com a sessão simulada como interativa eles esperariam resposta.

### Implementação da História 1

- [x] T006 [US1] Em `Invoke-InstallerCli` (`deploy/installer/src/Cli.psm1`), uma passada nos argumentos antes do teste da ajuda: decidir o logo e chamar `Write-InstallerBanner -Version $env:DOKPOD_INSTALLER_VERSION` quando `Test-InteractiveSession` for verdadeiro ([D-002](research.md#d-002-quando-exibir)). A chamada fica dentro do `try` existente, e uma falha ao escrever o logo é ignorada sem mudar o fluxo (FR-006).
- [x] T007 [US1] Repasse das variáveis do terminal em `deploy/installer/install.sh`: para cada uma de `NO_COLOR`, `TERM`, `COLORTERM`, `LANG`, `LC_ALL` e `LC_CTYPE` definida no host, acrescentar `--env=NOME` (só o nome), junto das demais opções do `docker run` ([D-003](research.md#d-003-bootstrap-e-variáveis-do-terminal)). Teste em `deploy/installer/tests/InstallSh.Tests.ps1`, no estilo do caso `--env=KC_ADMIN_SECRET`: com `COLORTERM=truecolor` e `LANG=pt_BR.UTF-8` definidas, o comando contém `--env=COLORTERM` e `--env=LANG`, sem o valor; com `NO_COLOR` indefinida, não contém `--env=NO_COLOR`.

**Checkpoint**: o logo aparece no terminal real pelo `install.sh`.

---

## Fase 4: História de Usuário 2 - Executar por automação sem o logo (Prioridade: P1)

**Objetivo**: `--no-logo`, `--non-interactive` e a saída redirecionada desligam o logo, sem mudar resultado nem código de saída.

**Teste independente**: rodar com `--no-logo` e com saída redirecionada e conferir que não há nenhum byte do logo; comparar os códigos de saída com e sem a opção.

### Testes da História 2

- [x] T008 [P] [US2] Testes em `deploy/installer/tests/Cli.Tests.ps1`:
  - `ConvertFrom-InstallerArguments` aceita `--no-logo` em qualquer posição e repetida (`@('--no-logo', 'plan', '--no-logo', '--dry-run')`), sem erro de opção desconhecida;
  - com sessão interativa simulada, `--no-logo` (em `help`, `--help` e num comando) e `--non-interactive` não chamam `Write-InstallerBanner`;
  - pelo `Invoke-Entry` (processo com saída redirecionada), `help` e `help --no-logo` não contêm `█`, `ESC[` nem a linha "Instalador do Dokpod … · Altivy", e saem com código 0;
  - para `help`, um erro de uso (`instalar`) e `plan` sem respostas, os códigos de saída são iguais com e sem `--no-logo` (SC-004);
  - `NO_COLOR=` vazia não tira a cor; `NO_COLOR=1` tira (A3 do `/speckit-analyze`).

### Implementação da História 2

- [x] T009 [US2] Em `deploy/installer/src/Cli.psm1`: `no-logo` em `$script:FlagOptions`; a passada da T006 tira `--no-logo` dos argumentos antes do teste da ajuda (para `help --no-logo` e `--no-logo --help` funcionarem) e não mostra o logo com `--no-logo` ou `--non-interactive`.
- [x] T010 [US2] Texto da ajuda (`Write-InstallerHelp` em `deploy/installer/src/Cli.psm1`): `--no-logo` na lista de opções e uma linha "O logo só aparece em terminal interativo; --no-logo ou --non-interactive o desligam."

**Checkpoint**: as duas histórias funcionam; automação sai limpa com ou sem a opção.

---

## Fase Final: Refinamento e Aspectos Transversais

- [x] T011 [P] Contrato e documentação:
  - `specs/033-instalador-guiado/contracts/installer-cli.md`: linha de `--no-logo` na tabela de opções globais, `--non-interactive` desligando o logo e o item "Logo" na seção "Saída", conforme [contracts/installer-cli.md](contracts/installer-cli.md);
  - `deploy/installer/README.md` e `docs/runbooks/instalacao.md`: `--no-logo` e o comportamento em automação;
  - `specs/README.md`: entrada da 036.
- [x] T012 Suíte completa do instalador na imagem de testes (`podman build --target test` e execução), incluindo o PSScriptAnalyzer da CI (`deploy/installer/PSScriptAnalyzerSettings.psd1`) no `Banner.psm1`; registrar o resultado.
- [x] T013 Executar os dois cenários de [quickstart.md](quickstart.md) num terminal real e registrar as evidências em `specs/036-logo-instalador/validation.md`, incluindo a medida de SC-003 (diferença no tempo até a primeira mensagem entre a execução com o logo e a mesma com `--no-logo`), com `NOT RUN` explícito no que faltar.

---

## Dependências e Ordem de Execução

- **Fase 1** (T001): sem dependências.
- **Fase 2** (T002–T004): T003 depende de T002; T004 depende de T003.
- **US1** (T005–T007): depende da Fase 2; T007 é independente de T005 e T006.
- **US2** (T008–T010): depende da T006 (a passada nos argumentos é a mesma).
- **Fase Final**: depende das duas histórias.

## Exemplos de Execução Paralela

```text
Fase 1 e 2: T001 (Dockerfile) | T002 (Banner.Tests.ps1)
US1:        T005 (Cli.Tests.ps1) | T007 (install.sh e InstallSh.Tests.ps1)
US2:        T008 (testes) antes de T009 e T010
Final:      T011 (documentação) | T012 (suíte)
```

## Estratégia de Implementação

### MVP

Fases 1 e 2, depois US1 e US2 juntas: o logo só pode ser publicado com o desligamento automático em automação, senão logs de CI passariam a ter arte e cor.

### Entrega

Um único PR para a 036, com a suíte do instalador verde e a validação manual registrada.
