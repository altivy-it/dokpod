UPDATE runs SET finished_at = $now, outcome = $outcome, exit_code = $exit_code, report_path = $report_path WHERE run_id = $run_id;
