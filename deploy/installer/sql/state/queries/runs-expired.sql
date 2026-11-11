-- Execuções além das $keep mais recentes, exceto as que registraram desvio de SQL (sempre preservadas).
SELECT run_id, report_path FROM runs
WHERE run_id NOT IN (SELECT run_id FROM runs ORDER BY started_at DESC, run_id DESC LIMIT $keep)
  AND run_id NOT IN (SELECT run_id FROM sql_overrides);
