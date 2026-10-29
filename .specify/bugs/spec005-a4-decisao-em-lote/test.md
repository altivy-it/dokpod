# Verificação de Bug: leituras do console fazem uma decisão UMA por ambiente (spec 005, A4)

- **Slug**: spec005-a4-decisao-em-lote
- **Testado em**: 2026-10-29
- **Avaliação**: ./assessment.md
- **Correção**: ./fix.md
- **Resultado**: partial

## Resumo

Suíte completa no container `lzocateli/dotnet-sdk:10.0.402-noble-v1` com PostgreSQL 17.6 local e migrations aplicadas (mesma configuração do CI), sem testes ignorados. A medição do número de chamadas ao Keycloak num trace real fica para o deploy da próxima release na VM `quality`, que roda a `v0.1.0`.

## Verificações Executadas

| Verificação | Comando / Ação | Resultado | Notas |
| --- | --- | --- | --- |
| Filtro em lote | `EnvironmentGrantsTests` | pass | 101 hosts com uma consulta por scope; provedor nulo ou com exceção = nenhum host; sem candidatos, nenhuma chamada |
| Alertas e auditoria com PostgreSQL | `AlertGovernanceServiceTests`, `AuditConsoleIntegrationTests` | pass | mesmas expectativas de antes, com os fakes respondendo também como leitor de grants |
| Registries | `RegistryCatalogTests` | pass | lista em lote; salvar negado continua `Forbidden` pelo decisor |
| Suíte do backend | `dotnet test Dokpod.slnx --configuration Release` | pass | 710 testes, 0 falhas, 0 ignorados |
| Trace na VM | console com 101 ambientes, contagem de chamadas `uma-ticket` | not run | depende do deploy da próxima release em `quality` |

## Riscos Residuais

- Sem a medição em trace real, a redução (103 → uma consulta por leitura) está comprovada só nos testes.
