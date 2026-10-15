# Tarefas: Registros

**Entrada**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/registries-api.md), [quickstart.md](quickstart.md).

**Estado**: `done` - implementada e validada em 2026-10-15.

## Fase 1: Contrato e domínio

- [x] T001 Proto aditivo (D-005) e tabela de alocação. Evidência: `agent.proto` só aditivo (payload 25, capability 20, `SnapshotScope` 5); sem projeção, o servidor não confirma a capability; tabela de alocação atualizada.
- [x] T002 Domínio: gramática e normalização de registro, auditoria `registry.policy_changed`; testes. Evidência: `RegistryNamesTests` (domínio: 156 aprovados); chaves `registries` e `previousRegistries` na allowlist da auditoria.

## Fase 2: Agente

- [x] T003 Engine: configuração de registros de `/info` (Docker) e `/libpod/info` (Podman); nomes de registros do arquivo de credenciais sem valores; testes. Evidência: `RegistryAgentTests` (`/info` do Docker, `/libpod/info` do Podman, espelhos, inseguro, bloqueado, busca, credenciais só pelos nomes, canário ausente da mensagem). A API compatível do Podman devolve `RegistryConfig` vazio (verificado no host de laboratório).
- [x] T004 Publicação por capability (sessão e mudança); testes. Evidência: `RegistryAgentTests` (revisão só com mudança); publicação na sessão, a cada 5 minutos e após downloads; agente: 117 aprovados.

## Fase 3: Plano de controle

- [x] T005 Migration `202610150005_RegistryInventory` com Down; store da configuração e da política; testes em PostgreSQL real. Evidência: Up/Down/Up no banco descartável; `RegistriesIntegrationTests` (revisão obsoleta, configuração indisponível, registros de origem das imagens, política por estágio).
- [x] T006 Sessão gRPC (capability, mensagem, invalidação `registries`); consolidação com as imagens; política com sobreposição recarregada no pull e na criação. Evidência: `RegistryCatalogTests` (origens por host, espelho fora da política, filtros, resumo, impacto, política salva sobrepondo a configuração no pull, auditoria com as duas listas, recusa sem permissão); aplicação: 154 aprovados.
- [x] T007 Endpoints de lista, detalhe, política, impacto e edição; OpenAPI e cliente; auditoria no console. Evidência: suíte da API (157 aprovados); contrato OpenAPI, cliente regenerado e rótulos da Auditoria (35 ações).

## Fase 4: Console

- [x] T008 Tela Infraestrutura → Registros (lista, filtros na URL, resumo, detalhe com link para Imagens); Vitest. Evidência: `registries.spec.ts`; Vitest: 165 aprovados; bundle inicial 418,91 kB após mover as chamadas de gestão de alertas para a feature.
- [x] T009 Vista de política por estágio (leitura, edição com impacto e confirmação); Vitest. Evidência: `registries.spec.ts` (normalização igual à do servidor, inválidos à parte, impacto com debounce, lista vazia = qualquer registro).

## Fase 5: Verificação

- [x] T010 E2E simulada (`test_020_*.py`) com axe. Evidência: `run-mock-e2e.sh`: 94 aprovados, com `test_020_registries.py` (5) e axe sem violações sérias/críticas; os exemplos de "Em breve" (spec 007 e shell) passaram a usar Configurações.
- [x] T011 Carga (SC-004) em PostgreSQL real. Evidência: SC-004: p95 de 29 ms com 200 registros em 56 hosts (2.240 imagens e 224 configurações).
- [x] T012 Stack real Podman: quickstart, SC-001 a SC-003. Evidência: stack real Podman (migration, imagens novas conferidas por id, arquivo de credenciais de teste montado só durante a validação). SC-001: imagens por registro iguais ao engine (`docker.io` 19, `localhost` 8, `mcr.microsoft.com` 2, `ghcr.io` 1, `quay.io` 1) e credencial de `registry.teste.local` como "sim"; SC-002: canário ausente de 38 respostas da API, do banco e dos logs; SC-003: download de `docker.io` recusado (`registry_not_allowed`) 0,1 s após salvar Homologação = `quay.io`, `quay.io` aceito, auditoria com as listas anterior (`*`) e nova; política de Homologação restaurada para "qualquer registro" (agora salva no console). SC-001 em Docker **NOT RUN** (sem Docker nesta máquina).
- [x] T013 Documentação e evidências. Evidência: `docs/seguranca.md` (threat model), `docs/frontend.md`, `docs/distribuicao.md` (configuração como valor inicial).

## Achados da implementação e da validação

- A API compatível do Podman não informa a configuração de `registries.conf` (`RegistryConfig` vazio); o agente usa `/libpod/info`.
- O bundle inicial passaria do aviso de 420 kB com a rota nova; as chamadas de gestão de alertas, usadas só pela tela Alertas, saíram do serviço global e o bundle voltou a 418,91 kB sem mudar o orçamento.
- O pipe da varredura gitleaks mascarava o código de saída; a varredura passou a falhar de fato com achado (um nome de variável `secret` num teste foi renomeado antes do push).
- Restaurar a política de um estágio pelo console grava a lista (mesmo vazia) como definida no console; não há rota para voltar ao "valor da configuração", por desenho (a trilha fica completa).
