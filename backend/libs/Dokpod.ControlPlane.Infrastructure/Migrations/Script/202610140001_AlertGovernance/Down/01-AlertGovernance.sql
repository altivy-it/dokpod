-- Reverte a spec 016 (remove regras, alertas, silenciamentos e justificativas).
DROP TABLE IF EXISTS dokpod.container_action_justifications;
DROP TABLE IF EXISTS dokpod.action_justification_policies;
DROP TABLE IF EXISTS dokpod.alert_timeline;
DROP TABLE IF EXISTS dokpod.alerts;
DROP TABLE IF EXISTS dokpod.alert_silences;
DROP TABLE IF EXISTS dokpod.alert_rules;
