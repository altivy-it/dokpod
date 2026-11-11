INSERT INTO environments (environment_id, name, engine, agent_fingerprint, package_generated_at, revoked_at)
VALUES ($environment_id, $name, $engine, $agent_fingerprint, $package_generated_at, $revoked_at)
ON CONFLICT (environment_id) DO UPDATE SET
    name = excluded.name, engine = excluded.engine, agent_fingerprint = excluded.agent_fingerprint,
    package_generated_at = excluded.package_generated_at, revoked_at = excluded.revoked_at;
