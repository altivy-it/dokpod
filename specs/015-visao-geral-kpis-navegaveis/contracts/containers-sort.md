# Contrato: ordenação por consumo em `GET /api/v1/containers`

Mudança aditiva em `contracts/openapi/dokpod-control-plane.v1.yaml`.

## Parâmetro `sort`

Enum atual: `name`, `-name`, `displayStatus`, `environment`, `createdAt`, `-createdAt`.

Valores novos:

| Valor | Ordem |
| --- | --- |
| `cpu` | CPU crescente; sem amostra no fim |
| `-cpu` | CPU decrescente; sem amostra no fim |
| `memory` | memória usada crescente; sem amostra no fim |
| `-memory` | memória usada decrescente; sem amostra no fim |

Valor fora do enum continua respondendo `400 invalid_request`. `nextCursor` mantém o formato opaco e só vale com o mesmo `sort` e filtros.

## `GET /api/v1/metrics/overview`

Forma inalterada. Semântica de `current` muda: `cpuPercent` e `memoryPercent` passam a ser calculados pela soma dos containers com amostra recente sobre a capacidade dos hosts (spec 015 D-005); `hostsSampled` conta hosts com amostra recente de containers. `series` continua sendo a série do host.

## Compatibilidade

Clientes antigos não usam os valores novos e continuam funcionando. A mudança de semântica de `current` é visível só nos números dos cards.
