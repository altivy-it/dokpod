INSERT INTO certificates (role, origin, subject_alt_names_json, not_after, fingerprint_sha256, secret_ref, ca_state, updated_at)
VALUES ($role, $origin, $subject_alt_names_json, $not_after, $fingerprint_sha256, $secret_ref, $ca_state, $now)
ON CONFLICT (role) DO UPDATE SET
    origin = excluded.origin, subject_alt_names_json = excluded.subject_alt_names_json, not_after = excluded.not_after,
    fingerprint_sha256 = excluded.fingerprint_sha256, secret_ref = excluded.secret_ref, ca_state = excluded.ca_state,
    updated_at = excluded.updated_at;
