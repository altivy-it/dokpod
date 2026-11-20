# Plano de Implementação: Reconexão do agente com espera crescente e aleatória

**Branch**: `035-reconexao-agente-backoff` | **Data**: 2026-11-20 | **Spec**: [spec.md](spec.md)

**Entrada**: Especificação da funcionalidade em `/specs/035-reconexao-agente-backoff/spec.md`

**Origem**: IA assistida; revisor humano: Lincoln (`@lzocateli`).

## Resumo

O laço de `ExecuteAsync` em `backend/apps/Dokpod.Agent/AgentControlWorker.cs` espera `ReconnectDelay` (5 s fixos) após cada queda da sessão de controle. O plano troca essa espera por uma política pura, `AgentReconnectBackoff`, em `backend/libs/Dokpod.Agent.Application/Protocol/`: primeira tentativa imediata, limite `min(30 s, 1 s × 2^(n-1))` na falha consecutiva `n`, espera sorteada uniformemente em `[limite/2, limite]` e recomeço da sequência só depois de uma sessão de pelo menos 60 s. O worker mede a duração da sessão, pede a próxima espera à política e registra um aviso com tentativa, status e espera. O stream de eventos do engine local continua com os 5 s atuais. Nada muda no protocolo nem no servidor.

## Contexto Tecnico

**Linguagem/Versão**: C# / .NET 10 (agente Linux em container e agente Windows self-contained, mesmo `AgentControlWorker`).

**Dependências Principais**: nenhuma nova. `System.Random` (injetável) e `TimeProvider` (já registrado em `backend/apps/Dokpod.Agent/Program.cs:16`).

**Persistência**: N/A. O estado da política vive na memória do processo; reiniciar o agente começa uma sequência nova (FR-001).

**Testes**: xUnit v3 em `backend/tests/Dokpod.Agent.Application.Tests/Protocol/` (política, determinística com `Random` semeado, e simulação de 100 agentes para SC-002/SC-003); regressão da sessão real em `backend/tests/Dokpod.ControlPlane.Api.Tests/Agents/TransportBoundaryTests.cs` (já cobre `RunSessionAsync`). Execução em container `lzocateli/dotnet-sdk:10.0.403-noble-v1`, a mesma imagem do job Backend do CI.

**Plataforma Alvo**: agente Linux em container (Docker/Podman rootless) e agente Windows como Windows Service.

**Tipo de Projeto**: agente do plano de controle self-hosted.

**Metas de Desempenho**: SC-001 (todos reconectados ≤ 60 s após a volta da API), SC-002 (≤ 45 tentativas em 10 min de queda), SC-003 (≤ 20% das tentativas numa janela de 1 s com 100 agentes).

**Restrições**: sem mudança de protobuf, mensagens ou servidor (FR-008); aviso sem segredos (FR-006); cancelamento imediato no stop (FR-005).

**Escala/Escopo**: um arquivo novo na biblioteca de aplicação do agente, ajuste do laço do worker e testes. Até ~100 agentes por instalação (carga de margem da 005).

## Verificação da Constituição

*GATE: aprovado antes da Fase 0 e revisado após o design da Fase 1.*

| Princípio | Atendimento |
| --- | --- |
| I. pt-BR | Artefatos em pt-BR; identificadores preservados. |
| II. Engine local como fonte, workloads preservados | Só muda o tempo entre tentativas de sessão; workloads e engine não são tocados. O stream de eventos do engine fica como está. |
| III. Identidade e menor privilégio | mTLS, revogação e certificados inalterados; o aviso não registra certificado, senha nem metadados da sessão. A revogação continua bloqueando a sessão (001 SC-003). |
| IV. Operações duráveis | Journal, fencing e reconciliação após reconexão inalterados (003); a sessão nova segue o handshake atual. |
| V. Modularidade e compatibilidade | Política pura na biblioteca `Dokpod.Agent.Application` (regra reutilizável); o host compõe. Sem protobuf novo: N/N-1 preservado em ambos os sentidos. Sem datastore, broker ou serviço. |
| VI. Qualidade por plataforma | Testes unitários determinísticos da política e simulação de 100 agentes; regressão da sessão real existente; validação com agente real e restart da API no quickstart. Windows: o mesmo código; a qualificação Windows (006) não é substituída por esta entrega e não é declarada. |

Resultado: **PASS**, sem violações. Revisão após a Fase 1: **PASS** (o design não adicionou contrato nem dependência).

## Estrutura do Projeto

### Documentação desta Funcionalidade

```text
specs/035-reconexao-agente-backoff/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── checklists/requirements.md
└── tasks.md              # gerado por /speckit-tasks
```

Sem `contracts/`: a feature não expõe interface externa nova nem altera `contracts/agent/`.

### Código-Fonte (raiz do repositório)

```text
backend/
├── libs/Dokpod.Agent.Application/Protocol/
│   └── AgentReconnectBackoff.cs                 # novo: política de espera
├── apps/Dokpod.Agent/
│   └── AgentControlWorker.cs                    # laço de ExecuteAsync usa a política
└── tests/Dokpod.Agent.Application.Tests/Protocol/
    └── AgentReconnectBackoffTests.cs            # novo: política e simulação de 100 agentes
```

**Decisão de Estrutura**: a política fica na biblioteca de aplicação do agente, ao lado de `AgentCommandProtocolHandler`, para ser testada sem gRPC nem host; o worker só a compõe. `ReconnectDelay` continua para o stream de eventos do engine (linha 584), renomeado para deixar claro que é do engine local.

## Riscos

| Risco | Mitigação |
| --- | --- |
| Volta mais lenta após restart curto (hoje 5–7 s) | Teto de 30 s mantém SC-001 (≤ 60 s) e SC-004 (alerta padrão de 2 min); premissa aceita na spec. |
| API instável que aceita e derruba a sessão | FR-004: só uma sessão ≥ 60 s zera a sequência. |
| `Random` compartilhado entre threads | A política é usada só pelo laço de `ExecuteAsync` (uma thread lógica); cada instância tem seu próprio `Random`. |
