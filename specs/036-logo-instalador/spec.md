# Especificação da Feature: Identificação visual do instalador

**Branch da feature**: `036-logo-instalador`

**Criada em**: 2026-11-20

**Estado**: `approved` - aprovada pelo solicitante (lzocateli) em 2026-11-20 ("Está aprovado"), com as correções do `/speckit-analyze` (A1, I1, I2, A2, A3).

**Entrada**: Pedido do solicitante em 2026-11-20: "Gostaria de uma identificação visual legal para o instalador, quando o usuário executar, gostaria que o nome dokpod logo em caracteres na console fosse exibido; como pode ser executado por automação, poderia --nologo". Prévia aprovada antes da spec ("se possível gostaria de ver antes como ficaria"): opção D (letras em blocos), esquema de cor Y1 (degradê de amarelo para o lima `#b7f34a` do logo do Dokpod; trocado do D1, ciano para azul, a pedido do solicitante), opção `--no-logo`.

**Origem**: IA assistida; solicitante e revisor humano: lzocateli.

**Depende de**: [033](../033-instalador-guiado/spec.md) (instalador guiado, contrato da CLI em [installer-cli.md](../033-instalador-guiado/contracts/installer-cli.md)).

## Esclarecimentos

### Sessão 2026-11-20

- P: Qual desenho e qual cor? → R: Letras em blocos (opção D da prévia) com degradê de amarelo para o lima da marca (`#b7f34a`), uma cor por linha (Y1, que substituiu em 2026-11-20 o degradê de ciano para azul D1 da primeira prévia, para seguir as cores do logo do Dokpod), e embaixo a linha "Instalador do Dokpod <versão> · Altivy" em tom apagado.
- P: Como desligar? → R: Pela opção `--no-logo`, no padrão das opções existentes (`--answers`, `--offline`).

## Cenários do Usuário e Testes (obrigatório)

### Jornada do Usuário 1 - Reconhecer o instalador ao executá-lo (Prioridade: P1)

O operador executa o instalador num terminal e vê o nome Dokpod em letras grandes, coloridas, com a versão logo abaixo, antes das perguntas ou do plano.

**Valor desta prioridade**: dá identidade ao produto no primeiro contato e mostra de imediato qual versão do instalador está rodando.

**Teste independente**: executar o instalador num terminal interativo e conferir o logo, as cores e a versão; repetir com a saída redirecionada para arquivo e conferir que o logo não aparece.

**Cenários de aceite**:

1. **Dado** um terminal interativo com suporte a cor, **Quando** o operador executa qualquer comando do instalador, **Então** o logo aparece uma vez, no início, com o degradê de amarelo para o lima da marca e a linha "Instalador do Dokpod <versão> · Altivy" com a versão real do instalador.
2. **Dado** um terminal interativo, **Quando** o operador pede a ajuda (`help`, `--help`), **Então** o logo aparece antes do texto de ajuda.
3. **Dado** um terminal sem suporte a cor de 24 bits, **Quando** o instalador executa, **Então** o logo usa as cores mais próximas que o terminal suporta, sem caracteres de controle visíveis.

---

### Jornada do Usuário 2 - Executar por automação sem o logo (Prioridade: P1)

Um pipeline de CI ou um script executa o instalador com `--answers` e guarda a saída em log. O log começa direto pela primeira mensagem útil.

**Valor desta prioridade**: logs de automação não podem ter arte nem códigos de cor, que atrapalham a leitura e a busca.

**Teste independente**: executar com `--no-logo` num terminal e conferir a ausência do logo; executar sem a opção, mas com a saída redirecionada ou em modo não interativo, e conferir também a ausência.

**Cenários de aceite**:

