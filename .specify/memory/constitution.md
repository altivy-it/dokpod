# Constituição do Dokpod

## Princípios Fundamentais

### I. Documentação em Português Brasileiro

Toda documentação produzida ou atualizada deve estar em português brasileiro,
incluindo specs, planos, tarefas, checklists e relatórios de defeitos. Preserve
comandos, identificadores, termos de contrato e marcadores literais das ferramentas.

### II. Engine Local como Fonte e Workloads Preservados

Docker/Podman local é a fonte de verdade; o inventário PostgreSQL é uma projeção
reconstruível. Somente o agente acessa o engine local, sem socket exposto pela rede
nem proxy genérico. Indisponibilidade do controle ou do agente não interrompe
workloads. Excluir container não remove volume implicitamente.

### III. Identidade Externa e Menor Privilégio

Keycloak é a autoridade de identidade e autorização dos usuários. O BFF mantém
tokens fora do browser; a API aplica as decisões antes de revelar ou operar
recursos e falha fechada. Agentes têm identidade individual, mTLS, rotação e
revogação. Secrets permanecem fora do repositório; telemetria não inclui secrets,
chaves privadas, variáveis de ambiente ou logs integrais de containers.

### IV. Operações Duráveis e Recuperáveis

Comandos mutáveis exigem autorização, ID idempotente, expiração, auditoria e
reconciliação. O agente persiste no journal antes do aceite, valida sessão/fencing,
deduplica e reconcilia após resposta perdida ou crash. Entrega at-least-once não
é promessa de exactly-once. Docker e Podman anunciam capabilities distintas;
operações desconhecidas ou não suportadas são recusadas explicitamente.

### V. Arquitetura Modular e Compatibilidade

Hosts compõem; bibliotecas implementam regras reutilizáveis; contratos não dependem
de aplicações. APIs públicas têm OpenAPI e versão explícita. Protobuf preserva
tags e exige compatibilidade N/N-1 ou migração/rollout explicitamente decididos.
Schemas seguem expand-contract com rollback verificável. Novo datastore, broker,
cache distribuído ou microsserviço exige ADR e evidência operacional.

### VI. Qualidade Verificável por Plataforma

Mudanças de comportamento exigem testes proporcionais ao risco. Adapters e
PostgreSQL usam integração real; jornadas de usuário usam E2E pela borda nginx.
Docker Linux não qualifica Podman nem Windows. Release exige evidências de carga
nominal/margem, recuperação, segurança, supply chain e revisão humana da matriz.
`NOT RUN` não é sucesso; o NO-GO atual não muda pela migração documental.

## Restrições Técnicas

- Backend e agente usam .NET 10; frontend Angular 22. Backend, BFF, frontend,
agente Linux, builds e runners executam em containers.
- A exceção Windows é o Worker Service self-contained; instalação e smoke test
ocorrem em host sem runtime .NET e não são substituídos por execução em console.
- Navegador e runner HTTP acessam a borda nginx containerizada, nunca API/BFF
diretamente. Testes unitários sem HTTP não precisam de proxy ocioso.
- Dependências exigem licença permissiva verificada e compatível com a distribuição
AGPL-3.0-only, versão fixada, manutenção e necessidade demonstradas.
- Tabelas de alto crescimento ou retenção temporal exigem partições por faixa de
data, criação antecipada, rollover, segurança fora da janela, índices e retenção.
- Operações privilegiadas e novas fronteiras exigem threat model e testes negativos.

## Processo de Desenvolvimento e Manutenção

Spec Kit é o único processo autorizado para features, mudanças de produto e
manutenção da aplicação. Instruções, agentes, prompts, Issues e ADRs apoiam o
processo, mas não criam requisitos, planos ou execução concorrentes.

Features usam `specs/<feature>/spec.md`, `plan.md` e `tasks.md`, por meio de
`/speckit-specify`, `/speckit-clarify` quando necessário, `/speckit-plan`,
`/speckit-tasks`, `/speckit-analyze`, autorização humana, `/speckit-implement`
e `/speckit-converge`. Novas tarefas de convergência precisam revisão/autorização.
Defeitos usam Bug Fixing oficial: `/speckit-bug-assess`, `/speckit-bug-fix` e
`/speckit-bug-test`, com relatórios em `.specify/bugs/<slug>/`.

Nenhum trabalho de aplicação começa sem artefato ativo, escopo revisado e
autorização humana explícita. Issue `Ready`, checkbox ou arquivo existente não
equivale a autorização. Checkboxes registram execução e validação, não aprovação.

Os cinco planos em `docs/plan/` ficam congelados. Seis conjuntos migrados em
rascunho preservam histórico e pendências, aguardando revisão humana. Não execute
essas iniciativas pela aprovação histórica nem arquive os originais antes da
revisão. ADRs registram decisões duradouras, não duplicam spec/plano e não autorizam
implementação; IA cria somente `proposed`, com revisor humano identificado.

## Governança

Esta constituição registra princípios duradouros; `.github/` detalha procedimentos.
README e documentos de arquitetura/segurança fornecem contexto e ADRs aceitos
registram decisões; specs descrevem cada mudança. Conflitos exigem decisão humana,
nunca aprovação inferida. Mudanças constitucionais exigem revisão e aprovação.

Versione semanticamente: PATCH para esclarecimento, MINOR para expansão compatível
de princípios e MAJOR para remoção ou redefinição incompatível. Reviews verificam
conformidade e exceções referenciadas. Não faça commit, push, publicação ou deploy
sem solicitação explícita; a autorização desta migração não autoriza release.

**Versão**: 1.0.0 | **Ratificada**: 2026-10-06 | **Última alteração**: 2026-10-06
