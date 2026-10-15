# Runbook: alertas, silenciamentos e justificativas

## Escopo

Tabelas da spec 016 (migration `202610140001_AlertGovernance`): `dokpod.alert_rules`, `dokpod.alerts`, `dokpod.alert_timeline`, `dokpod.alert_silences`, `dokpod.action_justification_policies` e `dokpod.container_action_justifications`. Não são particionadas; o volume é baixo porque existe no máximo um alerta ativo por regra e alvo.

## Retenção

- O `AlertSweeper` remove uma vez por dia os alertas **resolvidos** há mais de `Alerts:ResolvedRetention` (padrão 90 dias, mínimo 1 dia); a linha do tempo sai junto (`ON DELETE CASCADE`). Alertas ativos nunca são removidos.
- A varredura roda a cada `Alerts:SweepInterval` (padrão 30 s, mínimo 1 s).
- Justificativas de ações (`container_action_justifications`) e silenciamentos não têm retenção automática: seguem a política de retenção da auditoria da instalação. Antes de apagar, exporte o período (são evidência de paradas intencionais).

```sql
-- Volume por estado e idade do alerta resolvido mais antigo.
SELECT state, count(*) FROM dokpod.alerts GROUP BY state;
SELECT min(resolved_at_utc) FROM dokpod.alerts WHERE state = 'resolved';

-- Execução manual equivalente à retenção (90 dias).
DELETE FROM dokpod.alerts WHERE state = 'resolved' AND resolved_at_utc < now() - INTERVAL '90 days';
```

## Sinais

- log `Alert sweep failed` repetido (varredura parada: resolução automática, limites e fim de silenciamento deixam de acontecer);
- alertas `open` antigos de containers que não existem mais: a ausência no inventário só resolve depois de 2 minutos sem nova ocorrência; com o agente desconectado, o inventário não muda e o alerta permanece até a reconexão;
- silenciamentos vigentes de longa duração (`SELECT * FROM dokpod.alert_silences WHERE ended_at_utc IS NULL AND ends_at_utc > now();`).

## Política de justificativa

A política por estágio fica em `dokpod.action_justification_policies` (padrão: obrigatória em Produção). Altere pela tela Governança → Alertas → Regras ou por `PUT /api/v1/action-justification-policies/{stage}` (exige `environment:manage` em todos os hosts do estágio); não altere a tabela diretamente, porque a mudança não seria auditada.

## Scopes

Ambientes cadastrados antes da spec 016 precisam dos scopes UMA novos: execute `tools/scripts/configure-keycloak.ps1` (cria `alert:read` e `alert:manage` no resource server) e `tools/scripts/provision-environment-authorization.ps1 -Scope alert:read|alert:manage` para cada ambiente e grupo.
