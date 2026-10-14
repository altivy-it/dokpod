# Quickstart de Validação: Métricas

1. Stack E2E com agente Docker e agente Podman rootless (008 T027), sem montagens além do socket do engine.
2. Iniciar container `stress` sintético com `--cpus 1 --memory 256m` e carga de 50% CPU e 200 MiB.
3. Conferir `docker stats` versus detalhe (Recursos agora e Estatísticas) e lista de containers (SC-001); mudar a carga e medir o tempo até o valor atual refletir (≤ 30 s, SC-002).
4. Saturar um host acima de 80%: barra de alerta na Visão geral, Hosts e seleção de host do Novo container.
5. Parar o agente por 10 minutos: lacuna no gráfico, não zero.
6. Reiniciar a API: série de 12 h dos hosts preservada; série de container recomeça e se recompõe em até 30 min; nenhuma linha de container no PostgreSQL.
7. Reduzir retenção em teste e confirmar remoção de linhas e partições antigas de `host_resource_samples`.
8. Rodar carga de capacidade (56 hosts simulados/1.120 containers) e de margem (2.000 containers) e medir SC-003/SC-004.
9. Inspecionar payloads capturados: nenhum nome de processo, argumento, variável ou label (SC-006).
10. Loop visual (1920, 1440, 1100, 390 e 320px) dos pontos alterados, com revisão humana registrada.
