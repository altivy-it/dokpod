-- Contract da spec 025: o código anterior lê qualquer condição diferente de 'event' como métrica, então as regras de
-- imagem desatualizada e os alertas abertos por elas saem (a linha do tempo vai junto pela cascata).
DELETE FROM dokpod.alerts WHERE rule_id IN (SELECT rule_id FROM dokpod.alert_rules WHERE condition_kind = 'image_outdated');
DELETE FROM dokpod.alert_rules WHERE condition_kind = 'image_outdated';
ALTER TABLE dokpod.alert_rules DROP CONSTRAINT ck_alert_rules_condition;
ALTER TABLE dokpod.alert_rules ADD CONSTRAINT ck_alert_rules_condition CHECK (
    (condition_kind = 'event' AND event_type IS NOT NULL AND metric IS NULL AND threshold_percent IS NULL)
    OR (condition_kind = 'metric' AND metric IN ('host_cpu', 'host_memory', 'container_cpu', 'container_memory')
        AND threshold_percent BETWEEN 1 AND 100 AND event_type IS NULL));
ALTER TABLE dokpod.alert_rules DROP COLUMN outdated_days;

DROP TABLE dokpod.image_update_checks;
DROP TABLE dokpod.image_update_schedules;
