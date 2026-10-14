# Plano de Implementação: Inventário, Lifecycle e Reconciliação

**Branch**: `chore/adotar-spec-kit` (migração) | **Data**: 2026-10-06 | **Spec**: [spec.md](spec.md)

**Estado**: `draft` - rascunho de migração, aguardando revisão humana.

**Entrada**: [MVP](../../docs/plan/mvp.md) P-01/P-02/P-04/P-05 e [arquitetura](../../docs/arquitetura.md).

## Resumo

Confrontar garantias já implementadas com testes e evidências, sem nova feature de inventário/lifecycle. Separar prova Linux de execução Windows em console; journal durável Linux e ACK reportados no legado não certificam filesystem Windows. Aceite histórico não substitui revisão da migração.

## Contexto Técnico

**Linguagem/Versão**: .NET 10, Angular 22, OpenAPI 3.1 e Protobuf.

**Dependências Principais**: Docker adapter, gRPC mTLS, Keycloak PEP, BFF e SignalR existentes; Podman não presumido equivalente.

**Armazenamento**: Projeção/fila/auditoria PostgreSQL; journal local persistente; engine é fonte.

**Testes**: Domínio/aplicação, contratos, PostgreSQL e Docker reais, transporte mTLS e UI pelo nginx containerizado.

**Plataforma Alvo**: Docker Linux; Windows e Podman condicionados ao conjunto 006.

**Tipo de Projeto**: Monólito modular com agente, sem broker novo.

**Objetivos de Desempenho**: Heartbeat 15s/jitter 20%, snapshot 15min/jitter, invalidação <=1/s por ambiente; carga nominal/margem em 005.

**Restrições**: At-least-once, journal-before-ack, dedup/fencing, sem exactly-once ou volume implícito, N/N-1 e expand-contract.

**Escopo**: Validar inventário, mutações e recuperação; não criar containers/stacks, terminal, registries ou alta disponibilidade.

## Verificação da Constituição

Gate Spec Kit pendente de revisão humana, não certificado nesta migração. [Segurança](../../docs/seguranca.md) e arquitetura seguem como restrições; escala horizontal precisa de ADR/lease/ownership/fencing, fora do escopo inicial.

## Estrutura do Projeto

### Documentação desta Feature

[spec.md](spec.md), [plan.md](plan.md) e [tasks.md](tasks.md). Não gerar research/contratos fictícios para garantias existentes.

### Código-fonte e Operação

`backend/apps/Dokpod.Agent/`, `backend/apps/Dokpod.ControlPlane.Api/`, `backend/libs/`, `backend/tests/`, `contracts/agent/`, `contracts/openapi/`, `frontend/web/src/app/` e `frontend/tests/` são referências para futura avaliação.

**Decisão de Estrutura**: Reusar fila, journal, adapter e testes; [runbook de comandos](../../docs/runbooks/operacao-comandos-postgresql.md) mantém a operação proprietária.

## Rastreabilidade da Migração

| Origem | Destino | Tratamento |
| --- | --- | --- |
| MVP P-01 | T002/T004/T005 | Provas Docker/transportes 1/4/5/8; distribuição em 004 e Windows em 006 |
| MVP P-02 | T002/T005 | Fundação/contratos/arquitetura existentes; imagens em 004 |
| MVP P-04 | T003 | Inventário, deltas, snapshots, consulta e UI existentes; carga em 005 |
| MVP P-05 | T004/T006/T007 | Lifecycle, durabilidade, fila, expiração e UI existentes; auditoria compartilhada com 001 |

## Lacunas e Evidências Históricas

- P-04 ainda diz autorização horizontal pendente; P-05 e avaliação de release registram prova posterior. Não apagar nenhum registro nem declarar a suíte atual aprovada.
- P-05 registra screenshots verificadas e também pendentes; confrontar cenário e data, sem reiniciar toda UI como feature.
- Evidência de 16 containers em Docker Desktop não fecha carga, recuperação prolongada ou Docker Linux de produção.
- Duração de retenção/janela de replay e durabilidade equivalente Windows continuam pendentes; descarte segue desabilitado.
- N/N-1 precisa de servidor/agente de versões consecutivas, compatibilidade de contratos e rollback, não só negociação permissiva.

## Dependências e Estratégia

001 fornece identidade/auditoria, 002 a fronteira pública, 004 laboratório e 005 gates integrados. Após autorização, avaliar diferença, validar recortes e adicionar apenas residual comprovado. 006 referencia a prova Linux sem afirmar equivalência Windows/Podman.
