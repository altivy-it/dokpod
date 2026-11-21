# Pesquisa: Identificação visual do instalador

**Spec**: [spec.md](spec.md) | **Data**: 2026-11-20 | **Origem**: IA assistida; decisões técnicas sujeitas à revisão humana (lzocateli)

As decisões de produto (desenho D, cor Y1, `--no-logo`) vêm dos esclarecimentos da spec. Aqui ficam só as técnicas.

## D-001: Versão exibida

- **Decisão**: o `Dockerfile` do instalador ganha `ARG RELEASE_VERSION=` no estágio `runtime`, validado como no `backend/apps/Dokpod.ControlPlane.Api/Dockerfile` (só `0-9A-Za-z.-`), e grava `ENV DOKPOD_INSTALLER_VERSION=${RELEASE_VERSION:-dev}`. O logo mostra esse valor.
- **Justificativa**: hoje a imagem do instalador não sabe a própria versão: ela só lê o `release-manifest.json` da versão pedida nas respostas, que pode ser outra (no `upgrade`, por exemplo). O `tools/scripts/ci/Publish-ContainerImage.ps1`, chamado pelo `gw-container-images.yml` para cada imagem do `publish.yml`, já passa `--build-arg RELEASE_VERSION=<versão da tag>` a todo Dockerfile que declara o `ARG` (conferido em 2026-11-20), então a release recebe o valor certo sem mudar o workflow. Um build local mostra `dev`.
- **Alternativas**: a versão do manifesto (é a versão a instalar, não a do instalador, e exige rede antes do logo); um arquivo `VERSION` copiado no build (duplica o que o build já recebe).

## D-002: Quando exibir

- **Decisão**: `Invoke-InstallerCli` faz uma passada nos argumentos antes de tudo, inclusive antes da ajuda:
  - tira `--no-logo` da lista (aceita em qualquer posição, repetida ou não) e registra `no-logo` em `$script:FlagOptions`, para o parser não a recusar como opção desconhecida;
  - mostra o logo só se não houver `--no-logo` nem `--non-interactive` e se `Test-InteractiveSession` (`Common.psm1`, entrada e saída sem redirecionamento e sem `DOKPOD_NON_INTERACTIVE`) for verdadeiro.
- **Saída**: o logo vai para a saída padrão, numa única escrita, antes da primeira mensagem do comando. Como só aparece com a saída num terminal, nunca chega a um arquivo ou pipe lido por outra ferramenta (`migrations export`, `sql export` e o relatório escrevem em arquivos, não na saída).
- **Justificativa**: o caminho da ajuda (`--help`, `help`) retorna antes do parser; sem a passada prévia, o logo não apareceria nela, ou `--no-logo` seria recusada nela. Reaproveitar `Test-InteractiveSession` mantém uma só definição de "interativo" no instalador.
- **Alternativas**: escrever o logo na saída de erro (evitaria misturar com a saída, mas o operador costuma redirecionar só a saída padrão para log e o logo continuaria no terminal; a regra do terminal interativo já resolve).

## D-003: Bootstrap e variáveis do terminal

- **Decisão**: o `install.sh` repassa ao container, só pelo nome (`--env=NOME`, como já faz com as variáveis citadas nas respostas), as variáveis `NO_COLOR`, `TERM`, `COLORTERM`, `LANG`, `LC_ALL` e `LC_CTYPE`, cada uma apenas se estiver definida no host. A largura vem do próprio TTY, que o `install.sh` já pede (`--tty` quando entrada e saída são terminal). `--no-logo` passa direto, como as demais opções.
- **Justificativa**: o instalador roda num container; sem o repasse, ele nunca veria `NO_COLOR`, o suporte a truecolor nem o locale UTF-8 do operador. Passar só o nome não põe valores na linha de comando.

## D-004: Cor

- **Decisão**: uma função pura escolhe o modo de cor a partir das variáveis:
  - `NO_COLOR` definida (qualquer valor não vazio) ou `TERM=dumb`: sem cor;
  - `COLORTERM` igual a `truecolor` ou `24bit`: truecolor, com as seis cores do Y1 (`255;242;122`, `238;244;99`, `217;244;86`, `198;243;78`, `183;243;74`, `159;217;60`), do amarelo ao lima `#b7f34a` do logo do Dokpod e a linha da versão em tom apagado (`ESC[2m`);
  - `TERM` contendo `256color`: a cor mais próxima no cubo de 256 cores;
  - outro terminal: amarelo e verde da paleta de 16 cores (as três primeiras linhas em amarelo, as três últimas em verde).
- **Implementação (T003)**: os prefixos de cor de cada modo ficam numa tabela fixa (os índices do cubo de 256 cores já calculados: 228, 227, 191, 191, 155, 149) e a detecção usa comparação de texto, sem regex: montar as cores na hora acrescentava 66 ms à execução, acima da meta de SC-003 ([validation.md](validation.md#sc-003-tempo-acrescentado)).
- **Justificativa**: são as convenções usuais de terminal ([no-color.org](https://no-color.org), `COLORTERM` dos emuladores). A cor nunca depende de detectar o emulador por nome.

## D-005: Desenho e fallback

- **Decisão**: as seis linhas do desenho aprovado (50 colunas) ficam num novo `deploy/installer/src/Banner.psm1`, como constante. Sem UTF-8 (nenhuma de `LC_ALL`, `LC_CTYPE`, `LANG` termina em `UTF-8`/`utf8`, na ordem de precedência do POSIX), o instalador usa a versão ASCII derivada do mesmo desenho: `█` vira `#` e os traços da sombra viram espaço; a última linha, só de sombra, some, e o desenho fica com cinco linhas. Com a largura do terminal (`[Console]::WindowWidth`) abaixo de 52, ou indisponível, sai só a linha da versão. A escrita usa UTF-8 explicitamente.
- **Justificativa**: o desenho é original, então não há fonte de terceiros nem licença a verificar; a versão ASCII derivada mantém a mesma forma sem um segundo desenho para manter.
- **Alternativas**: uma fonte do FIGlet para a versão ASCII (traz um desenho diferente e uma licença a registrar).

## D-006: Testes

- **Decisão**: as funções do `Banner.psm1` recebem o ambiente (variáveis, largura e se é interativo) como parâmetros, para os testes não dependerem de um TTY real. Pester 6.2.0 na imagem de testes:
  - `Banner.Tests.ps1`: modos de cor por variável, fallback ASCII, terminal estreito, versão `dev` e de release, nenhum caractere de controle sem cor;
  - `Cli.Tests.ps1`: `--no-logo` aceita em qualquer posição e na ajuda; com saída redirecionada, nenhum byte do logo; códigos de saída iguais com e sem a opção;
  - `InstallSh.Tests.ps1`: as seis variáveis repassadas só pelo nome, e só quando definidas.
- **Validação manual**: um terminal real com `sh install.sh help` e `--no-logo`, registrado no quickstart, porque a renderização do degradê depende do emulador.
