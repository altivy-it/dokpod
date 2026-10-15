# Quickstart: Volumes

1. Aplique a migration `202610150003_VolumeInventory`, recrie API, web e agente; conceda `volume:delete` com `provision-environment-authorization.ps1`.
2. No host Podman: `podman volume create dados-teste`, `podman volume create usado-teste` e `podman run -d --name usa-volume -v usado-teste:/dados nginx`.
3. Abra Operação → Volumes: `dados-teste` sem uso e `usado-teste` em uso por `usa-volume`, em até 30 s.
4. Abra o detalhe de `usado-teste`: montagem `/dados` com link para o container; tamanho quando o engine informa.
5. Remova `dados-teste`: confirmação, justificativa conforme o estágio, resultado "removido" e evento "volume removido".
6. Tente remover `usado-teste` (pela API): recusa `volume_in_use`, volume intacto.
7. Pare `usa-volume`: o volume continua "em uso"; remova o container e confira que o volume passa a "sem uso".
