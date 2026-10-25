# Especificação da Funcionalidade: Pipeline único de CI/CD pelo Altivy.Groundwork

**Branch da Funcionalidade**: `024-pipeline-ci-cd-groundwork`

**Criada em**: 2026-10-24

**Status**: Aprovada (histórias 1 a 3) - execução autorizada pelo solicitante em 2026-10-25 ("Groundwork já é um projeto em produção, então precisa terminar o que for preciso no dokpod"). A história 4 segue bloqueada pelo GO da spec 005 (FR-011).

**Origem**: IA assistida, a pedido do mantenedor para executar a fase 6 do plano de CI/CD.

**Revisor humano**: lzocateli (solicitante). A aprovação não autoriza publicação nem deploy (FR-011).

**Entrada**: Descrição fornecida: "Adotar no Dokpod o processo único de CI/CD distribuído pelo
Altivy.Groundwork (fase 6 do plano `altivy-groundwork/docs/ci-cd/plano-ci-cd-via-groundwork.md`):
primeiro `ci`, `dependency-updates` e `repository-policy`, com commits assinados exigidos e o ruleset
legado `Protecao exclusiva da main` substituído; depois `release`, `publish-containers`,
`publish-github-assets` e `deploy` somente após o GO da spec 005."

> Redija esta especificação em português brasileiro (pt-BR). Preserve identificadores, nomes de API, comandos e termos técnicos necessários.

## Cenários de Usuário e Testes *(obrigatório)*

### História de Usuário 1 - Integrar mudanças por um único gate verificável (Prioridade: P1)

Como mantenedor, quero que todo pull request para a `main` seja aprovado ou reprovado por um único
check agregado, que reúna os testes atuais de backend, frontend e imagens, a varredura de secrets
no histórico completo, a validação do título em Conventional Commits, a análise dos workflows e a
conferência dos arquivos gerenciados pelo Groundwork, para não depender de vários checks
obrigatórios configurados à mão.

**Motivo da prioridade**: é a base das demais histórias. As políticas do repositório e a release
dependem de um check obrigatório confiável, e hoje a branch `development`, que não existe mais
no fluxo, ainda dispara o CI.

**Teste independente**: abrir um PR de teste com título inválido, outro com um workflow
inválido e outro com uma mudança válida, e conferir que só o último deixa o check agregado verde.

**Cenários de aceitação**:

1. **Dado** um PR com mudança válida e título em Conventional Commits, **Quando** o CI termina,
   **Então** o check agregado fica verde somente se backend, frontend, imagens, varredura de
   secrets, título, workflows e arquivos gerenciados tiverem sucesso.
2. **Dado** qualquer job obrigatório que falhe, seja cancelado ou pulado, **Quando** o CI termina,
   **Então** o check agregado reprova o PR e informa quais jobs não tiveram sucesso.
3. **Dado** um arquivo gerenciado pelo Groundwork alterado localmente, **Quando** o CI roda,
   **Então** o check agregado reprova o PR e orienta a mudança pelo catálogo.
4. **Dado** um push em uma branch que não seja a `main`, **Quando** ele acontece, **Então** o
   CI não é disparado fora de pull request.
5. **Dado** dois eventos do mesmo PR em sequência, **Quando** o segundo chega com o primeiro em
   andamento, **Então** o primeiro não é cancelado, para não deixar um check agregado com falha
   no mesmo commit.

---

### História de Usuário 2 - Proteger a `main` e as tags por contrato versionado (Prioridade: P1)

Como mantenedor, quero que as regras da `main`, das tags `v*`, das opções de merge e dos
Environments venham de um contrato versionado e auditável, aplicado e conferido por ferramenta,
para que um admin consiga fazer merge do próprio PR sem aprovação de terceiros, mas nunca com o
check agregado vermelho, sem commits assinados ou sem histórico linear.

**Motivo da prioridade**: a proteção atual combina branch protection clássica com
`enforce_admins` e um ruleset com bypass irrestrito para admins, o que permite contornar o CI e
diverge dos demais repositórios.

**Teste independente**: depois de aplicar o contrato, conferir pela ferramenta que não há
divergências e tentar fazer merge como admin de um PR com o check agregado vermelho; o merge deve
ser recusado. Um PR verde do próprio admin deve ser aceito sem aprovação de terceiros.

**Cenários de aceitação**:

1. **Dado** o contrato aplicado, **Quando** um admin tenta fazer merge com o check agregado
   vermelho, **Então** o merge é recusado sem possibilidade de bypass.
2. **Dado** um PR verde do próprio admin, **Quando** ele faz o merge, **Então** o merge é aceito
   por bypass em modo PR, somente por squash e com commit assinado.
3. **Dado** um push direto ou force push na `main`, **Quando** é tentado por qualquer ator,
   **Então** é recusado.
