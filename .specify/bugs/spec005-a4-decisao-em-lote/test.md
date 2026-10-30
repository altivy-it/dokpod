# Verificação de Bug: leituras do console fazem uma decisão UMA por ambiente (spec 005, A4)

- **Slug**: spec005-a4-decisao-em-lote
- **Testado em**: 2026-10-29 (testes) e 2026-10-30 (trace na VM)
- **Avaliação**: ./assessment.md
- **Correção**: ./fix.md
- **Resultado**: pass

## Resumo

Suíte completa no container `lzocateli/dotnet-sdk:10.0.402-noble-v1` com PostgreSQL 17.6 local e migrations aplicadas (mesma configuração do CI), sem testes ignorados. Na VM `quality`, com 101 ambientes, as três leituras afetadas caíram de 103 para 3 chamadas ao Keycloak por requisição, medidas na mesma carga sobre a `v0.1.1` e, depois do rollback, sobre a `v0.1.0`.

## Verificações Executadas

| Verificação | Comando / Ação | Resultado | Notas |
| --- | --- | --- | --- |
| Filtro em lote | `EnvironmentGrantsTests` | pass | 101 hosts com uma consulta por scope; provedor nulo ou com exceção = nenhum host; sem candidatos, nenhuma chamada |
| Alertas e auditoria com PostgreSQL | `AlertGovernanceServiceTests`, `AuditConsoleIntegrationTests` | pass | mesmas expectativas de antes, com os fakes respondendo também como leitor de grants |
| Registries | `RegistryCatalogTests` | pass | lista em lote; salvar negado continua `Forbidden` pelo decisor |
| Suíte do backend | `dotnet test Dokpod.slnx --configuration Release` | pass | 710 testes, 0 falhas, 0 ignorados |
| Trace na VM, `v0.1.1` | `vusers.py` (1 usuário, 1 min, 27 leituras, 0 erros) pela borda; spans do Jaeger com `/protocol/openid-connect/token` por trace da API | pass | `alerts/summary`, `audit/summary` e `settings/access`: 3 chamadas; demais leituras: 1 ou 2, iguais à `v0.1.0` |
| Trace na VM, `v0.1.0` (controle) | mesma carga depois do rollback (28 leituras, 0 erros) | pass | as mesmas três rotas: 103 chamadas, como no trace `8c931ccb…` |

## Riscos Residuais

- Nenhum para o A4. As ações (silenciar, reconhecer, salvar) seguem com uma decisão por host, como planejado.
