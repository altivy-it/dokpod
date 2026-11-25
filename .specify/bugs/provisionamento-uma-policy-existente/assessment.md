# Avaliação de Bug: provisionamento UMA não encontra policy ou permission existente

- **Slug**: provisionamento-uma-policy-existente
- **Criada em**: 2026-11-24
- **Origem**: texto colado; evidência em `specs/026-janelas-de-mudanca/validation.md` (achados 1 e 5)
- **Veredito**: valid
- **Severidade**: medium

## Relato (literal ou resumido)

> O script tools/scripts/provision-environment-authorization.ps1 (publicado no instalador 0.12.0) falha com 403 ao
> conceder um segundo scope ao mesmo grupo ou usuário: no PowerShell 7.6.9 da imagem, Invoke-RestMethod como última
> instrução da função emite o array JSON como um único objeto, a busca da policy/permission pelo nome não encontra a
> existente e o script tenta recriá-la; o Keycloak recusa (403). Evidência: T038 da spec 026 no quality em 2026-11-24
> (validation.md, achado 1), contornado com uma cópia que atribui o resultado a uma variável antes de devolvê-lo.
> Achado relacionado do mesmo script: grupos só aceitos em ^/dokpod/[a-z]+$ (achado 5). Autorizado pelo solicitante
> em 2026-11-24 ("Bug Fixing: sim").

## Sintoma

Ao conceder um scope a um grupo ou usuário que já tem a policy `environment-<id>-group-<nome>` (ou `-owner-<usuário>`),
o script não reconhece a policy existente, tenta criá-la de novo e termina com erro da API UMA. O esperado é reutilizar
a policy e criar ou atualizar a permission do scope. O mesmo defeito atinge a busca da permission pelo nome.

## Reprodução

1. Com um ambiente registrado, rodar `provision-environment-authorization.ps1 -EnvironmentId <id> -GroupPath /dokpod/plantao -Scope environment:read` (cria recurso, policy e permission).
2. Rodar de novo para o mesmo grupo com outro scope, por exemplo `-Scope change:override`.
3. A busca da policy devolve zero itens, o `POST /policy/group` é recusado e o script lança `Falha na API UMA em POST ...`.

Reprodução isolada da causa (2026-11-24), na imagem `localhost/dokpod-installer:test` (PowerShell 7.6.9) contra um
`HttpListener` que devolve `[{"id":"p1","name":"environment-x-group-administrators"}]`:

```text
PS 7.6.9
atual:    Count=1 tipo0=Object[] achou=0
corrigido: Count=1 tipo0=PSCustomObject achou=1
recuperação 409 atual: 1 item, id=p1
```

Na T038 a recusa observada foi `403`, e não `409`. [NEEDS CLARIFICATION: o motivo de o Keycloak do quality responder
`403`, e não `409`, ao nome duplicado não foi investigado; não muda a correção, que evita o `POST` duplicado.]

## Caminhos de Código Suspeitos

- `tools/scripts/provision-environment-authorization.ps1:171` (`Invoke-Api`): `try { Invoke-RestMethod @parameters }` emite a resposta JSON em array como um único `Object[]`; `@(Invoke-Api ...)` vira um array de um elemento que é o próprio array.
- `tools/scripts/provision-environment-authorization.ps1:240-251`: o filtro `$_.PSObject.Properties.Name -contains 'name'` é avaliado sobre o `Object[]`, que não tem a propriedade `name`; nenhuma policy é encontrada e o script faz `POST`.
- `tools/scripts/provision-environment-authorization.ps1:255-256` e `:281-284`: a mesma busca para a permission; no ramo de `409`, `$_.name -eq $permissionName` só funciona por acaso (enumeração de membros sobre o array).
- `tools/scripts/provision-environment-authorization.ps1:76`: `[ValidatePattern('^/dokpod/[a-z]+$')]` recusa grupos com hífen ou dígito (achado 5).
- Buscas de client, scope, recurso, usuário e grupos já desenrolam com `ForEach-Object { $_ }` ou passam o resultado direto a `Find-One` (parâmetro `[object[]]`), por isso funcionam.

