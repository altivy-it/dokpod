# Verificação de Bug: provisionamento UMA não encontra policy ou permission existente

- **Slug**: provisionamento-uma-policy-existente
- **Testado em**: 2026-11-24
- **Avaliação**: ./assessment.md
- **Correção**: ./fix.md
- **Resultado**: verified

## Resumo

O defeito foi reproduzido duas vezes: num Keycloak falso que recusa a policy duplicada com `403`, como no quality, e
num Keycloak 26.7.0 real e descartável, onde a busca pela policy também falha e o script antigo só passa porque o
`409` cai no `catch` de recuperação. Com a correção, a busca acha a policy e a permission existentes nos dois casos, e
a suíte do instalador não regrediu.

## Verificações Executadas

| Verificação | Comando / Ação | Resultado | Notas |
|-------|------------------|--------|-------|
| Reprodução com Keycloak falso (`403` ao duplicado) | `ProvisionAuthorization.Tests.ps1` contra o script da `main` | pass | 5 falhas esperadas: os 2 cenários de policy existente com `403` em `POST .../policy/group`, e os 3 grupos com hífen ou dígito recusados |
| Correção com Keycloak falso | `ProvisionAuthorization.Tests.ps1` contra o script corrigido | pass | 12/12 |
| Causa no Keycloak real | `Invoke-RestMethod` dentro de função contra `policy?name=...&exact=true` do Keycloak 26.7.0, PowerShell 7.6.9 | pass | `Count=1 tipo0=Object[]`: a busca do script antigo não acha a policy |
| Código do duplicado no Keycloak real | `POST /policy/group` com nome existente | pass | `409 Conflicting policy`; explica por que o instalador passava pela recuperação |
| Reprodução após correção no Keycloak real | `provision-environment-authorization.ps1`: 2 scopes para `/dokpod/administrators`, reexecução do mesmo scope e um ambiente deixado pelo script antigo | pass | "Policy UMA já existe", "Permission UMA criada" no scope novo e "Permission UMA atualizada" na reexecução |
| Grupo com hífen e dígito no Keycloak real | `-GroupPath /dokpod/plantao-2`, scopes `environment:read` e `change:override` | pass | policy `...-group-plantao-2` criada na primeira e reaproveitada na segunda |
| Suíte de regressão | `podman build --target test ...` e `podman run --rm` da imagem, como no CI | pass | `Tests Passed: 335, Failed: 0, Skipped: 69` |
| Lint | `Invoke-ScriptAnalyzer -Path deploy/installer/src -Recurse -Settings deploy/installer/PSScriptAnalyzerSettings.psd1 -EnableExit` | pass | sem achados |
| Teste real do primeiro ambiente | `Environments.Tests.ps1::Primeiro ambiente contra PostgreSQL, Keycloak e as imagens reais` | skipped | exige `DOKPOD_TEST_SHARED`, `DOKPOD_TEST_API_IMAGE` e engine; coberto em parte pelo Keycloak descartável acima |

O Keycloak descartável rodou em `127.0.0.1:18089` (`docker.io/lzocateli/keycloak:26.7.0`, `start-dev`), configurado
por `configure-keycloak.ps1 -Bootstrap -SkipUser` com credenciais geradas na hora. Container e credenciais foram
removidos no fim.

## Trechos de Saída

```text
função: Count=1 tipo0=Object[]
status=409
{"error":"Policy with name [...-group-administrators] already exists","error_description":"Conflicting policy"}
```

```text
Policy UMA já existe: environment-66666666-7777-4888-9999-aaaaaaaaaaaa-group-administrators
Permission UMA atualizada: environment-66666666-7777-4888-9999-aaaaaaaaaaaa-environment-read
```

```text
Tests Passed: 335, Failed: 0, Skipped: 69
```

## Riscos Residuais

- Não se sabe por que o Keycloak do quality respondeu `403`, e não `409`, ao duplicado. A correção não depende disso, porque deixa de fazer o `POST` duplicado.
- A correção não foi executada no Keycloak do quality; chega lá com a próxima release do instalador.

## Recomendação

Encerre o bug: verificado com o Keycloak falso, que reproduz o `403` do quality, e com um Keycloak 26.7.0 real, sem
regressão na suíte do instalador. Publique na próxima release do instalador.
