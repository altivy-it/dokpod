# Frontend

## Objetivo

O frontend é um console operacional Angular 22 para leitura rápida, comparação entre ambientes e execução deliberada de ações com impacto.

## Arquitetura

- componentes standalone e lazy loading por feature;
- TypeScript estrito;
- Signals para estado local e derivado;
- RxJS para HTTP, SignalR, cancelamento e fluxos assíncronos;
- cliente HTTP gerado de `contracts/openapi`;
- sessão mediada pelo BFF; o Angular nunca lê access token ou refresh token;
- componentes de apresentação sem acesso direto à rede;
- bibliotecas compartilhadas sem dependência interna da aplicação.

Estrutura inicial:

```text
frontend/web/src/app/
  core/
  shell/
  data-access/
  design-system/
  features/environments/
  features/containers/
  features/audit/
frontend/libs/
frontend/tests/
```

Features integram por rotas, contratos públicos e fachadas pequenas; não importam detalhes internos umas das outras.

### Lista unificada de containers e ações em lote (spec 010)

- `/containers` lista containers de todos os hosts autorizados (`GET /api/v1/containers`) com cursor, busca no servidor, ordenação por nome/situação/host/criação e resumo das seis situações; a rota legada `/environments/:environmentId/containers` redireciona para a lista filtrada pelo host;
- filtros (estágio global, host, engine, situação e busca) ficam na URL; os ativos aparecem como chips removíveis acima do grid ("Engine: …", "Ambiente: …") e são escolhidos pelo botão "Filtros" do grid com facetas fornecidas pela página;
- o grid oferece Agrupar (linhas carregadas), densidade, seletor de colunas lembrado no navegador e exportação CSV da página carregada com neutralização de fórmulas;
- ações por linha (logs, terminal, inspecionar, iniciar, parar, reiniciar, pausar, retomar, remover) seguem `allowedActions` (scope × capability do agente × situação × conexão);
- lotes: até 50 containers, um comando individual por container com concorrência 5, chaves de idempotência e IDs em `sessionStorage` (escopo da aba), acompanhamento consultando o servidor e retomada após recarga sem reenvio automático;
- a confirmação lista afetados e ignorados com motivo, informa que volumes não são removidos e, para remover mais de um container em Produção, exige digitar a quantidade;
- toda mutação usa `X-Dokpod-Antiforgery`, chave de idempotência UUID v4 (com fallback a `crypto.getRandomValues` fora de contexto seguro) e a revisão observada do container.

### Alertas e ações justificadas (spec 016)

- Governança → Alertas (`/alertas`) tem as vistas Abertos, Histórico (90 dias), Regras e Silenciamentos; vista, severidade, situação, host, regra, período e busca ficam na URL (`vista`, `severidade`, `estado`, `environmentId`, `regra`, `desde`, `ate`, `q`), e `alerta=<id>` abre o detalhe com a linha do tempo;
- reconhecer e resolver pedem comentário; silenciar pede janela e justificativa (10 a 500 caracteres); ações só aparecem com `canManage` devolvido pela API, nunca deduzido da lista de scopes do registro do ambiente;
- o contador do menu "Alertas" e o sino vêm de `AlertSummaryService` (`GET /api/v1/alerts/summary` do estágio selecionado), recarregado pela invalidação `alerts` do hub e, sem tempo real, a cada minuto;
- a política de justificativa por estágio fica na vista Regras; o diálogo de parar, pausar e remover (lista, detalhe e lote) mostra o campo obrigatório ou opcional conforme `JustificationPolicyService`, e a API continua sendo a autoridade (422 `justification_required`);
- containers parados ou pausados com justificativa vigente aparecem como "Parado intencionalmente"/"Pausado intencionalmente", com autor, horário e motivo no detalhe.

### Auditoria (spec 017)

- Governança → Auditoria (`/auditoria`) lista a trilha dos hosts com `audit:read`, sempre por período (`periodo` = 24h, 7d, 30d, 90d ou `personalizado` com `desde`/`ate`); categoria, resultado, ator, host e alvo ficam na URL (`categoria`, `resultado`, `ator`, `environmentId`, `q`), e `evento`/`em` abrem o detalhe; a busca (`q`) é full-text em português, sem diferenciar acentos, e aceita aspas, `or` e `-palavra`;
- o detalhe mostra os campos como gravados, o alvo navegável (container ou alerta) e os eventos relacionados pela correlação ou comando;
- o item de menu depende de `GET /api/v1/audit/summary` (decisão do Keycloak), não da lista de scopes do registro do ambiente; o detalhe do container oferece "Ver auditoria";
- "Exportar CSV" baixa todo o filtro (até 10.000 eventos) e a exportação fica registrada na própria trilha;
- a consulta fica em `features/audit/audit-api.service.ts`, fora do bundle inicial.

### Volumes (spec 018)

- Operação → Volumes (`/volumes`) lista os volumes dos hosts autorizados com os containers que os montam (inclusive parados); sem uso, tipo (`anonimo=1`/`0`), engine, host, busca e ordenação ficam na URL (`semUso`, `anonimo`, `engine`, `environmentId`, `q`, `sort`), e `volumeHost` + `volume` abrem o detalhe (link usado pelo detalhe do container);
- o resumo mostra total, sem uso e os hosts com inventário indisponível; a invalidação `volumes` do hub recarrega a lista;
- o detalhe mostra labels, montagens com link para o container e o tamanho consultado ao agente só quando o operador pede;
- remover (linha, detalhe ou seleção, até 50) abre a confirmação reforçada: aviso de apagamento definitivo, afetados e ignorados, digitar a quantidade (lote) ou o nome (um volume em Produção) e justificativa conforme `JustificationPolicyService`; o lote é acompanhado por comando e retomado após recarregar (`sessionStorage`) sem reenvio automático;
- as chamadas ficam em `features/volumes/volumes-api.service.ts`, fora do bundle inicial.