1. **Dado** a opção `--no-logo`, **Quando** o instalador executa, mesmo num terminal interativo, **Então** o logo não aparece e a saída começa pela primeira mensagem do comando.
2. **Dado** a saída redirecionada para arquivo ou pipe, ou o modo não interativo (`--non-interactive` ou `DOKPOD_NON_INTERACTIVE`), **Quando** o instalador executa sem `--no-logo`, **Então** o logo também não aparece.
3. **Dado** a variável `NO_COLOR` definida e não vazia num terminal interativo, **Quando** o instalador executa, **Então** o logo aparece sem cor.
4. **Dado** `--no-logo` com qualquer comando, **Quando** o instalador executa, **Então** o resultado e o código de saída são os mesmos que sem a opção.

---

## Casos de Borda

- Terminal sem UTF-8: as letras em blocos não são exibíveis; o instalador mostra uma versão do logo só com caracteres ASCII.
- Terminal com menos de 52 colunas (o logo tem 50, mais a margem): o logo não cabe; o instalador mostra só a linha "Instalador do Dokpod <versão> · Altivy".
- `--no-logo` repetida ou em qualquer posição entre as opções: aceita, sem erro.
- Erro de uso (código 2) antes de qualquer efeito: o logo, quando exibido, não muda a mensagem de erro nem o código.
- Saída lida por outra ferramenta: o logo nunca se mistura com conteúdo lido por máquina. As exportações (`migrations export --out <arquivo>`, `sql export --out <pasta>`) gravam em arquivo, e o logo nunca vai para elas.
- O logo vai para a saída de mensagens ao operador, nunca para um arquivo gerado pelo instalador (`answers.yaml`, pacotes, exportações).

## Requisitos (obrigatório)

### Requisitos Funcionais

- **FR-001**: O instalador DEVE exibir, uma vez por execução e antes de qualquer outra mensagem do próprio instalador (mensagens do bootstrap `install.sh` e do engine, como o download da imagem, podem vir antes), o nome Dokpod em letras grandes de blocos, com a linha "Instalador do Dokpod <versão> · Altivy", em que `<versão>` é a versão do próprio instalador.
- **FR-002**: O logo DEVE usar o degradê de amarelo para o lima da marca (uma cor por linha) e a linha da versão em tom apagado quando o terminal aceita cor; com `NO_COLOR` definida e não vazia, DEVE sair sem cor.
- **FR-003**: O instalador DEVE aceitar a opção `--no-logo` em todos os comandos; com ela, o logo NÃO DEVE aparecer.
- **FR-004**: O logo NÃO DEVE aparecer quando a entrada ou a saída não for um terminal interativo ou quando o modo não interativo estiver ativo, mesmo sem `--no-logo`.
- **FR-005**: Sem UTF-8, o instalador DEVE usar uma versão ASCII do logo; em terminal estreito demais para o logo, DEVE mostrar só a linha da versão.
- **FR-006**: A presença ou a ausência do logo NÃO DEVE mudar o resultado, os arquivos gerados nem o código de saída de nenhum comando.
- **FR-007**: A ajuda do instalador e o contrato da CLI DEVEM documentar `--no-logo` e o comportamento automático em automação.

## Critérios de Sucesso (obrigatório)

### Resultados Mensuráveis

- **SC-001**: Em 100% das execuções num terminal interativo sem `--no-logo`, o logo aparece com a versão correta do instalador.
- **SC-002**: Em 100% das execuções com `--no-logo`, com saída redirecionada ou em modo não interativo, a saída não contém o logo nem códigos de cor do logo.
- **SC-003**: O logo não acrescenta tempo perceptível à execução: o tempo até a primeira mensagem do comando cresce menos de 50 ms em relação à mesma execução com `--no-logo`.
- **SC-004**: Todos os comandos do instalador mantêm os mesmos códigos de saída e arquivos gerados com e sem o logo nos testes existentes.

## Premissas

- O desenho é o da prévia aprovada (letras em blocos com sombra, 6 linhas e 50 colunas) e é original, desenhado para o Dokpod; nenhuma fonte de terceiros é incorporada.
- A versão exibida é a mesma que o instalador já conhece para a release que executa.
- O bootstrap `install.sh` só repassa `--no-logo` ao instalador, como já faz com as demais opções; ele não exibe logo próprio.
- Fora do escopo: animação, som, logo no console web e nos outros componentes, e configuração de cores pelo usuário.
