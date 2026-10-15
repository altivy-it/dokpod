-- Spec 016: governança de alertas (regras, alertas, linha do tempo, silenciamentos) e ações justificadas.
CREATE TABLE dokpod.alert_rules (
    rule_id uuid NOT NULL CONSTRAINT pk_alert_rules PRIMARY KEY,
    name character varying(120) NOT NULL,
    description character varying(500),
    condition_kind character varying(16) NOT NULL,
    event_type character varying(64),
    metric character varying(32),
    threshold_percent numeric(5,2),
    duration_seconds integer NOT NULL DEFAULT 0,
    min_occurrences integer NOT NULL DEFAULT 1,
    severity character varying(16) NOT NULL,
    scope_stage character varying(16),
    scope_environment_id uuid,
    scope_container_pattern character varying(128),
    enabled boolean NOT NULL DEFAULT true,
    built_in boolean NOT NULL DEFAULT false,
    version integer NOT NULL DEFAULT 1,
    updated_by character varying(255),
    updated_at_utc timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT ck_alert_rules_condition CHECK (
        (condition_kind = 'event' AND event_type IS NOT NULL AND metric IS NULL AND threshold_percent IS NULL)
        OR (condition_kind = 'metric' AND metric IN ('host_cpu', 'host_memory', 'container_cpu', 'container_memory')
            AND threshold_percent BETWEEN 1 AND 100 AND event_type IS NULL)),
    CONSTRAINT ck_alert_rules_severity CHECK (severity IN ('critical', 'high', 'medium', 'low')),
    CONSTRAINT ck_alert_rules_duration CHECK (duration_seconds BETWEEN 0 AND 3600),
    CONSTRAINT ck_alert_rules_occurrences CHECK (min_occurrences BETWEEN 1 AND 100),
    CONSTRAINT ck_alert_rules_stage CHECK (scope_stage IS NULL OR scope_stage IN ('production', 'staging', 'development'))
);

CREATE TABLE dokpod.alert_silences (
    silence_id uuid NOT NULL CONSTRAINT pk_alert_silences PRIMARY KEY,
    scope_stage character varying(16),
    scope_environment_id uuid,
    scope_container_pattern character varying(128),
    alert_id uuid,
    starts_at_utc timestamp with time zone NOT NULL,
    ends_at_utc timestamp with time zone NOT NULL,
    ended_at_utc timestamp with time zone,
    justification character varying(500) NOT NULL,
    created_by character varying(255) NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    CONSTRAINT ck_alert_silences_window CHECK (ends_at_utc > starts_at_utc AND ends_at_utc <= starts_at_utc + interval '7 days')
);

CREATE INDEX ix_alert_silences_active ON dokpod.alert_silences (ends_at_utc) WHERE ended_at_utc IS NULL;

CREATE TABLE dokpod.alerts (
    alert_id uuid NOT NULL CONSTRAINT pk_alerts PRIMARY KEY,
    rule_id uuid NOT NULL,
    environment_id uuid NOT NULL,
    container_id character varying(128),
    container_name character varying(255),
    target_key character varying(200) NOT NULL,
    severity character varying(16) NOT NULL,
    state character varying(16) NOT NULL,
    condition_text character varying(300) NOT NULL,
    opened_at_utc timestamp with time zone NOT NULL,
    last_occurred_at_utc timestamp with time zone NOT NULL,
    occurrences integer NOT NULL DEFAULT 1,
    assignee character varying(255),
    silence_id uuid,
    resolved_at_utc timestamp with time zone,
    resolution_reason character varying(64),
    CONSTRAINT fk_alerts_rules FOREIGN KEY (rule_id) REFERENCES dokpod.alert_rules (rule_id),
    CONSTRAINT ck_alerts_state CHECK (state IN ('open', 'acknowledged', 'silenced', 'resolved')),
    CONSTRAINT ck_alerts_severity CHECK (severity IN ('critical', 'high', 'medium', 'low')),
    CONSTRAINT ck_alerts_occurrences CHECK (occurrences >= 1),
    CONSTRAINT ck_alerts_resolution CHECK ((state = 'resolved') = (resolved_at_utc IS NOT NULL))
);

-- FR-003: no máximo um alerta não resolvido por regra e alvo.
CREATE UNIQUE INDEX ux_alerts_active_target ON dokpod.alerts (rule_id, target_key) WHERE state <> 'resolved';
CREATE INDEX ix_alerts_state_environment ON dokpod.alerts (state, environment_id);
CREATE INDEX ix_alerts_resolved_at ON dokpod.alerts (resolved_at_utc) WHERE resolved_at_utc IS NOT NULL;