### Redes (spec 019)

- Operação → Redes (`/redes`) lista as redes dos hosts autorizados com os containers conectados (inclusive parados) e as sub-redes; sem uso, do sistema (`sistema=1`/`0`), driver, engine, host, busca (nome, identificador, sub-rede, label ou container) e ordenação ficam na URL (`semUso`, `sistema`, `driver`, `engine`, `environmentId`, `q`, `sort`), e `redeHost` + `rede` (identificador ou nome) abrem o detalhe, link usado pelo detalhe do container;
- redes do sistema aparecem como "Do sistema" e não contam como "sem uso"; o resumo mostra os hosts com inventário indisponível; a invalidação `networks` do hub recarrega a lista;
- remover (linha, detalhe ou seleção, até 50) lista afetadas e ignoradas (sistema, cluster, em uso, sem permissão, host desconectado), pede a quantidade só em lote com rede de Produção e justificativa conforme `JustificationPolicyService`; o lote é acompanhado e retomado como o de volumes;
- as chamadas ficam em `features/networks/networks-api.service.ts`, fora do bundle inicial (419,51 kB, aviso em 420 kB).

### Registros (spec 020)

- Infraestrutura → Registros (`/registros`) tem as vistas Registros e Política por estágio (`vista=politica`); origem, inseguros, fora da política, host e busca ficam na URL (`origem`, `inseguros`, `foraDaPolitica`, `environmentId`, `q`, `sort`) e `registro` abre o detalhe por host, com link para Imagens filtrada;
- credenciais aparecem só como "sim/não"; a situação mostra o caso mais grave (bloqueado, inseguro, fora da política);
- a política mostra a lista efetiva de cada estágio e se veio da configuração ou do console; a edição valida host[:porta], mostra quantas imagens ficam fora e só aparece com administração de todos os hosts do estágio;
- as chamadas ficam em `features/registries/registries-api.service.ts`; as de gestão de alertas passaram para `features/alerts/alerts-api.service.ts`, o que manteve o bundle inicial em 418,91 kB.

## Experiência operacional

- densidade informacional adequada a tarefas repetidas;
- estado de conexão e idade do inventário sempre visíveis;
- ações destrutivas distinguem container, volumes e efeitos;
- confirmação contextual para exclusão;
- loading, vazio, erro, offline, forbidden e estado desatualizado tratados;
- login, logout, sessão expirada e acesso negado seguem o fluxo do Keycloak pelo BFF;
- atualização em tempo real não move foco nem reordena listas inesperadamente;
- filtros e seleção são preservados durante refresh.

## Design system

O console segue o design system próprio derivado do Altivy (ADR 2026-0005), com os mockups de `docs/design/app` como fonte da verdade: tema escuro, vidro translúcido apenas em moldura, cards e overlays, conteúdo denso em painel opaco, tipografia Outfit (JetBrains Mono para dados técnicos) e `teal-light` como único acento. Tokens, fontes e utilitários globais ficam em `frontend/web/src/styles/` (`tokens.css`, `fonts.css`, `app-text.css`, `altivy-kit.css`) e componentes `dok-*` em `frontend/web/src/app/design-system/`; `frontend/libs/design-system/assets/` guarda apenas ativos de marca e ícones de navegador.

Ícones vêm de `dok-icon` (geometria do kit Altivy) e sempre possuem nome acessível ou tooltip quando o significado não é óbvio. Todo status leva palavra ou ícone além da cor.

### Cards de KPI navegáveis

Cards de indicador que representam um recorte de lista são links para essa lista, já filtrada ou ordenada, preservando o estágio da URL (`queryParamsHandling="merge"`); na Visão geral, os seis cards levam à tela Containers (situação ou ordenação por consumo) ou à tela Imagens (spec 015). O nome acessível é o texto visível do card seguido do destino em `.sr-only` (WCAG 2.5.3, "label in name"), nunca um `aria-label` que substitua o texto visível. Durante o carregamento o card não é link; com valor "indisponível" continua navegável.

## Acessibilidade e desempenho

- WCAG 2.2 AA;
- navegação completa por teclado e foco visível;
- região viva para resultado de comandos sem anúncios excessivos;
- tabelas e listas extensas com virtualização quando medição justificar;
- respeito a preferência de movimento reduzido;
- layouts sem sobreposição em desktop e mobile;
- logs carregados sob demanda, truncados e nunca renderizados como HTML confiável.

O frontend não implementa tela de senha, cadastro, recuperação, MFA, grupos ou políticas. Essas jornadas pertencem ao Keycloak e ao provedor federado configurado.

## Testes

- Vitest para componentes, estado e data access;
- `lzocateli/angular-cli:22.1.0-node24.15.0-bookworm` para install, lint, typecheck, testes e build;
- `lzocateli/playwright-e2e:0.1.0` para testes black-box de cadastro de ambiente, inventário e ciclo de vida do container;
- testes de acessibilidade dos fluxos críticos;
- screenshots desktop/mobile para mudanças visuais;
- build de produção, lint, typecheck e ausência de erros inesperados no console.

Os testes unitários das features cobrem catálogo e cadastro de ambientes, sessão expirada, carregamento do inventário, negação `403`, submissão com antiforgery e idempotência e acompanhamento até estado terminal. A validação black-box autenticada permanece obrigatória na stack E2E.

Os assets de produção são servidos por uma imagem Dokpod derivada de `lzocateli/nginx:1.28.0-bookworm-v1`; Node.js e Angular CLI não fazem parte da imagem final. A matriz completa e a política de digest estão em [Distribuição e operação](distribuicao.md#imagens-base-e-toolchains).
