DELETE FROM run_lock WHERE id = 1 AND ($run_id IS NULL OR run_id = $run_id);
