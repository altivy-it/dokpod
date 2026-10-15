# Quickstart: Redes

1. Aplique a migration `202610150004_NetworkInventory`, recrie API, web e agente; crie o scope com `configure-keycloak.ps1` e conceda `network:delete` com `provision-environment-authorization.ps1`.
2. No host Podman: `podman network create rede-teste`, `podman network create usada-teste --subnet 10.89.77.0/24` e `podman create --name usa-rede --network usada-teste nginx` (container parado).
3. Abra Operação → Redes: `rede-teste` sem uso, `usada-teste` em uso por `usa-rede` (parado) com a sub-rede, e `podman` como "Do sistema", em até 30 s.
4. Busque `10.89.77`: encontra `usada-teste`. Abra o detalhe: sub-rede, gateway e o container com link.
5. Remova `rede-teste`: confirmação, justificativa conforme o estágio e resultado "removida".
6. Tente remover `usada-teste` e `podman` pela API: recusas `network_in_use` e `network_system`, redes intactas.
7. Remova o container: `usada-teste` passa a "sem uso".
