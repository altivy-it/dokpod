# Especificação da Feature: Instalador guiado do Dokpod

**Branch da feature**: `033-instalador-guiado`

**Criada em**: 2026-11-08

**Estado**: `draft` - aguardando revisão humana.

**Origem**: IA assistida, a partir do pedido do solicitante (Lincoln, `@lzocateli`); revisor humano a definir na revisão.

**Entrada**: Pedido do solicitante em 2026-11-08: "Faça uma análise profunda no projeto, se fizer sentido, crie um spec para criar um script ou alguma forma que um usuário possa executar para fazer a instalação da stack do projeto, existem alguns cenários possíveis: 1. O usuário quer instalar toda stack, ele não tem postgres nem nginx nem keycloak, 2. O usuário já tem postgres e/ou nginx, 3. Igual 2 e já possui alguma instância do keycloak, a ideia é criar um instalador único que guie totalmente a instalação, faça as migrations do projeto, criar os arquivos e gerar os certificados, tudo idempotente ou com possibilidade de recriar tudo".

**Depende de**: [004](../004-distribuicao-containerizada/spec.md) (imagens e matriz de distribuição), [001](../001-identidade-ambientes-auditoria/spec.md) (realm, ambientes, identidade dos agentes e auditoria), [024](../024-pipeline-ci-cd-groundwork/spec.md) (release assinada, manifesto e deploy hook), [031](../031-licenciamento-business-offline/spec.md) (edição Business e importação de licença).

## Contexto e diagnóstico

Análise do repositório em 2026-11-08, na `v0.7.0` (`46416a3`):

| Lacuna | Situação atual | Consequência para quem instala |
| --- | --- | --- |
| Caminho de instalação | A única stack é a de laboratório (`deploy/e2e`). Ela depende da plataforma `altivy-identity` (rede `identity-global`, caminhos do AltivyNotes), roda com `ASPNETCORE_ENVIRONMENT=Development`, usa IPs fixos e a role administrativa do PostgreSQL. | Ninguém fora da equipe consegue instalar o Dokpod sem reproduzir a infraestrutura interna. |
| Migrations | A API não aplica migrations, e a release não publica artefato que as aplique. Hoje é preciso SDK .NET e `dotnet ef`. O script `--idempotent` gerado pelo EF falha, porque as migrations usam SQL próprio, embrulhado pelo EF em blocos `DO`. Isso foi observado ao subir a stack local em 2026-11-08. | A instalação e a atualização exigem toolchain de desenvolvimento e conhecimento do schema. |
| Identidade dos agentes | Não há enrollment. O fingerprint do certificado do agente é inserido à mão em `dokpod.agent_identities`. | O primeiro host gerenciado exige SQL manual. |
| Certificados | Não há ferramenta. A CA local, o certificado da API (SAN do endpoint e EKU `serverAuth`), o PFX de cada agente e o TLS da borda foram gerados ad hoc. | A etapa mais propensa a erro fica sem guia, e um certificado errado só aparece como falha de mTLS. |
| Keycloak | Já existe reconciliação idempotente do realm (`tools/scripts/configure-keycloak.ps1`) e dos recursos por ambiente (`tools/scripts/provision-environment-authorization.ps1`). | Pode ser reaproveitada pelo instalador, para Keycloak novo ou existente. |
| Atualização | O deploy hook (`deploy/hooks/compose-deploy-hook.sh`) troca imagens por digest, mas não aplica migrations. | A atualização com migration nova continua manual. |

## Esclarecimentos

### Sessão 2026-11-08 (recomendações adotadas, sujeitas à aprovação)

