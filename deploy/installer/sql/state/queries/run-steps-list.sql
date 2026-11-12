-- Ordem de gravação (rowid): duas etapas no mesmo milissegundo saem na ordem em que terminaram.
SELECT step, status, at FROM run_steps WHERE run_id = $run_id ORDER BY rowid;
