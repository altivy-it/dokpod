# Quickstart: Pods

1. Aplique a migration `202610160001_PodInventory` e recrie API, web e agente.
2. No host Podman: `podman pod create --name pod-teste`, `podman run -d --pod pod-teste --name pt-web nginx`, `podman create --pod pod-teste --name pt-parado nginx` (pod degradado) e `podman pod create --name pod-parado-teste`.
3. Abra Operação → Pods: `pod-teste` degradado (1 de 2 em execução), `pod-parado-teste`, `pod_dokpod` com o rótulo "Pod do agente"; compare com `podman pod ps`.
4. Abra o detalhe de `pod-teste`: containers com situação e link; no detalhe de `pt-web`, o link para o pod.
5. Pare `pod-teste` (justificativa conforme o estágio), inicie de novo e reinicie dois pods em lote; confira resultados e auditoria.
6. Tente parar `pod_dokpod` pela API: recusa `agent_pod`, pod intacto.
7. Remova os pods de teste.
