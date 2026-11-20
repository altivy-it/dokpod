# Modelo de Dados: Reconexão do agente com espera crescente e aleatória

Não há dados persistidos. O único estado é o da política de espera, na memória do processo do agente.

## AgentReconnectBackoff (estado em memória)

| Campo | Tipo | Regra |
| --- | --- | --- |
| `consecutiveFailures` | inteiro ≥ 0 | Começa em 0. Soma 1 a cada falha (exceção na sessão, sessão não estabelecida ou sessão com duração < 60 s). Volta a 0 depois de uma sessão com duração ≥ 60 s. |
| `random` | fonte aleatória | Injetada; `Random.Shared` no worker, semeada nos testes. |

### Constantes

| Nome | Valor | Origem |
| --- | --- | --- |
| `InitialDelay` | 1 s | FR-002 |
| `MaxDelay` | 30 s | FR-002, R2 |
| `StableSession` | 60 s | FR-004, R3 |

### Operações

- `NextDelay(sessionDuration)`: chamada depois de toda saída da sessão. Com `sessionDuration ≥ StableSession`, zera `consecutiveFailures`. Depois soma 1 e devolve `uniforme(limite/2, limite)`, com `limite = min(MaxDelay, InitialDelay × 2^(consecutiveFailures − 1))`. Também devolve o número da tentativa para o aviso (FR-006).

### Transições

```text
início ──(1ª tentativa imediata)──> conectando
conectando ──falha──> esperando(n+1)
conectando ──sessão estabelecida──> conectado
conectado ──queda com duração < 60 s──> esperando(n+1)
conectado ──queda com duração ≥ 60 s──> esperando(1)
esperando ──fim da espera──> conectando
qualquer ──stop do serviço──> encerrado (sem aguardar a espera)
```