CREATE TABLE dokpod.alert_timeline (
    entry_id bigint GENERATED ALWAYS AS IDENTITY CONSTRAINT pk_alert_timeline PRIMARY KEY,
    alert_id uuid NOT NULL,
    occurred_at_utc timestamp with time zone NOT NULL,
    kind character varying(16) NOT NULL,
    actor character varying(255),
    comment character varying(500),
    CONSTRAINT fk_alert_timeline_alerts FOREIGN KEY (alert_id) REFERENCES dokpod.alerts (alert_id) ON DELETE CASCADE,
    CONSTRAINT ck_alert_timeline_kind CHECK (kind IN ('opened', 'occurred', 'acknowledged', 'silenced', 'unsilenced', 'resolved'))
);

CREATE INDEX ix_alert_timeline_alert ON dokpod.alert_timeline (alert_id, occurred_at_utc);

CREATE TABLE dokpod.action_justification_policies (
    stage character varying(16) NOT NULL CONSTRAINT pk_action_justification_policies PRIMARY KEY,
    required boolean NOT NULL,
    updated_by character varying(255),
    updated_at_utc timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT ck_action_justification_policies_stage CHECK (stage IN ('production', 'staging', 'development'))
);

CREATE TABLE dokpod.container_action_justifications (
    justification_id uuid NOT NULL CONSTRAINT pk_container_action_justifications PRIMARY KEY,
    command_id uuid NOT NULL CONSTRAINT ux_container_action_justifications_command UNIQUE,
    environment_id uuid NOT NULL,
    container_id character varying(128) NOT NULL,
    container_name character varying(255),
    action character varying(16) NOT NULL,
    actor character varying(255) NOT NULL,
    reason character varying(500) NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    ended_at_utc timestamp with time zone,
    CONSTRAINT ck_container_action_justifications_action CHECK (action IN ('stop', 'pause', 'delete'))
);

CREATE INDEX ix_container_action_justifications_container
    ON dokpod.container_action_justifications (environment_id, container_id, created_at_utc DESC);
CREATE INDEX ix_container_action_justifications_environment
    ON dokpod.container_action_justifications (environment_id, created_at_utc DESC);

-- Regras padrão (FR-009), ids fixos.
INSERT INTO dokpod.alert_rules (rule_id, name, description, condition_kind, event_type, metric, threshold_percent, duration_seconds, min_occurrences, severity, built_in)
VALUES
    ('01600000-0000-4000-8000-000000000001', 'Container encerrado com erro', 'Encerramento com código diferente de 0, 137 e 143, sem ação justificada.', 'event', 'container.died', NULL, NULL, 0, 1, 'high', true),
    ('01600000-0000-4000-8000-000000000002', 'Container encerrado por falta de memória', 'O engine encerrou o container por OOM.', 'event', 'container.oom_killed', NULL, NULL, 0, 1, 'critical', true),
    ('01600000-0000-4000-8000-000000000003', 'Container sem saúde', 'O health check do container falhou.', 'event', 'container.unhealthy', NULL, NULL, 0, 1, 'medium', true),
    ('01600000-0000-4000-8000-000000000004', 'Reinícios repetidos', '3 ou mais reinícios automáticos em 10 minutos.', 'event', 'container.auto_restarted', NULL, NULL, 600, 3, 'high', true),
    ('01600000-0000-4000-8000-000000000005', 'Agente desconectado', 'Agente sem conexão por mais de 2 minutos.', 'event', 'agent.disconnected', NULL, NULL, 120, 1, 'high', true),
    ('01600000-0000-4000-8000-000000000006', 'CPU do host acima de 80%', 'Uso de CPU do host acima de 80% por 5 minutos.', 'metric', NULL, 'host_cpu', 80, 300, 1, 'medium', true),
    ('01600000-0000-4000-8000-000000000007', 'Memória do host acima de 80%', 'Uso de memória do host acima de 80% por 5 minutos.', 'metric', NULL, 'host_memory', 80, 300, 1, 'medium', true),
    ('01600000-0000-4000-8000-000000000008', 'Comando com falha', 'Um comando enviado pelo Dokpod falhou no host.', 'event', 'command.failed', NULL, NULL, 0, 1, 'medium', true),
    ('01600000-0000-4000-8000-000000000009', 'Comando com resultado indeterminado', 'O resultado de um comando não pôde ser confirmado.', 'event', 'command.indeterminate', NULL, NULL, 0, 1, 'low', true);

-- FR-014: justificativa exigida em Produção por padrão.
INSERT INTO dokpod.action_justification_policies (stage, required) VALUES ('production', true), ('staging', false), ('development', false);

GRANT SELECT, INSERT, UPDATE, DELETE ON
    dokpod.alert_rules, dokpod.alert_silences, dokpod.alerts, dokpod.alert_timeline,
    dokpod.action_justification_policies, dokpod.container_action_justifications
    TO dokpod_runtime;