4. **Dado** a criação, alteração ou deleção de uma tag `v*`, **Quando** feita por quem não for o
   GitHub App de release, **Então** é recusada.
5. **Dado** o ruleset legado `Protecao exclusiva da main` e a proteção clássica, **Quando** o
   contrato é reconciliado, **Então** eles só são removidos depois que o check agregado tiver
   sido comprovado verde em PR, e a remoção fica registrada.

---

### História de Usuário 3 - Receber atualizações de dependências compatíveis com o fluxo (Prioridade: P2)

Como mantenedor, quero que as atualizações automáticas de dependências cubram actions, pacotes
.NET, pacotes do frontend e imagens base. Os PRs devem usar mensagens em Conventional Commits
coerentes com o impacto na release e nunca alterar os arquivos gerenciados do Groundwork.

**Motivo da prioridade**: reduz o risco de supply chain, mas depende do gate da história 1 para
ser seguro.

**Teste independente**: disparar a verificação de dependências e conferir que os PRs abertos
usam os prefixos esperados, ficam agrupados por ecossistema e não tocam arquivos gerenciados.

**Cenários de aceitação**:

1. **Dado** uma atualização de dependência de runtime, **Quando** o PR é aberto, **Então** o
   título indica uma correção que gera release patch.
2. **Dado** uma atualização de ferramenta de desenvolvimento, action ou imagem base, **Quando** o
   PR é aberto, **Então** o título não gera release.
3. **Dado** um workflow gerenciado pelo Groundwork, **Quando** há versão nova de uma action usada
   nele, **Então** nenhum PR de dependência o altera.

---

### História de Usuário 4 - Lançar, publicar e implantar a partir de uma release (Prioridade: P3, bloqueada pelo GO da spec 005)

Como mantenedor, quero que versões do Dokpod sejam lançadas por Release PR e tag única. A
publicação deve incluir as imagens por digest, o pacote do agente Windows e um manifesto atestado
da release. O deploy em `quality` e `production` deve aceitar somente releases publicadas, com
redeploy e rollback da mesma release.

**Motivo da prioridade**: só pode começar depois que a spec 005 tiver GO humano, conforme
[release-readiness](../../docs/release-readiness.md) e a constituição (princípio VI). Enquanto o
veredito for NO-GO, esta história não é executável.

**Teste independente**: com o GO registrado, lançar uma versão candidata e demonstrar em
`quality` o deploy, o redeploy e o rollback da mesma release, com recusa de artefatos que não
pertençam a uma release.

**Cenários de aceitação**:

1. **Dado** o veredito NO-GO da spec 005, **Quando** alguém tenta executar esta história,
   **Então** ela permanece bloqueada.
2. **Dado** o GO humano registrado e um Release PR aprovado, **Quando** ele é integrado,
   **Então** a tag é criada pelo App de release e os artefatos são publicados com digest e
   manifesto atestado, sem recompilar artefatos já listados.
3. **Dado** uma release publicada, **Quando** o deploy em `quality` é solicitado, **Então** ele
   usa somente os artefatos do manifesto; deploy de branch, rascunho ou prerelease em
   `production` é recusado.
4. **Dado** uma versão abaixo do mínimo de rollback registrado, **Quando** o rollback é pedido,
   **Então** ele é recusado com orientação de roll-forward.

---

### Casos de Borda

- O CI de um PR do Dependabot ou de fork roda sem secrets e mesmo assim produz um check agregado
  confiável.
- O GitHub App de release ou seus secrets estão ausentes: o fluxo de release falha fechado,
  sem criar tags.
- Commits do App de release ou do Dependabot sem assinatura reconhecida: o merge por squash pela
  interface continua gerando commit assinado na `main`.
- O contrato do repositório diverge da configuração real: a conferência aponta a divergência e
  não altera nada sem o modo de sincronização.
- Um arquivo gerenciado precisa de correção urgente: a correção vai para o catálogo do
  Groundwork, e o projeto a recebe por atualização, nunca por edição local.
- A branch `development` ou workflows antigos continuam referenciados em documentação ou scripts.

## Requisitos *(obrigatório)*

### Requisitos Funcionais

- **FR-001**: O repositório DEVE usar os componentes `ci`, `dependency-updates` e
  `repository-policy` do Altivy.Groundwork, registrados no manifesto do Groundwork, sem o
  componente `core`, salvo decisão no plano.
- **FR-002**: O CI DEVE preservar os jobs atuais de backend, frontend e imagens, executados em
  containers, e agregá-los no check `Result` junto com higiene de PR, varredura de secrets no
  histórico completo e conferência dos arquivos gerenciados.
- **FR-003**: O check `Result` DEVE falhar fechado quando qualquer job obrigatório não terminar
  com sucesso, inclusive pulado ou cancelado.
