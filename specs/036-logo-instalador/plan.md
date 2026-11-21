# Plano de Implementação: Identificação visual do instalador

**Branch**: `036-logo-instalador` | **Data**: 2026-11-20 | **Spec**: [spec.md](spec.md)

**Entrada**: Especificação da funcionalidade em `/specs/036-logo-instalador/spec.md`

**Origem**: IA assistida; revisor humano: Lincoln (`@lzocateli`).

> Redija este plano e todos os artefatos derivados em português brasileiro (pt-BR). Preserve nomes de código, caminhos, comandos e identificadores técnicos.

## Resumo

O instalador passa a mostrar, no início de cada execução em terminal interativo, o nome Dokpod em letras de blocos com o degradê de amarelo para o lima da marca e a linha "Instalador do Dokpod <versão> · Altivy". O desenho e as regras de cor ficam num módulo novo, `Banner.psm1`. O `Invoke-InstallerCli` decide antes de tudo se mostra o logo, inclusive na ajuda.

O logo some com `--no-logo`, com `--non-interactive` e sempre que a entrada ou a saída não é terminal. Ele degrada para sem cor (`NO_COLOR`), para ASCII (sem UTF-8) e para só a linha da versão (terminal estreito).

Duas mudanças de suporte:
- a imagem passa a saber a própria versão, pelo `RELEASE_VERSION` que o `publish.yml` já envia;
- o `install.sh` repassa ao container, só pelo nome, as variáveis do terminal.

## Contexto Tecnico

**Linguagem/Versão**: PowerShell 7.6 (instalador, na imagem `dokpod-ce-installer`); POSIX `sh` (bootstrap `install.sh`).

**Dependências Principais**: nenhuma nova. O desenho é original ([D-005](research.md#d-005-desenho-e-fallback)).

**Persistência**: N/A.

**Testes**: Pester 6.2.0 na imagem de testes (`podman build --target test -f deploy/installer/Dockerfile .`): `Banner.Tests.ps1` novo, `Cli.Tests.ps1` e `InstallSh.Tests.ps1` estendidos; validação manual em terminal real ([quickstart.md](quickstart.md)).

**Plataforma Alvo**: container do instalador sobre Docker ou Podman rootless em Linux, como na 033.

**Tipo de Projeto**: ferramenta de instalação (CLI).

**Metas de Desempenho**: menos de 50 ms entre o início e a primeira mensagem do comando (SC-003); o logo é texto fixo, sem rede nem disco.

**Restrições**:
- nenhuma mudança de resultado, arquivo gerado ou código de saída (FR-006);
- nada do logo em saída redirecionada (FR-004);
- variáveis do host repassadas só pelo nome, como já é feito para as citadas nas respostas.

**Escala/Escopo**: um módulo novo, ajustes em `Cli.psm1`, `install.sh` e no `Dockerfile`, e documentação.

## Verificação da Constituição

*GATE: Deve ser aprovado antes da pesquisa da Fase 0 e revisado novamente após o design da Fase 1.*

| Princípio | Como o plano atende | Situação |
| --- | --- | --- |
| I. pt-BR | Textos do logo, da ajuda e dos artefatos em pt-BR | ok |
| II. Engine local e workloads | Não toca engine, stack nem workloads | ok |
| III. Identidade e menor privilégio | Nenhum segredo envolvido; variáveis do terminal repassadas só pelo nome, sem valores na linha de comando | ok |
| IV. Operações duráveis | Sem efeito em operações; códigos de saída e arquivos inalterados (FR-006) | ok |
| V. Modularidade e compatibilidade | Módulo próprio no instalador; contrato da CLI acrescido de forma compatível (opção nova, nenhuma removida) | ok |
| VI. Qualidade por plataforma | Pester na imagem de testes e validação manual em terminal real | ok |
| Dependências permissivas | Nenhuma | ok |
| Threat model para fronteira nova | Não há fronteira nova; o repasse de variáveis do terminal não leva segredo | ok (não se aplica) |

Reavaliação após o desenho: sem violações.

## Estrutura do Projeto

### Documentação desta Funcionalidade

```text
specs/036-logo-instalador/
├── spec.md
├── plan.md               # este arquivo
├── research.md           # D-001 a D-006
├── quickstart.md
├── contracts/
│   └── installer-cli.md  # acréscimo ao contrato da CLI da 033
├── checklists/requirements.md
└── tasks.md              # gerado por /speckit-tasks
```

Sem `data-model.md`: a feature não tem entidades nem dados persistidos.

### Código-Fonte (raiz do repositório)

```text
deploy/installer/
├── Dockerfile                 # ARG RELEASE_VERSION → ENV DOKPOD_INSTALLER_VERSION (D-001)
├── install.sh                 # repasse de NO_COLOR, TERM, COLORTERM, LANG, LC_ALL, LC_CTYPE (D-003)
├── README.md                  # --no-logo
├── src/
│   ├── Banner.psm1            # novo: desenho, modos de cor, fallback e Write-InstallerBanner (D-004, D-005)
│   └── Cli.psm1               # passada prévia por --no-logo, FlagOptions e texto da ajuda (D-002)
└── tests/
    ├── Banner.Tests.ps1       # novo
    ├── Cli.Tests.ps1
    └── InstallSh.Tests.ps1

specs/033-instalador-guiado/contracts/installer-cli.md   # opção e saída novas
docs/runbooks/instalacao.md                               # automação: logo desligado sem TTY e --no-logo
```

**Decisão de Estrutura**: um módulo próprio, como os demais do instalador (um assunto por `.psm1`), importado pelo `Cli.psm1`. O desenho fica como constante no módulo, sem arquivo de dados à parte, porque são seis linhas que nunca mudam por configuração.

## Registro de Complexidade

Sem violações a justificar.

## Itens para a revisão humana

- **Versão `dev`**: um build local, sem `RELEASE_VERSION`, mostra "Instalador do Dokpod dev · Altivy".
- **Paleta de 16 cores**: num terminal sem 256 cores, o degradê vira amarelo nas três primeiras linhas e verde nas três últimas.
