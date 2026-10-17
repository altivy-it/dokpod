# Tarefas: Redes

**Entrada**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/networks-api.md), [quickstart.md](quickstart.md).

**Estado**: `done` - implementada e validada em 2026-10-15.

## Fase 1: Contrato e domínio

- [x] T001 Proto aditivo (D-007) e tabela de alocação em `specs/README.md`. Evidência: `agent.proto` só aditivo (payload 24, heartbeat 5, comando 10, parâmetro 10, escopo 4, capabilities 18/19); o servidor só usa inventário e remoção de redes quando o agente anuncia as capabilities e só as confirma com projeção disponível; tabela de alocação atualizada.
- [x] T002 Domínio: identificador e nome de rede, redes do sistema, comando `RemoveNetwork` (parâmetro e hash), scope `network:delete`, ação de auditoria `network.remove`; testes. Evidência: `NetworkDomainTests` (domínio: 144 aprovados); `networkId` incluído na allowlist de detalhes da auditoria.

## Fase 2: Agente

- [x] T003 Engine: `ListNetworksAsync`, conexões de rede na lista de containers, `RemoveNetworkAsync`; testes com HTTP simulado. Evidência: `NetworkAgentTests` (lista sem `Options`, conexões de container parado, 204/404/403/409/500 na remoção, alvo pelo identificador).
- [x] T004 Builder do snapshot de redes e publicação por capability (sessão, eventos `network`, mudanças de conexão, pedido de snapshot); testes. Evidência: `NetworkAgentTests` (conexões por identificador ou nome, rede do sistema, revisão só com mudança); eventos `network` incluídos no stream do engine e relendo o inventário; agente: 114 aprovados.
- [x] T005 Execução do `REMOVE_NETWORK` (conferência de uso, sistema, códigos); testes. Evidência: `NetworkAgentTests` (em uso por container parado, sistema, cluster e inexistente recusados; só a rede livre removida).

## Fase 3: Plano de controle

- [x] T006 Migration `202610150004_NetworkInventory` com Down; store PostgreSQL (projeção, revisão, busca, resumo, detalhe por nome); testes em PostgreSQL real. Evidência: Up/Down/Up no banco descartável (o histórico da 003 estava ausente nesse banco desde a validação da 018 e foi registrado); `NetworksIntegrationTests` (revisão obsoleta, sistema fora do "sem uso", busca por sub-rede sobreposta, container parado, label, prefixo do identificador, curinga escapado, detalhe por nome).
- [x] T007 Sessão gRPC: capabilities, páginas, revisão no heartbeat, invalidação `networks`; capability no despacho de comandos. Evidência: build e suíte da API (154 aprovados), inclusive transporte; o servidor também marca redes do sistema pelo nome.
- [x] T008 `NetworkRemovalService` e endpoints de lista, detalhe e remoção; OpenAPI e cliente; auditoria no console. Evidência: `NetworkRemovalServiceTests`; `CommandStore_PersistsRemoveNetworkAsHostCommandWithParameters`; contrato OpenAPI, cliente regenerado e rótulos da Auditoria (34 ações).

## Fase 4: Console

- [x] T009 Tela Operação → Redes (filtros na URL, resumo, lista, detalhe) e link no detalhe do container; Vitest. Evidência: `networks.spec.ts` (10 testes); Vitest: 160 aprovados; bundle inicial 419,51 kB.
- [x] T010 Remoção individual e em lote (confirmação, justificativa, acompanhamento e retomada); Vitest. Evidência: `networks.spec.ts` (lote, limite de 50, retomada sem reenvio, quantidade só em lote com Produção, sem aviso de perda de dados).

## Fase 5: Verificação

- [x] T011 E2E simulada (`test_019_*.py`) com axe. Evidência: `run-mock-e2e.sh`: suíte completa aprovada, com `test_019_networks.py` (5) e axe sem violações sérias/críticas; os testes de lote de 018/019 passaram a esperar o envio antes de recarregar.
- [x] T012 Carga (SC-005) em PostgreSQL real. Evidência: SC-005: p95 de 13 ms na primeira página com 2.016 redes em 56 hosts (meta: 2 s).
- [x] T013 Stack real Podman: quickstart, SC-001 a SC-004. Evidência: stack real Podman (migration, imagens novas conferidas por id, scope criado e concedido). SC-001: 15 de 15 redes e todas as contagens de conexões (inclusive containers parados) iguais ao engine; SC-002: 23 redes e a conexão do container parado na projeção em 4,5 s, e rede liberada 9,8 s após remover o container; SC-003: remoção aceita pela API logo após conectar um container → `network_in_use` no agente, rede intacta; recusas pela API com código (`network_in_use`, `network_system`); SC-004: 20 redes removidas em 3,2 s com 4 interações; auditoria `network.remove` com ator. SC-001 em Docker **NOT RUN** (sem Docker nesta máquina).
- [x] T014 Documentação (`docs/seguranca.md`, `docs/frontend.md`, `docs/configuracao-keycloak.md`) e evidências. Evidência: `docs/seguranca.md` (threat model), `docs/frontend.md`, `docs/configuracao-keycloak.md`; scripts de Keycloak com `network:delete`.

## Achados da validação na stack real

- A API compatível do Podman chama a rede padrão de `bridge` (mesmo identificador da `podman` do CLI); o Dokpod mostra o nome da API e a trata como "Do sistema" nos dois casos.
- Containers rootless em modo `pasta` não usam rede nomeada e não contam como conexão, como no engine.
- Com o gateway da identidade corrigido (spec 018), as recusas chegam ao console com o código do problema.

## Atualização da spec 023 (2026-10-17)

- **Docker real**: redes iguais ao `docker` CLI e remoção de rede vazia pelo console.
