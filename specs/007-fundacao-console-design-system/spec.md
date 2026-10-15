# Especificação da Feature: Fundação do Console Operacional e Design System Próprio

**Branch da feature**: `007-console-operacional`

**Criada em**: 2026-10-12

**Estado**: `implemented` - aprovada em 2026-10-13 (ADR 2026-0005 `accepted`; ver [specs/README.md](../README.md)) e implementada; T029 executada na stack real em 2026-10-14 (Linux, Podman).

**Entrada**: Mockups do canvas "Gestão de Containers" em [docs/design/app](../../docs/design/app/project/canvas.json) (`Main`, `Containers`, `Detalhe`, `NovoContainer`, `Campo`, `Selecao`, `Marcador`) e design system Altivy em [ds/altivy](../../docs/design/app/project/ds/altivy/README.md). Decisão do solicitante em 2026-10-12: implementar componentes Angular próprios, fiéis ao mockup, substituindo a regra atual de uso exclusivo de PO UI.

**Conjunto**: primeira de oito specs do console operacional (007-014). Índice e ordem em [specs/README.md](../README.md#console-operacional-007-014).

## Contexto

O frontend atual (`frontend/web`) tem duas telas (catálogo de ambientes e lista de containers por ambiente) com estilo próprio "IBM Plex / verde", sem PO UI instalado, apesar de as instruções exigirem PO UI. Os mockups definem um console escuro com menu lateral, cabeçalho com breadcrumbs, atalhos de ambiente, KPIs, gráficos, grid de dados, abas, drawer de formulário e componentes de formulário próprios. Esta spec cria a fundação visual e de navegação sobre a qual as specs 008-014 entregam as funcionalidades.

## Cenários do Usuário e Testes (obrigatório)

### Jornada do Usuário 1 - Navegar pelo console com a moldura do mockup (Prioridade: P1)

Como operador autenticado, quero uma moldura única com menu lateral por seções (Operação, Infraestrutura, Governança), cabeçalho com título, breadcrumbs, atalhos de ambiente (Produção, Homologação, Desenvolvimento), notificações e identificação do usuário, para alternar entre as áreas do console sem perder o contexto.

**Valor desta prioridade**: Todas as telas das specs 008-014 dependem da moldura; sem ela nenhuma tela do mockup pode ser entregue.

**Teste independente**: Autenticar pela borda nginx, navegar por todos os itens do menu com mouse e teclado, alternar o atalho de ambiente e verificar rota, item ativo, breadcrumbs e estado preservado após recarregar a página.

**Cenários de aceite**:

1. **Dado** usuário autenticado, **Quando** abre o console, **Então** vê o menu com as seções e itens do mockup, o item da rota atual marcado como ativo, breadcrumbs coerentes com a rota e o nome do usuário no cabeçalho.
2. **Dado** item de menu cuja funcionalidade ainda não foi entregue (Pods, Volumes, Redes, Registros, Alertas, Auditoria, Configurações), **Quando** selecionado, **Então** abre uma página "Em breve" explicando a ausência, sem erro e sem link quebrado.
3. **Dado** atalho de ambiente selecionado, **Quando** o usuário navega entre telas ou recarrega, **Então** o estágio selecionado permanece na URL (`?stage=`). A aplicação do filtro pelas telas é entregue a partir da spec 008, que cria o atributo de estágio; até lá, o atalho aparece desabilitado com explicação.
4. **Dado** viewport abaixo de 1100px (incluindo móvel até 320px), **Quando** o usuário abre o console, **Então** o menu lateral vira painel recolhível acionado por botão com nome acessível, sem rolagem horizontal da página.
5. **Dado** sessão expirada ou Keycloak indisponível, **Quando** qualquer tela carrega, **Então** a moldura mostra o estado correspondente e oferece novo login sem fallback de autorização.

### Jornada do Usuário 2 - Biblioteca de componentes fiel ao mockup (Prioridade: P1)

Como desenvolvedor do Dokpod, quero componentes Angular próprios equivalentes aos usados nos mockups (botão, chip de status, círculo de status, badge de engine, KPI, card, barra de progresso, lista chave-valor, gráfico de linha e rosca, grid de dados, abas, campo de busca, campo, seleção, marcador, drawer, ícone, estados vazios e toast), para montar as telas sem reimplementar comportamento em cada feature.

**Valor desta prioridade**: Garante consistência visual, acessibilidade e teclado em um único lugar.

**Teste independente**: Página de catálogo de componentes acessível somente em build de desenvolvimento, com cada componente em todos os estados; testes unitários de teclado/ARIA e screenshots desktop/mobile comparados com os boards do canvas.

**Cenários de aceite**:

1. **Dado** o componente Seleção, **Quando** operado só por teclado (setas, Enter, Espaço, Esc, Home, End), **Então** comporta-se como o board `Selecao.dc.html`, com `listbox`/`option` e `aria-selected` corretos.
2. **Dado** o componente Campo com `secret`, **Quando** renderizado, **Então** o valor fica mascarado e só é revelado por ação explícita e anunciada.
3. **Dado** o grid de dados, **Quando** recebe linhas, colunas, tons de status, ações por linha, seleção, ordenação, densidade e busca, **Então** usa semântica de `table` nativa e oferece cabeçalho fixo, coluna travada, seleção múltipla e anúncio do número de selecionados; por teclado, Tab percorre checkboxes, cabeçalhos ordenáveis e ações de linha na ordem visual, Espaço marca/desmarca, Enter/Espaço no cabeçalho alterna a ordenação (refletida em `aria-sort`) e o menu de ações abre com Enter e fecha com Esc devolvendo o foco. Navegação célula a célula por setas (`role="grid"`) fica fora da v1.
4. **Dado** status apresentado em qualquer componente, **Quando** exibido, **Então** sempre combina cor com palavra ou ícone (nunca só cor).
5. **Dado** preferência `prefers-reduced-motion` ou `prefers-reduced-transparency`, **Quando** ativa, **Então** animações são suprimidas e superfícies de vidro passam a preenchimento opaco equivalente.

### Jornada do Usuário 3 - Governança da troca de biblioteca de UI (Prioridade: P1)

Como responsável pelo projeto, quero que a substituição da regra "somente PO UI" por um design system próprio seja registrada em ADR `proposed`, com instruções e documentação atualizadas, para que agentes e revisores não recebam regras concorrentes.

**Valor desta prioridade**: A constituição exige decisão humana para conflitos e proíbe aprovação inferida.

**Teste independente**: Revisão documental: nenhuma instrução em `.github/` ou `docs/` continua exigindo PO UI ou proibindo os padrões aprovados no ADR; o ADR identifica revisor humano.

**Cenários de aceite**:

1. **Dado** o ADR aprovado, **Quando** um agente lê as instruções de frontend, **Então** encontra uma única regra de design system (a do Dokpod baseada em Altivy) e a lista de dependências permitidas.
2. **Dado** ADR ainda `proposed`, **Quando** alguém tenta iniciar as tarefas de implementação desta spec, **Então** o `tasks.md` bloqueia a fase de implementação até a aprovação registrada.

### Jornada do Usuário 4 - Substituir as telas atuais sem regressão funcional (Prioridade: P2)

Como operador, quero que login, catálogo/cadastro de ambientes e inventário existentes continuem funcionando dentro da nova moldura até serem substituídos pelas telas das specs 008 e 010.

**Valor desta prioridade**: Evita janela em que o console perde funcionalidades já entregues.

**Teste independente**: Executar os E2E atuais de cadastro de ambiente, inventário e lifecycle pela nova moldura.

**Cenários de aceite**:

1. **Dado** usuário não autenticado, **Quando** acessa o console, **Então** vê a tela de entrada no visual novo com a ação "Entrar com Keycloak".
2. **Dado** telas legadas ainda não substituídas, **Quando** acessadas, **Então** renderizam dentro da moldura nova sem quebra de layout, e os testes E2E existentes continuam passando.

## Casos de Borda

- Nome de usuário ausente ou muito longo; iniciais calculadas de forma segura; nenhum dado de token exibido.
- Menu com contador (Alertas) sem permissão para o recurso: o contador não aparece.
- Rotas desconhecidas: página 404 dentro da moldura com link para a Visão geral.
- Fontes não carregadas: fallback de sistema sem deslocamento de layout relevante.
- Navegador sem suporte a `backdrop-filter`: superfícies opacas equivalentes.
- Conteúdo dinâmico (nomes de container, labels, imagens) sempre tratado como texto não confiável e escapado.
- Zoom de 200% e largura de 320px sem perda de conteúdo ou função (WCAG 1.4.10).

## Requisitos (obrigatório)

### Requisitos Funcionais

- **FR-001**: O console DEVE apresentar uma moldura única com menu lateral em três seções e os itens do mockup: Operação (Visão geral, Containers, Pods, Imagens, Volumes, Redes), Infraestrutura (Hosts, Registros) e Governança (Alertas, Auditoria, Configurações).
- **FR-002**: O cabeçalho DEVE exibir título da tela, breadcrumbs, atalhos de estágio (Produção, Homologação, Desenvolvimento), indicador de notificações e usuário (nome, grupo de exibição opcional e iniciais).
- **FR-003**: O estágio selecionado DEVE ser parte da URL (parâmetro de consulta) e preservado entre navegações e recargas; ausência de estágio significa "todos os estágios autorizados".
- **FR-004**: Itens de menu sem funcionalidade entregue DEVEM abrir página "Em breve" dentro da moldura; itens entregues pelas specs 008-014 substituem a página "Em breve" ao serem concluídos.
- **FR-005**: Itens de menu e contadores DEVEM respeitar os scopes retornados pela API; o frontend nunca decide autorização, apenas oculta o que não foi concedido.
- **FR-006**: O design system DEVE fornecer os componentes listados na Jornada 2 com API documentada, estados (padrão, hover, foco, desabilitado, carregando, erro) e testes de teclado/ARIA.
- **FR-007**: Todos os tokens visuais (cor, tipografia, espaço, raio, sombra, desfoque, gráfico) DEVEM vir de uma única fonte derivada de `docs/design/app/project/ds/altivy/tokens.json`; componentes não usam valores literais fora dos tokens.
- **FR-008**: O console DEVE ser escuro (dark-first) na v1; o tema claro existente é removido.
- **FR-009**: O console DEVE atender WCAG 2.2 AA: foco visível em todo elemento interativo, contraste mínimo conforme tabela do Altivy, status com palavra/ícone, região viva para resultados e navegação completa por teclado.
- **FR-010**: O console DEVE ser responsivo de 320px a 1920px, com menu recolhível abaixo de 1100px e sem rolagem horizontal da página; larguras de verificação: 1920, 1440, 1100, 390 e 320px.
- **FR-011**: Fontes e ícones DEVEM ser servidos pela própria aplicação (sem CDN externo em runtime), com licenças permissivas verificadas e compatíveis com AGPL-3.0-only.
- **FR-012**: Números e datas DEVEM seguir o formato pt-BR (`1.234,5`, `10/10/2026 15:12`) com algarismos tabulares.
- **FR-013**: A tela de entrada (não autenticado), estados de sessão (carregando, erro, expirada) e a página 404 DEVEM usar o novo visual.
- **FR-014**: A substituição da regra "somente PO UI" DEVE ser registrada em ADR `proposed` com revisor humano identificado, e as instruções/documentos afetados DEVEM ser atualizados no mesmo conjunto de mudanças, sem regra concorrente remanescente.
- **FR-015**: Telas legadas (catálogo de ambientes e inventário por ambiente) DEVEM continuar funcionais dentro da nova moldura até serem substituídas pelas specs 008 e 010.

### Entidades Principais

- **Item de navegação**: identificador, rótulo, ícone, seção, rota, scope exigido opcional, contador opcional com rótulo acessível.
- **Estágio de ambiente**: Produção, Homologação ou Desenvolvimento; definido na spec 008 e consumido aqui como filtro global.
- **Token visual**: nome semântico e valor, organizado por categoria (cor, tipo, espaço, raio, sombra, desfoque, gráfico).

## Critérios de Sucesso (obrigatório)

### Resultados Mensuráveis

- **SC-001**: 100% dos itens de menu do mockup navegáveis por teclado, com item ativo e breadcrumbs corretos em todas as rotas.
- **SC-002**: Para cada board do canvas coberto por esta spec (moldura, `Campo`, `Selecao`, `Marcador`), capturas a 1440px lado a lado com o board, revisadas e aprovadas por um humano, que registra na revisão: mesma estrutura de layout, tokens de cor/tipografia/raio iguais aos de `tokens.json` (verificados pelos estilos computados) e espaçamentos com no máximo 4px de diferença.
- **SC-003**: Zero violações de severidade séria ou crítica (`serious`/`critical`) apontadas pela ferramenta de acessibilidade aprovada no ADR 2026-0005, executada no E2E pelas páginas da moldura, entrada, 404, "Em breve" e catálogo de componentes.
- **SC-004**: E2E existentes (cadastro de ambiente, inventário, lifecycle) passam sem alteração de comportamento após a troca de moldura.
- **SC-005**: Nenhuma instrução ou documento do repositório continua exigindo PO UI após a aprovação do ADR.
- **SC-006**: Primeira renderização útil da moldura em até 2 segundos na stack E2E local, com bundle inicial de produção dentro do orçamento absoluto de 420 kB brutos (alerta) e 500 kB (erro). O limite original de 15% sobre a linha de base foi substituído em 2026-10-13 após a medição da T031 (ver [plan.md](plan.md), "Linha de Base").

## Premissas

- O design system Altivy pertence ao mesmo responsável do Dokpod; tokens e diretrizes podem ser reproduzidos no repositório. Componentes são reimplementados em Angular, não copiados do bundle React do canvas.
- Outfit e JetBrains Mono (SIL OFL 1.1) e ícones de traço no estilo Lucide (ISC) são candidatos; a verificação de licença e a fixação de versão fazem parte do plano.
- O glassmorphism e os raios grandes do Altivy contrariam hoje `docs/frontend.md` e `frontend-angular.instructions.md`; a aprovação do ADR remove essa contradição. Sem aprovação, a spec não avança para implementação.
- Grupo de exibição do usuário vem de um claim opcional configurado no Keycloak; se ausente, apenas o nome é exibido.
- Tema claro, editor de layout e paleta de comandos do Altivy ficam fora do escopo.
