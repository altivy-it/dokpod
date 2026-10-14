# Especificação da Feature: Identidade, Ambientes e Auditoria

**Branch da migração**: `chore/adotar-spec-kit`; branch de implementação a definir após autorização.

**Criada em**: 2026-10-06

**Estado**: `draft` - rascunho de migração, aguardando revisão humana.

**Entrada**: Escopo de identidade do [MVP](../../docs/plan/mvp.md) e do [plano P03](../../docs/plan/p03-auditoria-persistente.md).

## Cenários do Usuário e Testes (obrigatório)

### Jornada do Usuário 1 - Gerenciar ambientes autorizados (Prioridade: P1)

Como administrador autenticado, quero cadastrar, aprovar, suspender e revogar ambientes somente quando a decisão externa permitir.

**Valor desta prioridade**: A autorização precede a revelação de metadados e o controle do host.

**Teste independente**: Exercitar cadastro e leitura com usuários sintéticos de ambientes distintos e dependências indisponíveis.

**Cenários de aceite**:

1. **Dado** acesso ao ambiente A, **Quando** o usuário consulta ou altera B sem permissão, **Então** nenhum metadado de B é revelado nem mutação confirmada.
2. **Dado** decisão ausente, inválida, expirada ou indisponível, **Quando** ocorre cadastro, **Então** a API falha fechada, sem política local substituta.
3. **Dado** retry ou cadastro concorrente, **Quando** a intenção é reenviada, **Então** não há duplicação silenciosa nem confirmação sem auditoria obrigatória.

### Jornada do Usuário 2 - Inscrever e revogar um agente (Prioridade: P1)

Como operador, quero vincular uma identidade individual ao ambiente e impedir reconexão após revogação.

**Valor desta prioridade**: Um agente falso ou clonado não pode operar o host.

**Teste independente**: Exercitar bootstrap de uso único, certificado inválido, clone concorrente e revogação durante stream gRPC mTLS.

**Cenários de aceite**:

1. **Dado** bootstrap consumido ou ambiente divergente, **Quando** ocorre inscrição, **Então** ela é rejeitada sem ativar segunda identidade.
2. **Dado** certificado revogado, **Quando** o agente reconecta, **Então** a sessão é recusada e o stream ativo anterior é encerrado.

### Jornada do Usuário 3 - Consultar trilha íntegra (Prioridade: P1)

Como auditor autorizado, quero eventos duráveis correlacionados sem acessar dados de outro ambiente.

**Valor desta prioridade**: Auditoria deve sobreviver a retry, falha e recuperação.

**Teste independente**: Validar append concorrente e privilégios com PostgreSQL real isolado e restaurar evidências sintéticas.

**Cenários de aceite**:

1. **Dado** mesmo `EventId` e payload, **Quando** o append é repetido, **Então** permanece um evento; payload divergente é rejeitado.
2. **Dado** falha do writer obrigatório, **Quando** uma operação auditável é solicitada, **Então** ela não é confirmada como sucesso.
3. **Dado** papel runtime, **Quando** tenta alterar ou excluir auditoria, **Então** o banco rejeita a operação.

## Casos de Borda

- Keycloak/PostgreSQL indisponível, cancelamento, decisão malformada e resposta perdida.
- Bootstrap simultâneo, fingerprint divergente, agente falso, clone, expiração, rotação e revogação.
- Retry idêntico/conflitante, virada de mês, datas fora da janela, rollover e restore.
- Catálogo paginado sem total global e auditoria sem `audit:read`.

## Requisitos (obrigatório)

### Requisitos Funcionais

- **FR-001**: Keycloak DEVE ser autoridade de usuários e autorização; a API é PEP deny-by-default. Não copiar memberships ou políticas para PostgreSQL.
- **FR-002**: Cadastro, aprovação, suspensão, leitura, revogação e auditoria DEVEM autorizar ação/recurso antes de revelar dados; o catálogo omite ambientes negados, usa cursor e não expõe total global.
- **FR-003**: Agentes DEVEM usar identidade individual por ambiente, bootstrap de uso único armazenado como hash, aprovação por fingerprint, rotação e revogação; o ambiente é derivado do certificado gRPC mTLS, não de entrada confiada ao agente.
- **FR-004**: Auditoria DEVE ser append-only, contendo ator, ação, ambiente opaco, UTC, correlação, resultado e código de falha, com append obrigatório antes de confirmação e atomicidade onde exigida pelo caso de uso.
- **FR-005**: Retry idêntico DEVE ser idempotente; payload divergente, concorrência e falhas não relacionadas à unicidade não podem ser mascarados como sucesso/conflito.
- **FR-006**: Auditoria DEVE usar partições mensais, criação antecipada, partição de segurança, índices, pruning e rollover testados; retenção e descarte dependem de decisão humana, sem apagamento automático nesta migração.
- **FR-007**: Schema Dokpod DEVE ser isolado, com runtime sem `UPDATE`/`DELETE` de auditoria e migrations expand-contract compatíveis com rollback.
- **FR-008**: REST DEVE ser versionado, usar OpenAPI e `application/problem+json`; logs, métricas e readiness não contêm secrets, tokens nem payload integral do engine.
- **FR-009**: Cadastro, revogação, indisponibilidade e recuperação DEVEM ter evidência PostgreSQL/Keycloak reais; login, CSRF e sessão pertencem ao [conjunto 002](../002-sessao-bff-relay-realtime/spec.md).

### Entidades Principais

- **Ambiente/identidade de agente**: recurso opaco, fingerprint, estado e revogação; não contém política local.
- **AuditEvent**: evento imutável, chave global de idempotência e payload limitado.

## Critérios de Sucesso (obrigatório)

### Resultados Mensuráveis

- **SC-001**: Todos os cenários negativos exercitados bloqueiam revelação e mutação de ambientes negados.
- **SC-002**: Retry e corrida preservam uma intenção/evento e rejeitam divergências sem sobrescrita.
- **SC-003**: Revogação encerra sessão e bloqueia reconexão; bootstrap concorrente não ativa clones.
- **SC-004**: Testes reais comprovam privilégios, roteamento temporal, recuperação e ausência de dados sensíveis na trilha.

## Premissas e Lacunas

Registro, catálogo, writer, schema, UMA e revogação já possuem evidências no legado; não são novas entregas presumidas. Enrollment/provisionamento oficial não é provado pela identidade sintética do laboratório. Aprovação/suspensão, rotação, corrida de bootstrap e retenção exigem avaliação de diferença antes de qualquer implementação. Não aprovar dependências ou ADRs por inferência.

## Proveniência da Migração

Origem: MVP P-03 e P-02 (fronteira de persistência/contratos); P03-01 a P03-07. P03-07 divide sessão com 002 e carga/recuperação de release com 005. O [plano](plan.md) e as [tarefas](tasks.md) cobrem somente avaliação, validação e residual autorizado.
