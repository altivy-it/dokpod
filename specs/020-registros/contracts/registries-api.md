# Contrato: API de Registros

## `GET /api/v1/registries`

Query: `stage`, `environmentId[]`, `origin[]` (`images`, `mirror`, `insecure`, `search`, `blocked`, `credential`), `insecure` (bool), `outsidePolicy` (bool), `q` (2-128), `sort` (`registry`, `-registry`, `images`, `-images`, `hosts`, `-hosts`), `cursor`, `limit` (1-200).

Resposta: `{ registries: RegistryItem[], summary: { total, insecure, outsidePolicy, unavailableHosts[] }, nextCursor }`.

`RegistryItem`: `registry`, `hosts`, `origins[]`, `images`, `sizeBytes`, `insecureHosts`, `credentialHosts`, `blockedHosts`, `outsidePolicyHosts`, `mirrorOf`.

## `GET /api/v1/registries/{registry}`

`{ registry, hosts: [{ environmentId, environmentName, stage, hostConnected, configAvailable, origins[], credential, insecure, blocked, search, mirrors[], mirrorOf, images, sizeBytes, allowed }] }`; 404 quando o registro não aparece em host legível.

## `GET /api/v1/registry-policies`

`{ policies: [{ stage, registries[], source: "configuration" | "console", updatedBy, updatedAt, canEdit }] }`.

## `GET /api/v1/registry-policies/{stage}/impact?registries=...`

`{ images, hosts }` do estágio cujos registros ficariam fora da lista informada (vazia = nenhum).

## `PUT /api/v1/registry-policies/{stage}`

Corpo `{ registries: string[] }` (até 100, gramática da spec 012, sem duplicados). 204; 400 `registry_invalid`; 403 `authorization_denied`; 503 `persistence_unavailable`.
