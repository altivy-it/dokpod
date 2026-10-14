# Plano de Implementação: Qualificação Windows e Podman

**Branch**: `chore/adotar-spec-kit` (migração) | **Data**: 2026-10-06 | **Spec**: [spec.md](spec.md)

**Estado**: `draft` - rascunho de migração, aguardando revisão humana.

**Entrada**: [MVP](../../docs/plan/mvp.md) e [desbloqueio](../../docs/plan/desbloqueio-release.md), sem execução autorizada.

## Resumo

Qualificar apenas diferenças de plataforma/capability não comprovadas. Execução Windows em console e DryRun são evidências históricas limitadas; não equivalem a serviço em host limpo ou journal crash-safe. Não reimplementar núcleo/script/publish antes de avaliar diferença. Podman requer engine real e adapter específico quando necessário.

## Contexto Técnico

**Linguagem/Versão**: C#/.NET 10, Worker Service self-contained Windows, agente OCI Linux.

**Dependências Principais**: Docker named pipe, Podman local rootless/rootful, gRPC mTLS e scripts existentes.

**Armazenamento**: Journal/identidade/certificados persistentes, ACL Windows e mounts Linux; PostgreSQL só projeção.

**Testes**: Provas 2/3, engines reais, instalação/smoke Windows sem runtime, fault injection, update/rollback e N/N-1.

**Plataforma Alvo**: Matriz Windows Server/RID/Docker a decidir; Podman Linux por versão/modo. Nada estável por inferência.

**Tipo de Projeto**: Qualificação de plataforma, sem alterar arquitetura aceita.

**Objetivos de Desempenho**: Limits/capabilities exercitados por matriz; capacidade nominal/margem em 005, sem promessa adicional inventada.

**Restrições**: Windows Service nativo self-contained, journal-before-ack, ACL mínima, identidade preservada e licença permissiva para dependências.

**Escopo**: Windows e Podman Linux; fora do escopo Podman nativo com Windows containers.

## Verificação da Constituição

Gate formal pendente de revisão humana. Preservar [arquitetura](../../docs/arquitetura.md), [segurança](../../docs/seguranca.md), [distribuição](../../docs/distribuicao.md) e [viabilidade](../../docs/viabilidade.md). Não aprovar ADR, matriz, elevação nem instalação nesta migração.

## Estrutura do Projeto

### Documentação desta Feature

[spec.md](spec.md), [plan.md](plan.md) e [tasks.md](tasks.md); relatório real somente após prova autorizada.

### Módulos Existentes

`backend/apps/Dokpod.Agent/`, `backend/libs/`, `backend/tests/`, `contracts/agent/`, `deploy/agent/` e `deploy/agent/manage-windows-service.ps1` são superfícies de avaliação futura, não edição nesta migração.

**Decisão de Estrutura**: Mesmo contrato/casos de uso do agente; diferenças de lifecycle/filesystem/socket/pipe ficam em adapters de plataforma, sem presumir paridade.

## Rastreabilidade da Migração

| Origem | Destino | Tratamento |
| --- | --- | --- |
| MVP P-01 | T002/T003 | Console Windows/pipe/mTLS e DryRun existentes, não serviço qualificado |
| MVP P-02 | T002/T003 | Publish self-contained reproduzível existente, RID/matriz a verificar |
| MVP P-05 | T004 | Garantia equivalente Windows explicitamente pendente |
| MVP P-06 | T003/T005/T006 | Assinatura/pacote, ACL/update/rollback e suporte residual |
| MVP P-07 | T003-T006 | Provas 2/3, Windows e Podman rootless/rootful; classificação humana |
| Desbloqueio P-05 | T003-T006 | Recortes Windows/Podman; Docker Linux em 005 |

## Lacunas e Estratégia

Não existe evidência suficiente de host limpo/serviço/ACL/update/rollback nem Podman. Qualificar armazenamento Windows antes de alegar durabilidade equivalente ao journal Linux. Identificar conta/RID/versões por revisão humana; não escolher valores silenciosamente. 003 oferece garantias e testes comuns, 004 distribuição e 005 supply chain/GO-NO-GO. Pode haver adiamento formal de ambas capabilities sem transformá-las em stable nem apagar pendências. Revisão -> diferença -> provas autorizadas -> residual -> decisão por capability.
