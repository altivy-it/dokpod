-- Contract da spec 027: remove destinos, cursores e as colunas de ordem da trilha.
DROP TABLE dokpod.audit_export_cursors;
DROP TABLE dokpod.audit_export_destinations;
DROP INDEX dokpod.ix_audit_events_export_order;
ALTER TABLE dokpod.audit_events
    ALTER COLUMN export_seq DROP DEFAULT,
    ALTER COLUMN tx_id DROP DEFAULT;
ALTER TABLE dokpod.audit_events
    DROP COLUMN export_seq,
    DROP COLUMN tx_id;
DROP SEQUENCE IF EXISTS dokpod.audit_export_seq;
