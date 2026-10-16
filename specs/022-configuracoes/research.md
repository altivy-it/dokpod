# Pesquisa: Configurações

## D-001 - Versão do produto

- **Situação**: não há versão definida (o `Directory.Build.props` não declara `Version`; o `package.json` está em `0.0.0`; as imagens usam `IMAGE_VERSION=0.1.0-dev`).
- **Decisão**: versão única do produto `0.1.0` em `backend/Directory.Build.props` (API, BFF e agente pela versão do assembly) e no `package.json` do console; um teste de arquitetura garante que as duas coincidem. O BFF expõe a própria versão em `GET /bff/version`; o console usa a do `package.json` (só a feature Configurações importa o valor).

## D-002 - Acesso

- **Decisão**: `GET /api/v1/settings/access` decide `environment:manage` nos hosts legíveis e habilitados (em paralelo) e libera se algum permitir; as vistas Instalação e Retenções e limites respondem 403 aos demais. O console usa a resposta para mostrar o item de menu, como a Auditoria (spec 017).

## D-003 - Instalação

- Versão do plano de controle (assembly), versão do protocolo do agente (`1`), última migration aplicada e migrations pendentes (EF), saúde pelos health checks `ready` já registrados (API, Keycloak, PostgreSQL) com duração e hora, e distribuição dos agentes dos hosts legíveis por versão de agente e por engine/versão, com conectados e "atualização necessária" (spec 008).

## D-004 - Retenções e limites

- Valores efetivos de `Commands:Retention`, `Metrics:HostRetention`, `Metrics:LatestExpiry`, `EventRetention:Period`, `Alerts:ResolvedRetention`, `Terminal:*` (ociosidade, duração, sessões por usuário, por container e por agente, saída por segundo, estágios com justificativa, MFA recente) e constantes (lote 50, exportação 10.000, período da auditoria 90 dias, auditoria sem remoção automática). Origem `installation` quando a chave existe na configuração, senão `default`. A lista é fechada: nenhuma chave de configuração é exposta por nome arbitrário.

## D-005 - Política de justificativa com `canEdit`

- `GET /api/v1/action-justification-policies` passa a informar `canEdit` por estágio (mesma regra de edição da spec 016), para a vista Políticas mostrar somente leitura sem tentar salvar.
