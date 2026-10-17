# Tarefas: Qualificação na stack real

**Entrada**: [spec.md](spec.md), [plan.md](plan.md).

**Estado**: `done` - executada em 2026-10-17.

- [x] T001 Perfil `docker-real` no stack de teste (Docker 28 aninhado + agente) e documentação em `deploy/e2e/README.md`. Evidência: perfil `docker-real` em `deploy/e2e/docker-compose-dokpod.yaml` (Docker 28.5.2, API 1.51, overlay2, cgroup v2) e `deploy/e2e/README.md`; identidade do agente Docker registrada no laboratório.
- [x] T002 [US1] Docker real: inventário (containers, imagens, volumes, redes, registros, Pods "sem pods") igual ao `docker` CLI; ações, lote, pausa, logs, inspect, métricas, terminal, criação e pull pelo console. Evidência: containers (6 estados), imagens, volumes, redes e registros iguais ao `docker` CLI; Pods mostra o host como "sem pods"; resumo, inspeção, logs e métricas; parar, iniciar, reiniciar, pausar, retomar e remover; pull e criação; recusa de reinício por health check (`unsupported_option`); remoção de volume órfão e recusa de volume em uso; remoção de rede vazia; auditoria com 13 tipos de ação. Achado: o agente anunciava `PodActions` no Docker (corrigido).
- [x] T003 [US2] N-1: imagens do commit `2867c25`; agente N-1 ↔ servidor N e agente N ↔ servidor N-1 por 10 minutos com inventário e comandos. Evidência: imagens do commit `2867c25`. Agente N-1 ↔ servidor N: 10 minutos (14:24–14:34 UTC) sem reconexão, comandos funcionando, zero erros. Agente N ↔ servidor N-1: **falha** (`agent_hello_invalid`); corrigido para versões futuras e documentada a ordem de atualização. Achado: agente rebaixado mostrava inventário antigo como atual (corrigido).
- [x] T004 [US3] Usuário só de leitura na stack real: menu, telas e recusas da API. Evidência: usuário temporário `dok023-leitor` só com `environment:read`/`alert:read`; vê os hosts, não vê Configurações nem Auditoria, nenhuma ação oferecida, políticas somente leitura; a API recusa ações, pull, criação, remoção, políticas, estágio e terminal. Achados: permissões do Keycloak do tipo `resource` impediam acesso parcial; o console oferecia ações pelo cadastro do host (ambos corrigidos).
- [x] T005 [US3] Registro privado com credencial: pull e criação; sem credencial, falha legível; varredura da credencial. Evidência: `registry:2` com htpasswd no Docker real; pull autenticado e criação a partir da imagem privada; sem credencial → `registry_unauthorized` (antes `image_pull_failed`, corrigido); senha do registro com 0 ocorrências.
- [x] T006 [US4] Canários ponta a ponta (variável, label, health check, opção de log, termo de busca) em respostas, PostgreSQL e logs. Evidência: 9 canários aleatórios; 0 ocorrências em 76 respostas, no PostgreSQL (`pg_dump` do schema `dokpod`) e nos logs de nginx, BFF, API, agentes e engine. Achado: comando de saúde em claro no comando equivalente e nos parâmetros (corrigido; a entrega ao engine continua com o comando real e os valores cifrados são apagados ao terminar).
- [x] T007 [US4] Terminal real pelo navegador: prompt, eco, encerramento por fechamento, ociosidade e revogação; conteúdo digitado fora da auditoria. Evidência: Docker (prompt 0,19 s, eco 29 ms) e Podman (0,22 s, 8 ms); encerramento por botão, queda do navegador, ociosidade (60 s) e revogação (55 s, `permission_revoked`); conteúdo digitado fora da auditoria. Achado: revogação auditada como `user_closed` (corrigido).
- [x] T008 Correções de defeitos encontrados, com testes. Evidência: 8 commits de correção, cada um com teste que reproduz o defeito; backend 677, Vitest 177, E2E simulada 104, bundle inicial 419,72 kB.
- [x] T009 Evidências nas specs de origem, lista final de `NOT RUN` com destino (SC-004), limpeza do laboratório. Evidência: notas nas specs 008 a 021; recursos de teste removidos (Docker aninhado e volumes, registro, containers e imagens de teste, imagens N-1, worktree, usuário e política do Keycloak, canários); a conversão das permissões do Keycloak para o tipo scope foi mantida (é a correção).

## Pendências após a spec 023 (SC-004)

| Item | Spec de origem | Destino |
| --- | --- | --- |
| Carga de 56 hosts/1.120 containers e margem de 2.000; carga do canal de logs; 4 sessões de terminal por agente; SC-002 com 20 containers em lote | 009, 011, 014, 015, 010 | Frente 2 (qualificação de release) |
| Logs ao vivo medidos pela stack real; imagem real com `VOLUME` + remoção automática | 011, 013 | Frente 2 |
| Suíte E2E autenticada versionada (`test_authenticated_*`) pela borda | 007 | Frente 2 |
| Docker instalado diretamente num host, Podman rootful, agente Windows | 005, 008, 013 | Matriz da spec 005 |
| Matriz GO/NO-GO | 005 | Frente 2 |

## Achados

1. Agente anunciava `PodActions` no Docker.
2. Agente rebaixado (N-1) exibia inventário gravado por versão mais nova como atual (pods, volumes, redes, imagens, registros).
3. Servidor recusava agente mais novo (capability desconhecida) e encerrava a sessão em mensagem desconhecida; servidores anteriores continuam recusando — ordem de atualização documentada.
4. Permissões do Keycloak do tipo `resource` tornavam o acesso parcial impossível; decisão unânime entre policies impediria conceder o mesmo scope a dois grupos.
5. Console oferecia ações pelo cadastro do host, não pelas permissões do usuário (lista, detalhe e ambiente).
6. Comando de saúde em claro no comando equivalente (resposta e auditoria) e nos parâmetros do comando.
7. Pull sem credencial no Docker caía no código genérico.
8. Revogação de `container:exec` derrubava a sessão pelo console e era auditada como `user_closed`.
