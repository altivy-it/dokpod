# Plano de Implementação: Identidade, Ambientes e Auditoria

**Branch**: `chore/adotar-spec-kit` (migração) | **Data**: 2026-10-06 | **Spec**: [spec.md](spec.md)

**Estado**: `draft` - rascunho de migração, aguardando revisão humana.

**Entrada**: [MVP](../../docs/plan/mvp.md) e [P03](../../docs/plan/p03-auditoria-persistente.md), congelados.

## Resumo

Confrontar o escopo com a implementação existente e fechar apenas lacunas comprovadas. O legado registra infraestrutura, schema isolado, append idempotente, endpoints, UMA e revogação; esta migração não refaz esses módulos nem certifica os testes históricos.

## Contexto Técnico

**Linguagem/Versão**: C#/.NET 10; integração Angular 22 pelo BFF.

**Dependências Principais**: Keycloak, EF Core/Npgsql existentes, portas de aplicação e contratos versionados; nenhuma adição autorizada.

**Armazenamento**: PostgreSQL, schema Dokpod, partições de auditoria e chave global; identidades individuais sem memberships locais.

**Testes**: Arquitetura, contrato, PostgreSQL isolado real, Keycloak real e E2E por nginx containerizado, somente após autorização.

**Plataforma Alvo**: Plano de controle em containers; agente por gRPC mTLS.

**Tipo de Projeto**: Monólito modular, sem datastore novo.

**Objetivos de Desempenho**: Perfis 56 agentes/1.120 containers/30 usuários e margem >=100/2.000/50 no conjunto 005; thresholds não inventados.

**Restrições**: Append obrigatório, fail-closed, secrets externos, expand-contract, sem alteração de ADR nesta migração.

**Escopo**: Identidade, ambientes, auditoria e evidência residual; sessão e relay em 002.

## Verificação da Constituição

Gate não executado: revisão formal pelo fluxo Spec Kit permanece pendente. A migração não declara aprovação constitucional. [Arquitetura](../../docs/arquitetura.md) e [segurança](../../docs/seguranca.md) restringem o desenho; o trecho legado sobre Redis não autoriza cache novo sem ADR e evidência operacional.

## Estrutura do Projeto

### Documentação desta Feature

- [spec.md](spec.md), [plan.md](plan.md) e [tasks.md](tasks.md).
- Research, data-model, quickstart e contratos adicionais somente se necessários após revisão; não são gerados como suposta evidência.

### Código-fonte e Referências

- `backend/libs/Dokpod.ControlPlane.Infrastructure/`, `backend/apps/Dokpod.ControlPlane.Api/` e `backend/tests/`: persistência, PEP e verificações existentes.
- `contracts/openapi/` e `frontend/tests/`: contrato e jornada pública existente.
- [Runbook de auditoria](../../docs/runbooks/operacao-auditoria-postgresql.md) e [Keycloak](../../docs/configuracao-keycloak.md): operação e autoridade externa.

**Decisão de Estrutura**: Reusar módulos e testes; uma falha confirmada gera somente tarefa residual revisada, não um novo cadastro/writer como feature.

## Rastreabilidade da Migração

| Origem | Destino nesta feature | Tratamento |
| --- | --- | --- |
| MVP P-02 | T002/T007 | Verificar fronteiras, contratos e schema existentes; distribuição em 004 |
| MVP P-03 | T003-T007 | Cadastro, identidade, revogação e auditoria; sessão em 002 |
| P03-01 | T002 | `completed` histórico, mas evidência de ADR `proposed` e aprovação pendente; não resolver por inferência |
| P03-02 | T002/T005 | Infraestrutura existente; conferir diferença e isolamento |
| P03-03 | T005 | Encerramento humano narrado; rollover/privilégios residuais precisam evidência |
| P03-04 | T003/T005 | `in-progress` com narrativa de encerramento humano; preservar ambos |
| P03-05 | T003/T004 | Cadastro/aprovação/suspensão/revogação e bootstrap residual |
| P03-06 | T007 | OpenAPI, readiness, métricas e autorização |
| P03-07 | T004-T007 | Segurança e recuperação; sessão em 002, carga/gates em 005 |

## Lacunas e Decisões Pendentes

- Contagens e pendências históricas são temporais, não resultados desta branch; confirmar evidência por commit, ambiente e cenário.
- Decisão de retenção e inscrição oficial permanecem humanas; isolamento de laboratório não equivale a decisão de produção.
- Nenhuma aprovação de ADR, conclusão de etapa ou atualização de release-readiness é feita aqui.

## Dependências e Estratégia

Revisão humana -> avaliação de diferença -> testes pertinentes -> residual específico -> revalidação. Integração pública depende de 002/004; 005 agrega o gate de release sem repetir testes de identidade. Sem autorização, executar somente análise documental.