- P: Que topologia o instalador cobre? → R: Plano de controle (web, BFF e API) em um único host Linux com Docker Engine ou Podman, mais o agente Linux nesse mesmo host. Hosts adicionais recebem um pacote de agente gerado pelo instalador. Alta disponibilidade, Kubernetes e o agente Windows ficam fora (o Windows já tem `deploy/agent/manage-windows-service.ps1`).
- P: O que o usuário precisa ter instalado? → R: Só um engine de containers suportado, com Compose. Toda a lógica do instalador, a geração de certificados, as migrations e a configuração do Keycloak rodam em containers; o host só orquestra. Isso segue a regra do projeto de toolchains em containers.
- P: Interativo ou automatizado? → R: Os dois. O modo guiado pergunta o cenário e cada dado faltante e mostra o plano antes de aplicar. O modo não interativo lê um arquivo de respostas sem segredos; os segredos vêm de arquivos ou variáveis indicados nele. Os dois modos produzem o mesmo resultado.
- P: O que significa idempotente aqui? → R: Reexecutar com as mesmas respostas não muda nada que já esteja correto, não regera segredos nem certificados válidos e não perde dados. Reexecutar com respostas diferentes mostra o que vai mudar e pede confirmação. "Recriar" é uma ação explícita, por componente ou total, sempre com confirmação. Ela nunca apaga banco ou volumes sem uma segunda confirmação nomeada.
- P: Como o instalador aplica migrations sem SDK? → R: A release passa a publicar um meio suportado de aplicar migrations a partir das imagens da própria versão, forward-only, com registro do que foi aplicado. O desenho (modo da imagem da API, imagem dedicada ou bundle) fica para o plano e pode exigir ADR.
- P: Com PostgreSQL existente, quais credenciais o instalador usa? → R: Uma credencial administrativa informada uma vez, usada só para criar o banco e a role de runtime do Dokpod com menor privilégio, e não guardada. Alternativa: o usuário entrega banco e role já criados, e o instalador só valida a conexão e os privilégios. O banco do Dokpod é sempre separado do banco do Keycloak.
- P: Com Keycloak existente, o que o instalador altera? → R: Só o realm do Dokpod (padrão `dokpod`), seus clients, roles, recursos e scopes, pela reconciliação já existente. Nunca o realm `master` nem outros realms. A credencial administrativa é informada no momento e não fica guardada.
- P: Com nginx existente, o que o instalador faz? → R: Gera o arquivo de site do Dokpod para o usuário incluir no nginx dele, valida a sintaxe em container e mostra o que falta (certificado, include, reload). Ele não altera configuração fora do arquivo gerado. Sem nginx, sobe a borda própria do Dokpod.
- P: Que certificados? → R: Para cada certificado (borda pública, API gRPC e agentes), o usuário escolhe entre gerar por uma CA local criada pelo instalador ou informar certificados da organização. O instalador valida SAN, EKU, validade e cadeia antes de usar. Emissão automática por ACME (Let's Encrypt) fica fora desta entrega.
- P: Quem é o primeiro administrador? → R: Num Keycloak novo, o instalador cria um usuário administrador com senha temporária, que deve ser trocada no primeiro login, e a senha temporária é entregue uma única vez. Num Keycloak existente, o usuário indica um usuário existente, que recebe as roles do Dokpod.
- P: Edição CE ou Business? → R: O usuário escolhe. A Business usa a imagem `dokpod-api-business`. Se o usuário tiver um arquivo de licença, o instalador a importa pelo caminho já existente; sem licença, a instalação funciona sem os recursos licenciados.
- P: O instalador também atualiza? → R: Sim. Reexecutar com uma versão mais nova verifica as imagens, exige backup confirmado do banco quando há migration nova, aplica as migrations e troca as imagens, respeitando a ordem servidor antes de agentes. O deploy hook continua válido para trocas sem migration.

### Sessão 2026-11-08 (revisão do solicitante)

O solicitante (Lincoln, `@lzocateli`) aprovou as recomendações de topologia, pré-requisitos, modos de execução, credenciais administrativas e atualização ("1 ok, 2 ok, 3 ok, 4 ok, … 6 ok").

- P: Como aplicar migrations sem SDK? → R: A decisão foi mantida, com um objetivo a mais: "pode ser para que o usuário tenha maior controle sobre o que será aplicado no banco". O meio publicado pela release deve permitir ver, antes de aplicar, quais migrations estão pendentes e o SQL exato de cada uma. Também deve permitir exportar esse SQL para revisão (por exemplo, por um DBA) e aplicá-lo fora do instalador. Nesse caso, o instalador só confere que o banco ficou na versão esperada. Os requisitos estão em FR-036 a FR-038.
- Correções do `/speckit-analyze` aprovadas pelo solicitante ("aplique as correções F1 a F6, com F2 opção a"):
  - **F1**: o histórico de migrations não guarda o SQL aplicado, então a conferência passa a comparar o schema real com a impressão do schema esperado (FR-042).
  - **F2, opção a**: a porta gRPC com mTLS dos agentes é a única exceção à borda como porta pública (FR-021, FR-022).
  - **F3**: com nginx existente, web e BFF só escutam em loopback ou em rede interna (FR-043).
  - **F4, F5 e F6**: ajustes nas tarefas: backup na primeira instalação, os dois engines nos cenários 2 e 3, e repasse das variáveis `{env}`.
- P: Quando alguém altera um arquivo SQL de migration fora do instalador (por exemplo, um DBA ajusta tablespace ou índice), o que o instalador deve fazer com a versão alterada? → R: Opção B (recomendada), escolhida pelo solicitante em resposta à diretriz "certifique que o instalador não tenha comandos SQL fixos, o ideal é usar arquivos SQL, assim pode ser usado pelo EfCore migrations e pode ser alterado externamente". O instalador aceita um diretório de SQL sobreposto. O plano mostra cada arquivo alterado com o hash original e o novo, aplica após confirmação nomeada e registra o desvio no estado e no relatório. O `verify` aceita o desvio só se ele foi registrado (FR-039 a FR-041).

## Cenários do Usuário e Testes (obrigatório)

### Jornada do Usuário 1 - Instalar tudo num host vazio (Prioridade: P1)

Um administrador sem PostgreSQL, nginx nem Keycloak executa o instalador num host Linux novo, responde às perguntas e termina com o Dokpod acessível no navegador, o host local gerenciado e um usuário administrador para entrar.

**Valor desta prioridade**: é o caminho de avaliação e adoção. Sem ele, o produto não é instalável por quem está fora do projeto.

**Teste independente**: num host Linux limpo com só o engine de containers, executar o instalador no modo guiado com o cenário "instalação completa", entrar no console pela borda com o administrador criado e ver o host local conectado com seus containers.

**Cenários de aceite**:

1. **Dado** um host só com engine de containers e Compose, **Quando** o usuário escolhe "instalação completa" e confirma o plano, **Então** o instalador sobe PostgreSQL, Keycloak, borda, web, BFF, API e agente, aplica as migrations, configura o realm, cadastra o primeiro ambiente com a identidade do agente e termina mostrando a URL de acesso e o que fazer em seguida.
2. **Dado** a instalação concluída, **Quando** o administrador entra pela URL mostrada, **Então** ele precisa trocar a senha temporária, entra no console e vê o host local conectado.
3. **Dado** a instalação concluída, **Quando** o usuário executa o instalador de novo com as mesmas respostas, **Então** nenhum container, segredo, certificado ou dado é alterado, e o instalador informa que está tudo convergido.
4. **Dado** que um pré-requisito falta (engine ausente ou abaixo da versão mínima, porta ocupada, DNS do nome público não resolve, pouco espaço), **Quando** o instalador faz as verificações iniciais, **Então** ele para antes de alterar qualquer coisa e diz o que corrigir.

---

### Jornada do Usuário 2 - Usar PostgreSQL e nginx que já existem (Prioridade: P2)

Um administrador que já opera PostgreSQL e/ou nginx quer que o Dokpod use essa infraestrutura, e não suba duplicatas.

**Valor desta prioridade**: em ambientes corporativos, banco e borda são serviços geridos. Duplicá-los é o principal motivo de recusa de instalação.

**Teste independente**: com um PostgreSQL e um nginx em containers à parte, executar o cenário "infraestrutura existente", conferir que nenhum PostgreSQL ou nginx novo foi criado, que o Dokpod usa uma role sem privilégio administrativo e que o site gerado, incluído no nginx existente, dá acesso ao console.

**Cenários de aceite**:

1. **Dado** um PostgreSQL existente e uma credencial administrativa informada, **Quando** o instalador prepara o banco, **Então** ele cria um banco e uma role de runtime exclusivos do Dokpod, aplica as migrations e configura a API com essa role, sem guardar a credencial administrativa.
2. **Dado** banco e role já criados pelo DBA, **Quando** o usuário os informa, **Então** o instalador valida conexão, versão e privilégios necessários e recusa uma role com privilégio a mais (superusuário) ou a menos, dizendo qual.
3. **Dado** um nginx existente, **Quando** o usuário escolhe usá-lo, **Então** o instalador gera o arquivo de site do Dokpod, valida a sintaxe e lista os passos manuais restantes, sem alterar a configuração do nginx fora desse arquivo.
4. **Dado** só um dos dois existente (por exemplo, PostgreSQL sim e nginx não), **Quando** o usuário responde, **Então** o instalador combina os modos: usa o que existe e provisiona o que falta.

---

### Jornada do Usuário 3 - Integrar a um Keycloak existente (Prioridade: P2)

Um administrador que já tem Keycloak, possivelmente com PostgreSQL e nginx, quer que o Dokpod use essa instância.

**Valor desta prioridade**: o Keycloak é a autoridade de identidade obrigatória, e organizações com Keycloak não aceitam uma segunda instância só para o Dokpod.

**Teste independente**: com um Keycloak existente que já tenha outro realm, executar o cenário "Keycloak existente", conferir que só o realm do Dokpod foi criado ou alterado, e entrar no console com um usuário existente que recebeu as roles do Dokpod.

**Cenários de aceite**:

1. **Dado** um Keycloak existente sem o realm do Dokpod, **Quando** o instalador o configura, **Então** ele cria o realm, os clients, as roles, os recursos e os scopes do Dokpod e guarda o segredo do client do BFF só no destino de segredos da instalação.
2. **Dado** um Keycloak que já tem o realm do Dokpod, **Quando** o instalador roda, **Então** ele reconcilia o que diverge do esperado, relata cada mudança e preserva usuários, sessões e customizações fora do contrato do Dokpod.
3. **Dado** outros realms na mesma instância, **Quando** o instalador termina, **Então** nenhum outro realm, nem o `master`, foi alterado.
4. **Dado** um Keycloak de versão abaixo da homologada ou sem acesso administrativo, **Quando** o instalador o verifica, **Então** ele para antes de alterar qualquer coisa, com o motivo.

---

### Jornada do Usuário 4 - Recriar componentes com segurança (Prioridade: P3)

O administrador precisa refazer parte da instalação (certificado vencido ou comprometido, segredo vazado, configuração corrompida) ou começar do zero, sem destruir dados por engano.

**Valor desta prioridade**: sem recriação guiada, o operador apaga arquivos à mão e perde chaves de Data Protection, identidades de agente ou o banco.

**Teste independente**: numa instalação existente, pedir a recriação dos certificados da borda e conferir que só eles mudaram. Depois, pedir a recriação total, recusar a segunda confirmação e conferir que banco e volumes ficaram intactos.

**Cenários de aceite**:

1. **Dado** uma instalação existente, **Quando** o usuário pede para recriar um componente (certificados, segredos, configuração, um serviço), **Então** o instalador mostra o que vai ser substituído e o efeito colateral (por exemplo, "os agentes precisarão do novo certificado da CA"), pede confirmação e recria só aquilo.
2. **Dado** um destino de segredo ou certificado que já existe, **Quando** o instalador roda sem pedido de recriação, **Então** ele nunca sobrescreve o destino, mesmo que o conteúdo pareça inválido; ele relata o problema e aponta a ação de recriar.
3. **Dado** um pedido de recriação total, **Quando** o usuário confirma, **Então** o instalador exige uma segunda confirmação, que nomeia banco, volumes e chaves que serão apagados, antes de apagá-los. Sem ela, recria todo o resto e preserva os dados.
4. **Dado** a troca da CA local, **Quando** ela é recriada, **Então** o instalador segue o procedimento de troca de CA (raiz antiga e nova aceitas durante a transição) e reemite os certificados dos agentes, sem deixar agentes conectados sem confiança.

---

### Jornada do Usuário 5 - Adicionar hosts e atualizar a versão (Prioridade: P3)

Depois da instalação, o administrador adiciona outros hosts Linux e, mais tarde, atualiza o Dokpod para uma versão nova.

**Valor desta prioridade**: um plano de controle com um só host não cumpre o propósito do produto, e uma atualização com migration manual reintroduz a dependência de SDK.

**Teste independente**: gerar o pacote de agente para um segundo host, aplicá-lo nesse host e ver o host conectado no console. Depois, executar o instalador com uma versão mais nova que tenha migration e conferir o backup exigido, a migration aplicada e a nova versão em todos os serviços.

**Cenários de aceite**:

1. **Dado** uma instalação existente, **Quando** o administrador pede um novo host gerenciado, **Então** o instalador cadastra o ambiente, emite o certificado do agente, registra a identidade e entrega um pacote com tudo de que o host precisa (certificado, CA, endpoint e definição do container). O pacote é protegido e não tem nenhum outro segredo da instalação.
2. **Dado** uma versão nova com migration, **Quando** o administrador executa a atualização, **Então** o instalador verifica a assinatura das imagens, exige um backup confirmado do banco, aplica as migrations, atualiza o plano de controle e depois os agentes, e confirma a versão de cada serviço.
3. **Dado** uma versão anterior à instalada, **Quando** o usuário tenta "atualizar" para ela, **Então** o instalador recusa e aponta o procedimento de rollback e restauração existente.

### Casos de Borda

- O instalador é interrompido no meio (queda de energia, Ctrl+C): a próxima execução detecta o estado parcial e continua de onde parou, sem duplicar banco, realm, ambiente ou identidade.
- O nome público muda depois da instalação: a reexecução com o novo nome mostra que borda, BFF, redirect URIs do Keycloak e certificado da borda serão alterados, e pede confirmação.
- A porta da borda ou da API já está ocupada por outro processo: o instalador para nas verificações iniciais e oferece outra porta.
- O PostgreSQL existente está numa versão abaixo da suportada ou sem extensão requerida: o instalador para com o motivo antes de criar a role.
- O certificado informado pela organização não tem o SAN ou o EKU exigido, ou está vencido: o instalador recusa e diz o que falta.
- Engine Podman rootless: o instalador usa o socket do usuário, ajusta a resolução de nomes curtos de imagem e não exige privilégio de root além do que o Podman exige.
- Docker e Podman no mesmo host: o usuário escolhe qual engine o agente local gerencia. O socket nunca é exposto por rede.
- A release pedida não existe, ou uma imagem tem assinatura inválida: o instalador para antes de qualquer alteração.
- Host sem acesso à internet: o instalador aceita imagens pré-carregadas (exportadas da release), verificadas pelo manifesto e pela assinatura.
- O banco tem migrations de uma versão mais nova que a pedida: o instalador recusa, porque as migrations são forward-only.
- Um arquivo de migration alterado à mão foi aplicado sem passar pelo diretório sobreposto: a conferência do schema aponta, objeto a objeto, a diferença não registrada e recusa seguir. Para continuar, o desvio precisa ser registrado com o mesmo arquivo pelo diretório sobreposto, com confirmação nomeada, ou o banco precisa voltar ao esperado.
- O diretório sobreposto altera um arquivo de uma migration que já está aplicada: o instalador avisa que a alteração não tem efeito nesse banco e só a registra se o usuário confirmar.
- O arquivo de respostas contém um segredo em texto: o instalador recusa e indica como referenciar o segredo por arquivo.

## Requisitos (obrigatório)

### Requisitos Funcionais

**Entrada e plano**

- **FR-001**: O instalador DEVE oferecer um ponto de entrada único que, no modo guiado, pergunte o cenário (instalação completa, PostgreSQL existente, nginx existente, Keycloak existente, em qualquer combinação), a versão, a edição (CE ou Business), o nome público, o engine do agente local e a origem de cada certificado.
- **FR-002**: O instalador DEVE aceitar um arquivo de respostas para execução não interativa, com o mesmo resultado do modo guiado. Esse arquivo DEVE recusar segredos em texto e aceitar apenas referências a arquivos ou variáveis de ambiente.
- **FR-003**: Antes de alterar qualquer coisa, o instalador DEVE verificar os pré-requisitos (engine e versão mínima, Compose, portas livres, espaço em disco, resolução do nome público, alcance de PostgreSQL e Keycloak existentes e suas versões) e parar com a correção sugerida quando algum falhar.
- **FR-004**: O instalador DEVE mostrar o plano de alterações (o que cria, altera, mantém e recria) e só aplicar após confirmação. Um modo de simulação DEVE mostrar o plano sem efeitos.

**Imagens e versão**

- **FR-005**: O instalador DEVE usar somente imagens de uma release publicada, referenciadas por digest do manifesto da release, e verificar a assinatura de cada imagem antes de usá-la. Ele não aceita tag livre nem `latest`.
- **FR-006**: O instalador DEVE suportar host sem internet a partir de imagens e manifesto previamente exportados, com a mesma verificação de assinatura.

**Banco e migrations**

- **FR-007**: No cenário sem PostgreSQL, o instalador DEVE provisionar instâncias ou bancos separados para o Dokpod e para o Keycloak, com volumes persistentes e credenciais geradas.
- **FR-008**: No cenário com PostgreSQL existente, o instalador DEVE criar, com uma credencial administrativa usada uma única vez e não persistida, um banco e uma role de runtime exclusivos do Dokpod, sem privilégio de superusuário. Ele DEVE também aceitar banco e role já existentes, validando os privilégios e recusando excesso ou falta.
- **FR-009**: A release DEVE publicar um meio de aplicar as migrations do Dokpod sem SDK nem toolchain no host, executado em container a partir da mesma versão das imagens.
- **FR-010**: A aplicação de migrations DEVE ser forward-only, aplicar só as pendentes, registrar o que aplicou, ser segura para reexecução e recusar um banco com migrations de versão mais nova.
- **FR-011**: Antes de aplicar migrations num banco que já tem dados, o instalador DEVE exigir a confirmação de um backup recente, ou fazê-lo quando o PostgreSQL foi provisionado por ele, seguindo o runbook de backup existente.
- **FR-036**: Antes de aplicar migrations, o instalador DEVE mostrar a versão atual do banco, a lista de migrations pendentes e um resumo do efeito de cada uma (objetos criados, alterados ou removidos). Ele só aplica após confirmação explícita, mesmo quando o restante do plano já foi confirmado.
- **FR-037**: O usuário DEVE poder exportar, sem efeitos no banco, o SQL exato das migrations pendentes para a versão alvo, num arquivo com versão de origem, versão de destino e hash de conferência. Esse SQL DEVE ser executável fora do instalador pelas ferramentas padrão do PostgreSQL, sem o problema atual do script idempotente do EF.
- **FR-038**: Quando o usuário escolher aplicar as migrations por conta própria, o instalador DEVE pausar nessa etapa. Na retomada, DEVE conferir que o banco está exatamente na versão esperada e que o histórico de migrations confere com o exportado. Ele recusa seguir se faltar migration ou houver divergência e diz qual.
- **FR-039**: O instalador e o modo administrativo NÃO DEVEM ter SQL escrito no código. Todo SQL que executam fica em arquivos `.sql` versionados: migrations, criação de banco e roles, concessões, validação de privilégios, consultas de status e verificação, e contagens de conferência. Os arquivos de migration são os mesmos que as migrations do EF Core carregam, uma fonte só. Os demais arquivos ficam ao lado deles, com parâmetros nomeados em vez de valores concatenados.
- **FR-040**: A release DEVE disponibilizar esses arquivos para leitura e alteração externa, na imagem e como pacote, com um manifesto que liste cada arquivo, sua finalidade e seu hash.
- **FR-041**: O usuário DEVE poder informar um diretório de SQL sobreposto, que substitui arquivos da release com o mesmo caminho. Cada arquivo alterado aparece no plano com o hash original e o novo e exige confirmação que o nomeie. O desvio fica registrado no estado e no relatório. Um arquivo sobreposto sem correspondente na release, ou um diretório sem o manifesto, é recusado.
- **FR-042**: A release DEVE publicar a impressão do schema esperado da versão: tabelas, colunas (tipo, nulidade e default), índices, restrições, funções (assinatura e hash do corpo) e gatilhos do schema `dokpod`, sem as partições criadas em execução e sem concessões. A conferência DEVE comparar essa impressão com o schema real do banco, porque o histórico de migrations não guarda o SQL aplicado. A diferença só é aceita quando corresponde a um desvio registrado: a impressão observada depois da aplicação confirmada com o diretório sobreposto, guardada no estado. Diferença não registrada é recusada e listada objeto a objeto.

**Identidade**

- **FR-012**: No cenário sem Keycloak, o instalador DEVE provisionar o Keycloak na versão homologada, com banco próprio, e configurar o realm do Dokpod.
- **FR-013**: Em qualquer cenário, o instalador DEVE criar ou reconciliar o realm do Dokpod (clients, roles, recursos, scopes e políticas) de forma idempotente, relatando cada divergência corrigida. Ele NUNCA DEVE alterar o realm `master` nem outros realms.
- **FR-014**: No Keycloak novo, o instalador DEVE criar o primeiro administrador do Dokpod com senha temporária de troca obrigatória e entregá-la uma única vez. No Keycloak existente, DEVE atribuir as roles do Dokpod a um usuário indicado.
- **FR-015**: A credencial administrativa do Keycloak DEVE ser usada só durante a execução e NUNCA DEVE ser gravada. O runtime do Dokpod não recebe credencial administrativa do realm.

**Certificados**

- **FR-016**: O instalador DEVE oferecer, por certificado (borda pública, API gRPC e agentes), a escolha entre gerar por uma CA local criada por ele ou usar certificados da organização.
- **FR-017**: Todo certificado, gerado ou informado, DEVE ser validado antes do uso: SAN do nome usado para alcançar o serviço, EKU adequado (servidor ou cliente), validade, cadeia até a raiz configurada e correspondência entre chave e certificado.
- **FR-018**: A chave privada da CA local DEVE ficar no destino protegido de segredos da instalação, com acesso restrito ao usuário que opera o instalador, e NUNCA DEVE ser montada em serviço de runtime.
- **FR-019**: O instalador DEVE avisar com antecedência configurável, na reexecução ou numa verificação dedicada, quando um certificado gerenciado por ele estiver perto do vencimento, e oferecer a renovação.

**Arquivos, borda e serviços**

- **FR-020**: O instalador DEVE gerar a definição dos serviços (Compose), as configurações não secretas e a configuração de borda num diretório de instalação escolhido pelo usuário, fora do repositório. Os segredos ficam num destino separado, com permissões restritas.
- **FR-021**: No cenário sem nginx, o instalador DEVE subir a borda do Dokpod, a única porta pública HTTP, encaminhando o console, o BFF e o SignalR. No cenário com nginx existente, DEVE gerar o arquivo de site, validar a sintaxe em container e listar os passos manuais restantes, sem alterar outros arquivos do nginx.
- **FR-022**: Os serviços DEVEM rodar com configuração de produção (sem modo de desenvolvimento), sem IPs fixos obrigatórios e só com redes internas, com duas exceções. A primeira é a borda. A segunda, só quando o usuário pede hosts adicionais, é a porta gRPC da API para os agentes. Essa porta exige certificado de cliente (mTLS) emitido para um ambiente cadastrado e não atende nenhuma outra rota. O socket do engine só é montado no agente e nunca é publicado por rede.
- **FR-023**: Os arquivos gerados DEVEM ser compatíveis com o deploy hook existente, para que atualizações sem migration continuem possíveis por ele.
- **FR-043**: Com nginx existente, web e BFF DEVEM escutar só em loopback (`127.0.0.1` ou `::1`) ou numa rede interna compartilhada com o nginx do usuário. Outro endereço é recusado, para que nenhum tráfego externo chegue ao BFF sem passar pela borda.

**Ambiente e agentes**

- **FR-024**: O instalador DEVE cadastrar o primeiro ambiente (host local), provisionar seus recursos de autorização no Keycloak, emitir o certificado do agente, registrar a identidade do agente vinculada ao ambiente e subir o agente com o engine escolhido. Tudo isso DEVE ficar registrado na auditoria do Dokpod, sem SQL manual.
- **FR-025**: O instalador DEVE gerar, sob pedido, o pacote de um novo host Linux (ambiente cadastrado, certificado e identidade do agente, CA, endpoint e definição do container), sem nenhum outro segredo da instalação, e permitir revogar a identidade de um host pelo caminho de revogação já existente.

**Idempotência e recriação**

- **FR-026**: Reexecutar com as mesmas respostas NÃO DEVE alterar nada que já esteja correto: não regera segredos nem certificados válidos e não recria containers cuja configuração e imagem não mudaram. A execução termina relatando "convergido" ou a lista do que mudou.
- **FR-027**: Uma execução interrompida DEVE poder ser retomada, convergindo sem duplicar banco, role, realm, ambiente, identidade ou certificado.
- **FR-028**: Um destino de segredo ou certificado existente NUNCA DEVE ser sobrescrito sem pedido explícito de recriação daquele item (falha fechada).
- **FR-029**: O instalador DEVE oferecer recriação por componente (certificados, segredos, configuração, serviço) e recriação total. Cada uma DEVE mostrar o efeito e pedir confirmação. A remoção de banco, volumes, chaves de Data Protection ou identidades exige uma segunda confirmação que os nomeie.
- **FR-030**: A recriação da CA local DEVE seguir o procedimento de troca da CA (raízes antiga e nova aceitas durante a transição) e reemitir os certificados afetados.

**Licença e atualização**

- **FR-031**: Na edição Business, o instalador DEVE usar a imagem Business e, se o usuário informar um arquivo de licença, importá-lo pelo caminho de importação existente. Sem licença, a instalação conclui e informa como importá-la depois.
- **FR-032**: Reexecutar com uma versão mais nova DEVE atualizar a instalação: verificar as imagens, exigir backup quando houver migration nova (FR-011), aplicar as migrations, atualizar o plano de controle antes dos agentes e confirmar a versão de cada serviço. Uma versão mais antiga que a instalada é recusada, com referência ao procedimento de rollback.

**Segurança e saída**

- **FR-033**: Nenhum segredo (senhas, client secrets, chaves privadas, connection strings, senha temporária após a entrega) DEVE aparecer na saída, nos logs do instalador, nos arquivos não secretos ou em argumentos de processo visíveis a outros usuários.
- **FR-034**: O instalador DEVE registrar, no diretório de instalação, um relatório de cada execução sem segredos: versão, cenário, plano, o que mudou, verificações e resultado.
- **FR-035**: Toda ferramenta do instalador DEVE oferecer ajuda sem efeitos colaterais e códigos de saída distintos para sucesso, falha de pré-requisito, recusa de confirmação e erro na aplicação.

### Entidades Principais

- **Instalação**: o Dokpod num host. Tem versão, edição, cenário, nome público, diretório de instalação, destino de segredos e o estado de cada componente (provisionado pelo instalador ou existente do usuário).
- **Componente**: unidade convergida pelo instalador (PostgreSQL, Keycloak, borda, web, BFF, API, agente, realm, migrations, certificados, segredos). Tem origem (provisionado ou existente), estado esperado, estado observado e se pode ser recriado.
- **Arquivo de respostas**: as escolhas da instalação sem segredos, com referências a onde estão os segredos. Permite reexecutar e automatizar.
- **Segredo da instalação**: valor sensível gerado ou recebido (credenciais do banco, client secret do BFF, senhas de PFX, chave da CA), guardado só no destino protegido e nunca exibido.
- **Certificado gerenciado**: borda, API ou agente, com origem (CA local ou organização), nomes, validade e o componente que o usa.
- **Pacote de host**: o necessário para um novo agente Linux se conectar a um ambiente cadastrado, e nada além disso.
- **Relatório de execução**: o registro sem segredos de cada execução e do resultado.

## Critérios de Sucesso (obrigatório)

### Resultados Mensuráveis

- **SC-001**: Num host Linux limpo com só o engine de containers, um administrador sem conhecimento prévio do projeto conclui a instalação completa e entra no console em até 20 minutos, sem editar arquivo nem executar comando além do instalador (excluído o tempo de download das imagens).
- **SC-002**: Os três cenários (completo, PostgreSQL ou nginx existente, Keycloak existente) e suas combinações concluem com Docker Engine e com Podman rootless, e terminam com o host local conectado e o login funcionando.
- **SC-003**: Uma segunda execução com as mesmas respostas termina sem alterar nenhum container, segredo, certificado, banco ou realm, conferido por comparação do estado antes e depois.
- **SC-004**: Uma execução interrompida em cada etapa e retomada converge para o mesmo estado de uma execução sem interrupção, sem recurso duplicado.
- **SC-005**: Nenhum segredo da instalação aparece na saída, no relatório, nos arquivos não secretos ou na lista de processos, verificado por varredura com valores canário.
- **SC-006**: Em Keycloak e PostgreSQL existentes, nenhum realm além do do Dokpod e nenhum banco ou role além dos do Dokpod é alterado, verificado por comparação antes e depois.
- **SC-007**: A atualização entre duas versões consecutivas com migration nova conclui sem SDK no host, com backup confirmado e a nova versão em todos os serviços.
- **SC-008**: A recriação total recusada na segunda confirmação preserva 100% dos dados do banco, das chaves de Data Protection e das identidades de agente.
- **SC-009**: Um novo host recebe o pacote gerado e aparece conectado no console em até 5 minutos depois de aplicado, sem SQL manual.
- **SC-010**: Para toda versão alvo, o SQL exportado e aplicado manualmente leva o banco ao mesmo estado (schema e histórico de migrations) que a aplicação feita pelo instalador, conferido por comparação de schema.
- **SC-011**: Nenhum comando SQL aparece escrito no código do instalador nem no do modo administrativo, conferido por varredura do código. Uma alteração externa num arquivo de migration aparece no plano e no relatório em 100% dos casos, e só é aplicada com a confirmação nomeada. Uma alteração aplicada fora do instalador que mude o schema é detectada pela conferência em 100% dos casos de teste: coluna, índice, restrição, função e gatilho.

## Premissas

- O host do plano de controle é Linux (x86-64) com Docker Engine ≥ 27 ou Podman ≥ 5, com Compose, conforme a matriz de engines homologados. macOS e Windows como host do plano de controle ficam fora.
- A instalação atende um único host de plano de controle; alta disponibilidade, réplicas e Kubernetes ficam fora.
- O agente Windows continua pelo script próprio, e o instalador só pode gerar o certificado e cadastrar o ambiente dele.
- Emissão automática por ACME fica fora; o usuário traz certificado público ou usa a CA local (adequada a redes internas, com a raiz distribuída explicitamente).
- O instalador é distribuído com a release, assinado como os demais artefatos, e roda em containers a partir das imagens da release. O formato exato é decidido no plano.
- As versões homologadas de PostgreSQL e Keycloak são as da matriz de distribuição (`docs/distribuicao.md`).
- O backup gerado pelo instalador segue o runbook existente (`docs/runbooks/backup-restore-e-atualizacao.md`); restauração continua um procedimento do runbook.
- A importação de licença Business usa o fluxo da spec 031; a emissão de licença não faz parte do instalador.
- Mudanças em contrato, identidade ou fronteira privilegiada desta spec (meio de migration na release, registro de identidade de agente fora do console, uso de credencial administrativa do Keycloak e do PostgreSQL) exigem threat model e, se forem decisões duradouras, ADR `proposed` no plano.
- A stack de laboratório (`deploy/e2e`) continua existindo para desenvolvimento e não é substituída por esta entrega.
