DROP FUNCTION IF EXISTS dokpod.dokpod_append_audit_event(
    uuid, timestamptz, uuid, smallint, varchar, varchar, uuid, uuid, smallint, varchar, bytea, varchar, jsonb);
DROP INDEX IF EXISTS dokpod.ix_audit_events_environment_subject_time;
ALTER TABLE dokpod.audit_events
    DROP CONSTRAINT IF EXISTS ck_audit_events_details_size,
    DROP COLUMN IF EXISTS details,
    DROP COLUMN IF EXISTS subject_id;
