# Pesquisa: Registros

## D-001 - Configuração de registros do engine

- **Docker**: `/info` → `RegistryConfig.IndexConfigs` (`Name`, `Mirrors`, `Secure`, `Official`) e `RegistryConfig.Mirrors` (espelhos de `docker.io`, em URL). Registro com `Secure=false` é inseguro. `InsecureRegistryCIDRs` (faixas, inclusive o padrão `127.0.0.0/8`) não é um registro e fica fora da lista.
- **Podman**: a API compatível devolve `RegistryConfig` vazio (verificado no host de laboratório); a configuração de `registries.conf` vem de `/libpod/info` → `registries` (`search: [...]` e, por prefixo, `Location`, `Insecure`, `Blocked`, `Mirrors`). O agente tenta `/libpod/info` e usa `/info` quando não existe.
- Espelhos aparecem como registros próprios com a origem "espelho de <registro>".

## D-002 - Credenciais e publicação

- O agente lê só as chaves de `auths` do arquivo `DOKPOD_AGENT_REGISTRY_AUTH_FILE` (normalizando `https://index.docker.io/v1/` para `docker.io` e removendo esquema), nunca os valores.
- `AgentMessage.registry_inventory` (25) com revisão e a lista de registros do host (até 500); enviada no início da sessão e quando o conteúdo muda (releitura a cada 5 minutos ou em eventos de imagem). Capability `REGISTRY_INVENTORY` (20).

## D-003 - Consolidação

- Registros de origem das imagens: para cada imagem projetada, o registro da primeira tag válida (ou do primeiro digest), pela normalização da spec 012; consolidado em memória por requisição para os hosts autorizados (a projeção já é limitada por host).
- A projeção de configuração (`inventory_registries`) é substituída por mensagem e protegida por revisão.
- "Fora da política": o registro tem imagens ou configuração em algum host cujo estágio tem lista não vazia que não o contém.

## D-004 - Política no banco

- `registry_policies (stage, registries jsonb, updated_by, updated_at)`. Sem linha para o estágio, vale `Images:AllowedRegistriesByStage` (valor inicial).
- `ImagePullPolicy` passa a ter sobreposição carregada do banco: na alteração (na própria instância) e a cada 3 segundos (demais instâncias), o que cumpre a SC-003.
- Edição: `environment:manage` em todos os hosts habilitados e legíveis do estágio (regra da spec 016); auditoria `registry.policy_changed` (categoria governança) em cada host do estágio, com `stage`, `previousRegistries` e `registries`.
- Impacto: imagens e hosts do estágio cujos registros ficariam fora da nova lista.

## D-005 - Alocação no contrato do agente

`AgentMessage.payload` 25; `Capability` 20; `SnapshotScope` 5 (reenvio a pedido do servidor).
