# Correção de Bug: leituras do console fazem uma decisão UMA por ambiente (spec 005, A4)

- **Slug**: spec005-a4-decisao-em-lote
- **Corrigido em**: 2026-10-29
- **Avaliação**: ./assessment.md
- **Estado**: applied

## Resumo

As leituras que filtram ambientes por scope passam a usar uma só consulta de grants (`response_mode=permissions`) em vez de uma decisão UMA por ambiente. Ações continuam decididas uma a uma.

## Alterações

| Arquivo | Alteração | Notas |
| --- | --- | --- |
| `backend/libs/Dokpod.ControlPlane.Application/Authorization/EnvironmentGrants.cs` | modificado | `WithScopeAsync`: uma consulta, filtra pelo scope; sem resposta ou com exceção, nenhum host; sem candidatos, nenhuma chamada |
| `backend/libs/Dokpod.ControlPlane.Application/Auditing/AuditQueryService.cs` | modificado | `ReadableAsync` em lote; o serviço recebe `IEnvironmentGrantReader` no lugar do decisor |
| `backend/libs/Dokpod.ControlPlane.Application/Alerts/AlertGovernanceService.cs` | modificado | `ReadableWithAsync` para resumo, lista, regras, silenciamentos e políticas; `FilterAsync` (decisão por host) mantido para detalhe e ações |
| `backend/libs/Dokpod.ControlPlane.Application/Registries/RegistryPolicyService.cs` | modificado | lista em lote; salvar continua com `ManagedAsync` |
| `backend/apps/Dokpod.ControlPlane.Api/Environments/SettingsEndpoints.cs` | modificado | `AuthorizeAsync` em lote (acesso, instalação e parâmetros são leituras) |

## Testes Adicionados ou Atualizados

- `backend/tests/Dokpod.ControlPlane.Application.Tests/Authorization/EnvironmentGrantsTests.cs` (novo).
- Fakes de `AlertGovernanceServiceTests`, `AuditConsoleIntegrationTests`, `RegistryCatalogTests` e `HostCommandFakes`.

## Verificação Local

- Ver `test.md`.

## Desvios da Avaliação

- Nenhum.

## Acompanhamento

- Medir o trace na VM `quality` no deploy da próxima release e atualizar a linha do A4 na spec 005.
