SELECT step, status, at FROM run_steps WHERE run_id = $run_id ORDER BY at, step;
