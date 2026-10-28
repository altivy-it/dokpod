# Verificação de Bug: provisionamento UMA falha depois de 100 policies (spec 005, A1)

- **Slug**: spec005-a1-paginacao-policies
- **Testado em**: 2026-10-28
- **Avaliação**: ./assessment.md
- **Correção**: ./fix.md
- **Resultado**: verified

## Resumo

Na VM isolada da spec 005 (Keycloak 26.7.0, realm `dokpod`, 101 ambientes concedidos a `/dokpod/administrators`), o script original e o corrigido rodaram para o ambiente "VM Podman real", o último a ser criado, com o scope `environment:read`. O PowerShell 7.5 rodou em container no namespace de rede do Keycloak, com `-BaseUrl http://localhost:8080`.

## Verificações Executadas

| Verificação | Comando / Ação | Resultado | Notas |
| --- | --- | --- | --- |
| Script original | `provision-environment-authorization.ps1 -EnvironmentId <VM Podman real> -GroupPath /dokpod/administrators -Scope environment:read` | fail (reproduzido) | `Find-One` lança "deve resolver exatamente um resultado" para a policy existente |
| Script corrigido | mesmo comando | pass | "Policy UMA já existe", "Permission UMA atualizada", código 0 |
| Idempotência | script corrigido de novo | pass | mesmo resultado, sem duplicar policy nem permission |
| Outras listagens de authz em `tools/` | busca no repositório | pass | só `retire-load-hosts.sh`, que já usa `max=-1` |

## Riscos Residuais

- O caminho de criação de policy nova (ambiente sem policy) não mudou e não foi exercitado nesta verificação.
