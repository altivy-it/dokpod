# Especificação de Feature: Portal de documentação do Dokpod

**Branch da feature**: `034-portal-documentacao`

**Criada em**: 2026-11-17

**Status**: Rascunho

**Origem**: IA assistida, a partir do plano transversal de portais aprovado pelo solicitante em 2026-11-17.

**Entrada**: Criar o piloto de documentação pública versionada do Dokpod, alinhado à marca Altivy, em português, inglês e espanhol, com escolha inicial de idioma pelo navegador e tema claro, escuro ou sistema. Validar o build e a publicação do artefato estático por CI, sem configurar DNS, TLS ou deploy de produção.

## Cenários de usuário e testes

### História 1 — Consultar o manual da versão correta (Prioridade: P1)

Como pessoa que instala ou opera o Dokpod, quero encontrar instruções para a linha do produto que utilizo, para seguir procedimentos compatíveis com essa versão.

**Por que esta prioridade**: Instruções de outra linha podem induzir configuração ou operação incorreta. A navegação por versão é o valor essencial do portal.

**Teste independente**: Abrir duas linhas de documentação de demonstração e confirmar que ambas exibem conteúdo distinto, preservam o identificador do tópico e não redirecionam silenciosamente para outra linha.

**Cenários de aceitação**:

1. **Dado** um portal com duas linhas publicadas, **quando** a pessoa escolhe uma linha, **então** o conteúdo, a navegação e o rótulo apresentados pertencem à linha escolhida.
2. **Dado** um tópico inexistente na linha escolhida, **quando** a pessoa troca de linha, **então** o portal informa que o tópico não existe nessa linha e oferece o índice dessa linha sem abrir outra versão automaticamente.
3. **Dado** um link profundo válido para uma linha, **quando** a pessoa o abre diretamente ou atualiza a página, **então** permanece no mesmo tópico e linha.

### História 2 — Ler no idioma e tema preferidos (Prioridade: P1)

Como pessoa leitora, quero acessar o manual em português, inglês ou espanhol e escolher tema claro, escuro ou sistema, para ler com conforto sem perder o tópico ou a versão.

**Por que esta prioridade**: Idiomas e preferências visuais foram requisitos aprovados do portal e precisam funcionar desde o piloto, não ser uma customização posterior.

**Teste independente**: Validar fixtures de cada idioma, selecionar os três temas e alternar idioma/tema sem perder linha nem tópico.

**Cenários de aceitação**:

1. **Dado** que a pessoa abre a entrada sem locale explícito e sem escolha manual salva, **quando** o navegador prefere inglês, espanhol ou português, **então** o portal oferece o idioma correspondente; idioma não reconhecido resulta em português.
2. **Dado** um URL com idioma ou tópico explícito, **quando** a pessoa o abre, **então** o portal respeita o URL e não redireciona por preferência do navegador.
3. **Dado** um tópico com traduções disponíveis, **quando** a pessoa troca idioma, **então** mantém tópico e linha equivalentes.
4. **Dado** uma tradução ausente, **quando** a pessoa seleciona esse idioma, **então** recebe aviso explícito e opção de abrir a versão em português do mesmo tópico, sem conteúdo incorretamente apresentado como traduzido.
5. **Dado** uma preferência de tema Claro, Escuro ou Sistema, **quando** a pessoa navega, retorna ou muda a preferência do sistema no modo Sistema, **então** o tema efetivo e a preferência escolhida seguem as regras do modo selecionado.

### História 3 — Manter o manual ligado ao produto (Prioridade: P2)

Como mantenedor do Dokpod, quero validar o portal e gerar um artefato estático por CI, para revisar a documentação sem publicar conteúdo ou expor credenciais.

**Por que esta prioridade**: A publicação deve ser reproduzível e independente da aplicação; falhas do manual não podem liberar ou publicar o produto automaticamente.

**Teste independente**: Executar os comandos de validação documentados em container, confirmar build dos três idiomas e verificar que o artefato contém somente a saída pública do portal.

**Cenários de aceitação**:

1. **Dado** um pull request com mudança no portal, **quando** o CI executa, **então** valida o build e testes sem credenciais de produção e não inicia deploy.
2. **Dado** um link quebrado, uma tradução obrigatória ausente ou conteúdo de fixture com erro, **quando** o CI roda, **então** a validação falha com diagnóstico acionável.
3. **Dado** um build bem-sucedido, **quando** o artefato é gerado, **então** inclui somente saída pública aprovada e metadados de integridade, sem dependências de runtime Node.

## Casos de borda

- O armazenamento local pode estar bloqueado ou conter locale/tema inválido; a leitura e navegação devem continuar disponíveis.
- O navegador pode não informar idioma suportado; o fallback é português.
- O sistema operacional pode mudar de preferência enquanto o portal está aberto; somente o modo Sistema acompanha essa mudança.
- A tradução pode não existir para um tópico, linha ou idioma; o portal não deve inventar fallback nem alterar a linha.
- Uma versão de preview pode não ter linha estável aprovada; ela não deve ser apresentada como versão publicada.
- A construção de um portal privado pode incluir somente arquivos explicitamente aprovados; fontes, fixtures, resultados de teste e materiais internos não entram no artefato.
- CSP ou navegação sem JavaScript podem impedir preferências interativas; o conteúdo permanece legível e oferece links explícitos de idioma.

## Requisitos

### Requisitos funcionais

