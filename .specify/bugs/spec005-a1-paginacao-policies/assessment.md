# Avaliação de Bug: provisionamento UMA falha depois de 100 policies (spec 005, A1)

- **Slug**: spec005-a1-paginacao-policies
- **Criada em**: 2026-10-28
- **Origem**: achado A1 de [specs/005 plan.md](../../../specs/005-qualificacao-release-docker-linux/plan.md#achados-da-vm) e ordem de pendências aprovada pelo solicitante em 2026-10-28 ("siga a ordem")
- **Veredito**: valid
- **Severidade**: medium

## Relato (literal ou resumido)

Na preparação da VM isolada da spec 005, a concessão dos 17 scopes aos 101 ambientes falhou em 1.253 de 1.717 chamadas de `tools/scripts/provision-environment-authorization.ps1`: a partir de certo ponto, o script não encontrava a policy que ele mesmo tinha criado.

## Sintoma

`Find-One` lança "policy environment-<id>-group-<grupo> deve resolver exatamente um resultado; encontrados 0." para ambientes cuja policy já existe, e a permission do scope não é criada nem atualizada.

## Reprodução

1. Realm com mais de 100 policies no resource server `dokpod-api` (101 ambientes concedidos a `/dokpod/administrators`).
2. Rodar o script para um ambiente cuja policy está além das 100 primeiras, por exemplo `-EnvironmentId <id> -GroupPath /dokpod/administrators -Scope environment:read`.
3. O `GET .../policy` sem filtro devolve 100 policies, a policy do ambiente não está entre elas, o `POST` recebe `409` e o fallback repete o mesmo `GET` sem filtro: 0 resultados e erro.

## Caminhos de Código Suspeitos

- `tools/scripts/provision-environment-authorization.ps1`, busca da policy (`$policyResponse = @(Invoke-Api GET "$authzBase/policy")`) e o fallback depois do `409`. Recurso e permission já filtram por nome; só a policy não.

## Hipótese de Causa Raiz

Confiança: high. O endpoint de policies do Keycloak pagina por padrão (`max` = 100) quando não recebe `first`/`max`. O script pressupunha a lista completa.

## Correção Proposta

**Preferencial**: buscar a policy por `name=<nome>&exact=true`, como a permission já faz, no caminho normal e no fallback do `409`, mantendo o filtro exato no cliente.

**Alternativas**:

- `first=0&max=-1` sem filtro: traz todas as policies a cada chamada, custo que cresce com o número de ambientes.
- Paginar até achar: mais código para o mesmo resultado.

**Arquivos Prováveis de Alteração**:

- `tools/scripts/provision-environment-authorization.ps1`

**Testes a Adicionar ou Atualizar**:

- Não há suíte para os scripts de Keycloak; a verificação é contra um Keycloak real com mais de 100 policies (VM da spec 005), comparando o script original e o corrigido.

## Riscos e Considerações

- Outra listagem de authz em `tools/` (`tools/loadtest/retire-load-hosts.sh`) já usa `max=-1`. Nenhuma outra busca de policy sem filtro.
- O filtro `name` do Keycloak é por semelhança; o `exact=true` e o `.Where` no cliente garantem o nome exato.

## Perguntas em Aberto

- Nenhuma.
