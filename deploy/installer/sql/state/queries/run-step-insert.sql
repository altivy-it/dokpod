INSERT INTO run_steps (run_id, step, status, at) VALUES ($run_id, $step, $status, $now)
ON CONFLICT (run_id, step) DO UPDATE SET status = excluded.status, at = excluded.at;
