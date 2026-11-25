# Correção de Bug: provisionamento UMA não encontra policy ou permission existente

- **Slug**: provisionamento-uma-policy-existente
- **Corrigido em**: 2026-11-24
- **Avaliação**: ./assessment.md
- **Estado**: applied

## Resumo

`Invoke-Api` passou a devolver a resposta do `Invoke-RestMethod` por uma variável, o que enumera os arrays do Admin
API. Com isso as buscas da policy e da permission pelo nome encontram a existente, e a segunda concessão ao mesmo grupo
ou usuário deixa de tentar recriar a policy. Na mesma mudança, `-GroupPath` passou a aceitar dígitos e hífens no
último segmento.

## Alterações

| Arquivo | Alteração | Notas |
|------|--------|-------|
| `tools/scripts/provision-environment-authorization.ps1` | modificado | `Invoke-Api` enumera a resposta; `Find-One` ignora nulos; `ValidatePattern` de `-GroupPath` e ajuda do parâmetro |
| `deploy/installer/tests/ProvisionAuthorization.Tests.ps1` | teste adicionado | Admin API falso do Keycloak em `HttpListener`, no estágio de testes da imagem do instalador |
| `docs/configuracao-keycloak.md` | modificado | formato aceito de `-GroupPath` e reaproveitamento da policy entre scopes |

## Destaques do Diff (opcional)

```powershell
# Invoke-RestMethod escreve o array JSON como um único objeto; devolvê-lo pela variável enumera os itens.
try { $response = Invoke-RestMethod @parameters; $response }
```

```powershell
$matches = @($Items | Where-Object { $null -ne $_ })   # Find-One
[ValidatePattern('^/dokpod/[a-z0-9]+(?:-[a-z0-9]+)*$')]  # GroupPath
```

O filtro de nulos em `Find-One` acompanha a enumeração: uma lista vazia do Keycloak agora chega como `$null`, e sem o
filtro `@($null)` contaria um item. O erro continua "encontrados 0".

## Testes Adicionados ou Atualizados

- `ProvisionAuthorization.Tests.ps1::o segundo scope para o mesmo grupo reaproveita a policy e cria só a permission nova` — um único `POST /policy/group`, duas permissions e dois scopes no recurso; o Keycloak falso responde `403` ao nome duplicado, como na T038.
- `ProvisionAuthorization.Tests.ps1::a reexecução do mesmo scope atualiza a permission existente sem recriá-la` — sem `POST /permission/scope` novo, um `PUT` na permission existente.
- `ProvisionAuthorization.Tests.ps1::lista vazia do Keycloak continua falhando como zero resultados` — protege o ajuste do `Find-One`.
- `ProvisionAuthorization.Tests.ps1::aceita dígitos e hífens entre letras` (3 casos) e `recusa <grupo>` (6 casos: hífen no início ou no fim, hífen duplo, sublinhado, subgrupo e outra raiz).

## Verificação Local

- `podman build --format docker --target test -t localhost/dokpod-installer:pester-bugfix -f deploy/installer/Dockerfile .` → imagem do estágio de testes (PowerShell 7.6.9, Pester 6.2.0).
- Teste novo contra o script corrigido → `Tests Passed: 12, Failed: 0`.
- Teste novo contra o script da `main` (antes da correção) → `Tests Passed: 7, Failed: 5`: os dois cenários de policy existente falham com `Falha na API UMA em POST .../policy/group: ... 403 (Forbidden)`, o mesmo erro da T038, e os três grupos com hífen ou dígito são recusados.
- Suíte inteira do instalador, como no CI (`podman run --rm localhost/dokpod-installer:pester-bugfix`) → `Tests Passed: 335, Failed: 0, Skipped: 69` (os pulados exigem engine, PostgreSQL e Keycloak reais).
- `Invoke-ScriptAnalyzer -Path deploy/installer/src -Recurse -Settings deploy/installer/PSScriptAnalyzerSettings.psd1 -EnableExit` → sem achados.
- Gitleaks 8.30.1 com `.gitleaks.toml` sobre os três arquivos alterados → `no leaks found`.

## Desvios da Avaliação

- O filtro de nulos em `Find-One` não constava da proposta; é consequência direta da enumeração em `Invoke-Api` e tem teste próprio.
- O caso `/dokpod/Admins` previsto como recusado na avaliação foi retirado: `ValidatePattern` não diferencia maiúsculas, e a regex anterior já aceitava maiúsculas. Mudar isso foge do escopo do defeito.

## Acompanhamento

- A correção só chega aos operadores numa nova release do instalador (o script é copiado para `/opt/dokpod-installer/keycloak`).
- Pendente da avaliação: o teste real `Primeiro ambiente contra PostgreSQL, Keycloak e as imagens reais` (pulado sem engine) e a reexecução contra o Keycloak do quality; ficam para `/speckit-bug-test`.
- O PSScriptAnalyzer aponta `PSAvoidAssignmentToAutomaticVariable` para `$matches` em `Find-One`; o nome é anterior à correção e o CI só analisa `deploy/installer/src`.
- Em aberto da avaliação: por que o Keycloak do quality responde `403`, e não `409`, ao nome duplicado. Deixou de importar, porque a correção não faz mais o `POST` duplicado.
