# Modelo de Dados: Criação de Container

## agent_command_parameters (alteração da 012)

| Coluna | Tipo | Regra |
| --- | --- | --- |
| `parameters` | jsonb | especificação sem valores sensíveis (nomes, referências) |
| `protected_values` | bytea null | valores literais de variáveis cifrados (Data Protection) |
| `purged_at` | timestamptz null | preenchido pela purga após estado terminal |

## agent_commands (alteração da 012)

- `ck_agent_commands_kind` ampliada de `BETWEEN 1 AND 7` para `BETWEEN 1 AND 8` (migration expand, com rollback testado).
- O `CHECK` de alvo da 012 (`(kind BETWEEN 1 AND 6) = (container_id IS NOT NULL AND expected_container_revision IS NOT NULL)`) continua válido: o kind 8 usa `HostTarget`, sem `container_id`; teste explícito para o kind 8.

## ContainerCreationSpec (domínio)

| Campo | Regra |
| --- | --- |
| `image` | `ImageReference` válida; registro permitido se configurado |
| `name` | `^[a-zA-Z0-9][a-zA-Z0-9_.-]{0,62}$` |
| `ports[]` | ≤ 32; `hostPort` 1-65535 (0 = aleatória), `containerPort` 1-65535, `protocol` tcp/udp, `hostIp` opcional IPv4/IPv6 |
| `environment[]` | ≤ 100; `name` `^[A-Za-z_][A-Za-z0-9_]{0,127}$`; `value` ≤ 32 KiB **ou** `secretRef {kind, name}`; `sensitive` bool |
| `volumes[]` | ≤ 10; `{type: volume\|bind, source, target, readOnly}`; bind só na allowlist do agente; a interface edita um volume no formato `origem:destino[:ro\|rw]` |
| `network` | nome de rede existente, opcional; somente API (sem campo na interface) |
| `cpuLimitCores` | 0,01-64 |
| `memoryLimitBytes` | 6 MiB-1 TiB |
| `restartPolicy` | `no` \| `on-failure` (+`maxRetries` ≤ 100) \| `always` \| `unless-stopped` |
| `healthcheck` | `test[]` ≤ 16 itens, `intervalSeconds` 1-3600, `timeoutSeconds` 1-600, `retries` 1-100, `startPeriodSeconds` 0-3600, `onFailure` `none` \| `restart` (`restart` só Podman); preset da interface: HTTP GET `/healthz` na primeira porta do container, 15 s, 3 tentativas, tempo limite 5 s |
| `autoRemove` | bool (incompatível com restartPolicy ≠ `no`; recusado se a imagem declarar `VOLUME` sem volume nomeado no destino) |
| `deadlineUtc` | padrão: agora + prazo de pull da 012 (15 min) + 2 min |

## Auditoria

`container.create.requested` (comando equivalente redigido, host, imagem, nome), `container.create.completed|failed|indeterminate` (id criado, falha).

## Resultado

`CommandResult.created_container_id` (novo campo), falhas: `name_conflict`, `port_conflict`, `privileged_port`, `mount_not_allowed`, `registry_not_allowed`, `secret_not_found`, `network_not_found`, `image_pull_failed`, `start_failed`, `invalid_spec`, `anonymous_volume_with_auto_remove`, `unsupported_option`, `parameters_unavailable`.

O campo `sensitive` é o nome canônico no contrato e no domínio; a interface o exibe como "segredo".
