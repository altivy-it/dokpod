# Plano de Implementação: Distribuição Containerizada

**Branch**: `chore/adotar-spec-kit` (migração) | **Data**: 2026-10-06 | **Spec**: [spec.md](spec.md)

**Estado**: `draft` - rascunho de migração, aguardando revisão humana.

**Entrada**: [P04](../../docs/plan/p04-padronizacao-dockerfiles-compose.md) e [distribuição](../../docs/distribuicao.md).

## Resumo

Reusar imagens/topologia existentes e confrontar paridade, contexto limpo, segurança e persistência. Os testes históricos de startup e falhas posteriores não são apagados nem convertidos em conclusão. Nenhuma aplicação é executada nesta migração.

## Contexto Técnico

**Linguagem/Versão**: .NET 10, Angular 22, Compose e nginx.

**Dependências Principais**: Bases/toolchains fixadas da matriz, plataforma central de identidade/gateway; sem novas imagens ou dependências aprovadas aqui.

**Armazenamento**: Mounts persistentes e PostgreSQL central com namespace Dokpod.

**Testes**: Build limpo, configuração, inspeção, smoke e integração pública por nginx em containers.

**Plataforma Alvo**: Docker Linux; agente Windows self-contained não vira container obrigatório.

**Tipo de Projeto**: Distribuição OCI da aplicação modular.

**Objetivos de Desempenho**: Paridade/reprodutibilidade; carga nominal e margem em 005.

**Restrições**: Sem secrets no repo/imagens; sem plataforma central duplicada ou bypass do BFF; socket é privilegiado.

**Escopo**: Imagens, Compose, borda e persistência runtime; relay funcional em 002, release em 005.

## Verificação da Constituição

Gate formal pendente de revisão humana; não declarar aderência avaliada. [Arquitetura](../../docs/arquitetura.md) e [segurança](../../docs/seguranca.md) delimitam topologia e privilégio. Mudança de rede/infraestrutura exige resolver divergências antes de editar runtime.

## Estrutura do Projeto

### Documentação desta Feature

[spec.md](spec.md), [plan.md](plan.md) e [tasks.md](tasks.md); quickstart novo somente se faltar procedimento proprietário após revisão.

### Módulos Existentes

`backend/apps/Dokpod.Bff/Dockerfile`, `backend/apps/Dokpod.ControlPlane.Api/Dockerfile`, `frontend/web/Dockerfile`, `deploy/agent/Dockerfile` e `deploy/e2e/docker-compose-dokpod.yaml` são os alvos históricos. `deploy/nginx/` e a plataforma central delimitam borda; não editar outro repositório como efeito colateral.

**Decisão de Estrutura**: Preservar Dockerfiles proprietários e Compose canônico, sem adicionar stack paralela.

## Rastreabilidade da Migração

| Origem | Destino | Tratamento |
| --- | --- | --- |
| P04-01 | T003 | Dockerfile BFF existente, avaliar targets/paridade |
| P04-02 | T003 | Web/API/agente existentes, avaliar build/contexto/runtime |
| P04-03 | T004 | Compose/rede central e ausência de duplicação |
| P04-04 | T005 | Startup/health/mTLS/login e persistência; relay em 002 |
| MVP P-01 | T003/T005 | Build e smoke agente existentes |
| MVP P-02 | T003/T006 | Imagens/toolchains/gates mínimos; contratos em 003 |
| P05-07 | T004/T005 | Nginx, secrets, key ring e portas; testes de browser em 002 |
| MVP P-06; desbloqueio P-05 | T006 e 005/006 | Distribuição inspecionada, qualificação/suporte decididos separadamente |

## Lacunas e Estratégia

P04 não tem status por etapa; não inferir `not-started` nem `completed`. Registra stack saudável e pendência `up --wait`; registrar qual cenário é realmente residual. O P03 menciona `identity-client`, P04 `identity-global`: diferença de contexto/data a revisar, sem reconciliar por edição de Compose nesta migração. Builds locais não substituem inspeções automatizadas/CI remoto de 005. Revisão -> diferença -> inspeções -> residual autorizado -> evidência.