## Hipótese de Causa Raiz

Desde o PowerShell 6, `Invoke-RestMethod` escreve um array JSON no pipeline como um único objeto, sem enumerar. Como
`Invoke-Api` devolve a chamada diretamente, o chamador recebe um `Object[]` aninhado. As buscas da policy e da
permission não desenrolam esse resultado e procuram a propriedade `name` no array, que não existe. O caminho do
instalador (`Environments.Sidecar.ps1`, 17 scopes para `/dokpod/administrators`) só passa quando o Keycloak responde
`409` ao nome duplicado: a recuperação do `catch` acha a policy por enumeração de membros. Quando a recusa vem com
outro código, como o `403` da T038, o script falha. Confiança: high (reproduzido na versão da imagem).

## Correção Proposta

**Preferencial**: em `Invoke-Api`, atribuir a resposta a uma variável e devolvê-la (`$response = Invoke-RestMethod
@parameters; $response`), o que enumera os arrays para todos os chamadores; é o mesmo contorno validado na T038. Revisar
os chamadores que hoje desenrolam com `ForEach-Object { $_ }` ou `@(@(...))` para confirmar que continuam corretos com
a resposta já enumerada (devem continuar; o desenrolar extra fica inócuo). Na mesma mudança, aceitar grupos com dígito
e hífen no último segmento: `^/dokpod/[a-z0-9]+(?:-[a-z0-9]+)*$`, nome que entra na policy
`environment-<id>-group-<segmento>`.

**Alternativas**:
- Desenrolar em cada busca de policy e permission, sem mexer em `Invoke-Api`: corrige o sintoma, mas deixa o mesmo erro latente para qualquer busca futura.
- Trocar por `Invoke-WebRequest` e `ConvertFrom-Json`, que enumera arrays: mais código e o mesmo resultado.

**Arquivos Prováveis de Alteração**:
- `tools/scripts/provision-environment-authorization.ps1`
- `deploy/installer/tests/Environments.Tests.ps1` (ou um novo teste Pester ao lado, que o instalador já executa)
- `docs/configuracao-keycloak.md` (formato aceito de `-GroupPath`, se a regex mudar)

**Testes a Adicionar ou Atualizar**:
- Teste Pester com servidor HTTP falso (`HttpListener`) simulando o Admin API: segunda execução para o mesmo grupo com outro scope reutiliza a policy, não faz `POST /policy/group` e cria a permission do scope novo; reexecução do mesmo scope atualiza a permission sem `POST`; resposta não `409` ao duplicado não ocorre porque não há `POST`.
- Caso de grupo com hífen e dígito (`/dokpod/operador-teste2`) aceito, e caso inválido (`/dokpod/Admins`, `/outro/x`) recusado pela validação.
- Rodar o teste real existente `Primeiro ambiente contra PostgreSQL, Keycloak e as imagens reais` quando o ambiente estiver disponível.

## Riscos e Considerações

- O script é publicado no instalador (`deploy/installer/Dockerfile:57`); a correção só chega aos operadores numa nova release do instalador.
- Alterar `Invoke-Api` muda a forma de todas as respostas; o `Find-One` e os ramos `Update-Permission` precisam continuar recebendo um item por objeto. O teste com servidor falso deve cobrir esses caminhos.
- Ampliar a regex muda os nomes de policy possíveis; os nomes continuam compatíveis com o Keycloak e não afetam policies existentes. Hífen e dígito não abrem caminho para injeção porque o segmento só entra em nome e em query string escapada.
- Sem impacto em contrato HTTP, schema ou protocolo do agente.

## Perguntas em Aberto

- [NEEDS CLARIFICATION: por que o Keycloak do quality respondeu `403` e não `409` ao `POST` da policy duplicada. Não bloqueia a correção.]
