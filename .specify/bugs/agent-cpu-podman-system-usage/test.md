# Verificação de Bug: CPU do container inflada no Podman (spec 009, SC-001)

- **Slug**: agent-cpu-podman-system-usage
- **Testado em**: 2026-10-28
- **Avaliação**: ./assessment.md
- **Correção**: ./fix.md
- **Resultado**: verified (Podman); Docker real pendente

## Resumo

Na VM da spec 005 (Podman 6.1.3 rootless, 8 vCPU), só o agente Podman real foi trocado por uma imagem local da branch de correção (`deploy/agent/Dockerfile`), e o cenário da 009 T018 foi repetido: container com 2 vCPU e 512 MiB, ocioso, depois 1 núcleo ocupado e 200 MiB, depois ocioso de novo. A API de métricas foi lida a cada 1 s e comparada ao `cpu.stat` e ao `memory.current` do cgroup. No fim, o agente voltou para a imagem assinada `v0.1.0-rc.2`.

## Verificações Executadas

| Verificação | Comando / Ação | Resultado | Notas |
| --- | --- | --- | --- |
| CPU com o agente da rc.2 | `vm-t011.sh metricas` + `analisa-009.py` | fail (reproduzido) | 214% do limite em média contra 50% no cgroup; dif. máx. 185 p.p. |
| CPU com a correção | mesmo cenário | pass | 50,3% do limite (12,6% do host); dif. máx. 2,32 p.p., média −0,06 p.p. |
| Memória | mesmo cenário | pass | dif. máx. 0,41 p.p. do limite, igual antes e depois |
| Reflexo da mudança (SC-002) | mesmo cenário | pass | 17,4 s na subida e 25,7 s na descida (limite 30 s) |
| Contadores brutos do Podman | `stats?stream=false` duas vezes em 10 s | causa confirmada | `Δtotal_usage` 1,00e9 ns/s (1 núcleo); `Δsystem_cpu_usage` 1,26e9 ns/s com 8 CPUs |
| Testes da solução | `dotnet test Dokpod.slnx` | pass | 0 falhas |

## Riscos Residuais

- Docker real não foi medido de novo; pela fórmula, o resultado deve ser o mesmo da fórmula oficial do Docker.
