DROP INDEX IF EXISTS dokpod.ix_audit_events_subject_pattern;
DROP INDEX IF EXISTS dokpod.ix_audit_events_search;
DROP FUNCTION IF EXISTS dokpod.dokpod_audit_search_query(text);
DROP FUNCTION IF EXISTS dokpod.dokpod_audit_search_document(varchar, varchar, varchar, varchar, varchar, jsonb);
DROP FUNCTION IF EXISTS dokpod.dokpod_fold_text(text);
