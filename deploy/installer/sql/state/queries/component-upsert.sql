INSERT INTO components (name, origin, desired_hash, observed_json, recreatable, updated_at)
VALUES ($name, $origin, $desired_hash, $observed_json, $recreatable, $now)
ON CONFLICT (name) DO UPDATE SET
    origin = excluded.origin, desired_hash = excluded.desired_hash, observed_json = excluded.observed_json,
    recreatable = excluded.recreatable, updated_at = excluded.updated_at;
