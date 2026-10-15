# Plano de Implementação: Registros

**Branch**: `020-registros` | **Data**: 2026-10-15 | **Spec**: [spec.md](spec.md)

**Estado**: `approved` - plano derivado da spec aprovada em 2026-10-15.

## Resumo

O agente publica, com capability nova, a configuração de registros do engine (espelhos, inseguros, de busca e bloqueados) e os nomes dos registros para os quais tem credencial; o plano de controle guarda essa configuração por host e a consolida, por registro, com os registros de origem das imagens já projetadas (spec 012). A política de registros permitidos por estágio passa da configuração da API para o banco, editável no console com auditoria, e vale imediatamente para download e criação de containers.

## Contexto Técnico

**Linguagem/Versão**: .NET 10, protobuf v1 (aditivo), EF Core com scripts SQL, OpenAPI 3.1, Angular 22.

**Dependências Principais**: existentes; nenhuma nova.

**Armazenamento**: migration `202610150005_RegistryInventory`: `inventory_registry_projections` e `inventory_registries` (configuração por host e registro) e `registry_policies` (lista por estágio, autor e horário).

**Testes**: domínio (gramática de registro, auditoria), agente (leitura de `/info` Docker e `/libpod/info` Podman, nomes do arquivo de credenciais sem valores), PostgreSQL real (projeção, política, carga de 200 registros), aplicação (consolidação, política, autorização), Vitest, E2E simulada e stack real (Podman).

**Plataforma-alvo**: agente (Docker e Podman) e plano de controle; contrato aditivo (N/N-1).

**Metas de desempenho**: política nova vale em até 5 s (SC-003); primeira página em até 2 s no p95 com 200 registros em 56 hosts (SC-004).

**Restrições**: nenhum valor de credencial sai do host; nada é alterado no host; mudar a política não remove imagens.

## Verificação da Constituição

| Princípio | Situação |
| --- | --- |
| I. Documentação em pt-BR | OK. |
| II. Engine local como fonte | OK: configuração vem do engine e do arquivo do host pelo agente; o console não altera o host. |
| III. Identidade externa e menor privilégio | OK: leitura com `environment:read`; política com `environment:manage` em todos os hosts do estágio (regra da spec 016). |
| IV. Operações duráveis | OK: não há comando novo; a política é gravada e auditada em seguida, por host do estágio (mesmo modelo da política de justificativa da spec 016). |
| V. Modularidade e compatibilidade | OK: mensagem e capability novas; agente N-1 fica "configuração indisponível"; sem política no banco, vale a configuração da API. |
| VI. Qualidade verificável | OK: testes por camada, canário de credencial, E2E simulada e stack real Podman. Docker real `NOT RUN` nesta máquina. |

## Rastreabilidade

| Requisito | Desenho |
| --- | --- |
| FR-001, FR-009 | Capability `REGISTRY_INVENTORY`, `AgentMessage.registry_inventory` (D-001, D-002). |
| FR-002 a FR-004 | `IRegistryInventoryStore` + consolidação com as tags e digests das imagens projetadas (D-003). |
| FR-005 a FR-007 | `registry_policies`, `ImagePullPolicy` com sobreposição recarregada, `RegistryPolicyService` (D-004). |
| FR-008 | Rota `/registros`. |