- **FR-004**: O CI DEVE ser disparado somente por pull request, push na `main` e execução manual;
  o gatilho da branch `development` DEVE ser removido.
- **FR-005**: O workflow `gitleaks.yml` atual DEVE ser substituído pela varredura de secrets
  gerenciada, preservando a configuração local de exceções do Dokpod.
- **FR-006**: As regras da `main` DEVEM exigir PR, squash, histórico linear, commits assinados e o
  check `Result` sem bypass, com bypass de admin somente em modo PR para a exigência de aprovação.
- **FR-007**: Tags `v*` DEVEM poder ser criadas, alteradas ou apagadas somente pelo GitHub App de
  release.
- **FR-008**: O ruleset legado `Protecao exclusiva da main` e a branch protection clássica DEVEM
  ser removidos somente após o check `Result` ter sido comprovado verde em PR, por reconciliação
  registrada do contrato.
- **FR-009**: As atualizações automáticas de dependências DEVEM cobrir actions, NuGet do backend,
  npm do frontend e imagens base, com mensagens em Conventional Commits coerentes com o impacto
  na release, e DEVEM excluir os workflows gerenciados.
- **FR-010**: A documentação de contribuição e operação DEVE descrever o novo fluxo de integração
  e as políticas, sem referências à branch `development` nem aos checks obrigatórios antigos.
- **FR-011**: A história 4 (`release`, `publish-containers`, `publish-github-assets` e `deploy`)
  NÃO DEVE ser executada enquanto a spec 005 não tiver GO humano registrado; o GO não autoriza
  publicação nem deploy sem solicitação explícita.
- **FR-012**: Quando executada, a história 4 DEVE manter a versão em `backend/Directory.Build.props`
  (como `VersionPrefix`) e `frontend/web/package.json`, publicar imagens por digest e o pacote do
  agente Windows, e configurar `quality` e `production` como Environments de deploy que aceitam
  somente tags `v*`, sem deploy automático em `production`.
- **FR-013**: Nenhuma mudança DEVE expor secrets, sockets de engine ou credenciais em logs,
  artefatos ou no repositório; actions DEVEM ser fixadas por SHA, com permissões mínimas por job.

### Entidades Principais

- **Contrato de políticas do repositório**: versão desejada de rulesets, opções de merge,
  permissões do token de workflow e Environments, conferida e reconciliada por ferramenta.
- **Manifesto do Groundwork**: registro dos componentes instalados, parâmetros, versão do
  catálogo e hash de cada arquivo gerenciado.
- **Check agregado (`Result`)**: único check obrigatório da `main`, derivado dos jobs do CI.
- **Release** (história 4): tag `vX.Y.Z`, notas e manifesto atestado com os artefatos e seus
  digests.

## Critérios de Sucesso *(obrigatório)*

### Resultados Mensuráveis

- **SC-001**: 100% dos PRs para a `main` passam a ter exatamente um check obrigatório, e nenhum
  PR com job obrigatório falho, pulado ou cancelado consegue ser integrado.
- **SC-002**: A conferência das políticas do repositório termina sem divergências logo após a
  reconciliação e continua sem divergências após 30 dias.
- **SC-003**: Um admin integra o próprio PR verde sem aprovação de terceiros em menos de 2
  minutos após o check agregado concluir, e 0 tentativas de merge com o check vermelho são
  aceitas.
- **SC-004**: Nenhum PR de atualização de dependência altera arquivo gerenciado, e todos usam
  título aceito pela validação de Conventional Commits.
- **SC-005**: Não restam referências à branch `development` nem aos checks obrigatórios antigos
  na documentação ou nos workflows.
- **SC-006** (história 4, após GO): deploy, redeploy e rollback da mesma release são
  demonstrados em `quality` com 100% dos artefatos implantados pertencentes ao manifesto.

## Premissas

- O GitHub App `Altivy Release` (ID 5441343) já está instalado na organização `altivy-it`, com
  `RELEASE_APP_ID` e `RELEASE_APP_PRIVATE_KEY` disponíveis para o repositório.
- O Altivy.Groundwork 0.5.0 é a versão de referência; mudanças em arquivos gerenciados vão para o
  catálogo, nunca para o Dokpod.
- O repositório é público, então rulesets, Environments com revisores e atestação estão
  disponíveis.
- Commits criados por squash pela interface do GitHub são assinados e atendem à exigência de
  assinatura; commits do App de release devem ser validados na execução.
- Os Environments atuais `ci`, `copilot`, `production` e `quality` são revisados no plano; o
  ruleset `Protect release tags` é reavaliado no plano frente ao `gw-release-tags`.
- Os testes e builds continuam em containers, conforme a constituição.
- Esta feature não altera o veredito NO-GO de [release-readiness](../../docs/release-readiness.md).
