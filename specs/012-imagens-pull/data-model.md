# Modelo de Dados: Imagens e Pull

## inventory_images

| Coluna | Tipo | Regra |
| --- | --- | --- |
| `environment_id` | uuid | PK com `image_id` |
| `image_id` | text | `sha256:...` |
| `repo_tags` | text[] | ≤ 64 |
| `repo_digests` | text[] | ≤ 64 |
| `size_bytes` | bigint | ≥ 0 |
| `created_at` | timestamptz null | — |
| `architecture` / `os` | text null | — |
| `observed_at` | timestamptz | — |
| `revision` | bigint | revisão da projeção de imagens do host |

"Em uso" = existe container projetado no mesmo host com `image_id` igual (D-006; cálculo na consulta; índice em `inventory_containers(environment_id, image_id)`).

## inventory_containers (alteração)

| Coluna | Tipo | Regra |
| --- | --- | --- |
| `image_id` | text null | `sha256:...`; nulo para agente N-1 |

## agent_commands (alteração, migration expand)

- `container_id` e `expected_container_revision` passam a aceitar nulo.
- `CHECK ((kind BETWEEN 1 AND 6) = (container_id IS NOT NULL AND expected_container_revision IS NOT NULL))`.
- `ck_agent_commands_kind` ampliada para `kind BETWEEN 1 AND 7` (a 013 amplia para 8).
- Retenção aplicada por `dokpod_apply_temporal_retention` (D-008).

## agent_command_parameters

| Coluna | Tipo | Regra |
| --- | --- | --- |
| `environment_id`, `command_id` | uuid | PK `(environment_id, command_id, created_at_utc)`; FK composta para `agent_commands` |
| `created_at_utc` | timestamptz | igual ao do comando; chave de partição mensal (criada por `dokpod_ensure_monthly_partitions`) |
| `kind` | text | `pull_image` \| `create_container` (013) |
| `parameters` | jsonb | forma canônica não sensível |

Colunas de valores protegidos e purga (`protected_values`, `purged_at`) são adicionadas pela 013. Retenção igual à de `agent_commands` (D-008).

## Domínio

- `ImageReference`: registro, porta, repositório, tag, digest; validação conforme gramática de referência OCI; `IsMutableTag`.
- `AgentCommandKind.PullImage`; alvo `HostTarget` (ao lado do alvo de container existente); hash `v2` (D-001).
- Resultado: `resolvedDigest`, `sizeBytes`, falhas `image_not_found`, `registry_unauthorized`, `registry_unreachable`, `registry_not_allowed`, `insufficient_storage`, `platform_mismatch`, `invalid_reference`, `deadline_exceeded`.
- Configuração: `Images:AllowedRegistriesByStage` (opcional; vazio = qualquer registro), `Commands:Retention` 90 dias.
