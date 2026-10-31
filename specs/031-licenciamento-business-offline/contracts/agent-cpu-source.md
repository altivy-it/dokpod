# Contrato: origem da contagem de CPU no `AgentHello`

**Decisão**: [D-004](../research.md#d-004-identidade-do-host-e-contagem-de-cpu) | **Arquivo**: `contracts/agent/v1/agent.proto`

Mudança aditiva, compatível N/N-1. `cpu_count` (tag 10) permanece com o mesmo significado e tipo.

```proto
enum CpuCountSource {
  CPU_COUNT_SOURCE_UNSPECIFIED = 0;     // agente N-1: tratar como desconhecido
  CPU_COUNT_SOURCE_ENGINE = 1;          // informado pelo engine (Docker/Podman info)
  CPU_COUNT_SOURCE_PROCESS_FALLBACK = 2; // Environment.ProcessorCount do processo do agente
}

message AgentHello {
  // ... campos existentes, inclusive uint32 cpu_count = 10;
  CpuCountSource cpu_count_source = 14;
}
```

- A tag 14 está reservada para a 031 no [registro de alocação de contratos](../../README.md#registro-de-alocação-de-contratos) (T001, 2026-10-31).
- O agente preenche `ENGINE` quando `EngineHostInfo.CpuCount > 0` e `PROCESS_FALLBACK` quando usa `Environment.ProcessorCount` (`backend/apps/Dokpod.Agent/AgentControlWorker.cs`, `CreateHello`).
- O plano de controle só grava observação confiável com `ENGINE`; `PROCESS_FALLBACK` e `UNSPECIFIED` entram como `source = process_fallback|unknown` e contam como medição desconhecida (FR-013).
- Agente N com plano de controle N-1: o campo é ignorado, sem efeito na sessão.
- Falha ao registrar observação nunca derruba a sessão de comandos do agente.
