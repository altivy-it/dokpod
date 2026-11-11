SELECT run_id, command, started_at, finished_at, outcome, exit_code, report_path FROM runs WHERE run_id = $run_id;
