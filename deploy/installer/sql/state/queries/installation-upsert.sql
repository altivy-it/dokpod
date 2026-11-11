INSERT INTO installation (installation_id, version, edition, engine, public_url, created_at, updated_at)
VALUES ($installation_id, $version, $edition, $engine, $public_url, $now, $now)
ON CONFLICT (singleton) DO UPDATE SET
    version = excluded.version, edition = excluded.edition, engine = excluded.engine,
    public_url = excluded.public_url, updated_at = excluded.updated_at;