- **FR-001**: O portal MUST oferecer manuais separados por linhas `X.Y` aprovadas, distinguindo conteúdo entre as linhas e mantendo tópicos equivalentes identificáveis.
- **FR-002**: O portal MUST preservar a linha e o tópico ao navegar diretamente, atualizar a página ou trocar de idioma quando a tradução correspondente existir.
- **FR-003**: O portal MUST oferecer conteúdo e navegação em português, inglês e espanhol, sem declarar uma tradução disponível antes de sua revisão.
- **FR-004**: Na entrada sem idioma explícito, o portal MUST usar a preferência de idioma do navegador entre português, inglês e espanhol, com fallback para português.
- **FR-005**: O portal MUST respeitar idioma explícito do URL; autodetecção não deve redirecionar links profundos.
- **FR-006**: O portal MUST informar quando a tradução do tópico não existe e permitir ao leitor escolher explicitamente o tópico em português da mesma linha.
- **FR-007**: O portal MUST permitir escolher Claro, Escuro e Sistema, persistir a preferência localmente quando possível e acompanhar mudanças do sistema somente no modo Sistema.
- **FR-008**: Preferências de idioma ou tema indisponíveis ou inválidas MUST NOT impedir leitura, navegação ou acesso às opções explícitas.
- **FR-009**: A apresentação do portal MUST seguir a identidade visual aprovada da Altivy, com fontes e tokens locais aprovados, nos temas claro e escuro, sem recursos visuais carregados de terceiros.
- **FR-010**: O portal MUST permitir localizar conteúdo por busca contextual ao idioma e à linha escolhidos, sem enviar consultas para serviço externo.
- **FR-011**: Alterações do portal MUST ser validadas por CI sem secrets de produção e sem publicar ou implantar conteúdo automaticamente.
- **FR-012**: O artefato de CI MUST conter apenas saída estática e metadados de integridade aprovados; não deve depender de runtime Node para servir o site.
- **FR-013**: O portal MUST identificar explicitamente conteúdo de demonstração/preview e não apresentar fixture como release real ou alegação de suporte.

## Entidades principais

- **Linha de documentação**: grupo major.minor que identifica o manual; contém documentos, disponibilidade de locales e estado editorial.
- **Tópico**: unidade navegável com identificador estável, linha, conteúdo, metadados e traduções eventualmente disponíveis.
- **Preferência de leitura**: idioma e modo de tema escolhidos ou inferidos para uma pessoa leitora, sem conta ou transmissão a analytics.
- **Artefato de portal**: saída estática validada, associada a uma revisão editorial e a informações de integridade.

## Critérios de sucesso

### Resultados mensuráveis

- **SC-001**: Em todas as duas linhas de teste, um leitor consegue distinguir o conteúdo da versão e retorna ao mesmo tópico após atualizar a página, sem cair em outra linha.
- **SC-002**: Para `pt-*`, `en-*` e `es-*`, a entrada seleciona o locale correspondente; para idioma não suportado, seleciona português; URLs explícitas nunca mudam por autodetecção.
- **SC-003**: Cada um dos três modos de tema passa testes de persistência e precedência; alterações do sistema afetam o portal apenas no modo Sistema.
- **SC-004**: 100% dos tópicos e traduções marcados como obrigatórios no conjunto de teste podem ser abertos na linha e idioma corretos; ausência é comunicada sem fallback silencioso.
- **SC-005**: O build do portal valida PT/EN/ES e falha para link interno quebrado, tradução obrigatória ausente ou fixture inválida.
- **SC-006**: Pull requests geram validação e artefato revisável sem secrets de produção, deploy, DNS, TLS ou publicação.
- **SC-007**: O artefato contém somente arquivos estáticos e metadados públicos aprovados; a execução da saída não requer runtime Node.

## Premissas

- O escopo desta feature é o piloto técnico Dokpod. Não cria os portais de nodra, clavora, naoatenda ou garagemotor; esses produtos terão inventário e aprovação próprios.
- Docusaurus é a plataforma escolhida no plano aprovado; versão exata e dependências ficam para pesquisa/planejamento e precisam de licença permissiva, manutenção e compatibilidade verificadas.
- O portal inicial usa fixtures para provar versão, idioma, tema e busca; não sinaliza release estável nem substitui a documentação interna existente.
- Domínio preferido é `docs.dokpod.altivy.it`, mas DNS/TLS, CD de produção e publicação estão fora desta feature.
- A identidade Altivy será consumida por snapshot aprovado de tokens, fonte e logos com checksums; não dependerá de diretório externo ao repositório durante build/runtime.
- A pesquisa automática de idioma ocorre apenas na entrada neutra; links explícitos permanecem determinísticos.
- O CI executará somente build/teste/empacotamento; deploy posterior requer feature/tarefas e autorização próprias.

## Escopo não incluído

- Publicar o portal em domínio público, alterar DNS ou certificados, vhost da borda, infraestrutura de produção ou ambiente GitHub protegido.
- Criar ou alterar funcionalidades do produto Dokpod, contratos API, release, matriz de suporte ou decisão GO/NO-GO.
- Importar integralmente `docs/` do repositório; fixtures devem ser sintéticas e o conteúdo editorial real será uma etapa posterior.
- Criar portais de outros produtos, um repositório compartilhado de tema, login, analytics, backend ou serviço de busca externo.
- Publicar preços, clientes, métricas, canais ou alegações de produto não aprovadas.
