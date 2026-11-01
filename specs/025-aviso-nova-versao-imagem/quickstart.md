# Validação: Aviso de nova versão de imagem

Cenários ponta a ponta para comprovar a feature depois da implementação. Tudo roda em containers. Navegador e cliente HTTP usam a borda nginx (`https://localhost:7443/dokpod/`).

## Pré-requisitos

- Imagem Business da API construída localmente (`Business.Dockerfile`) e licença com a capability `image-update-notice`. A chave de teste fica no trust store só do build local.
- Registro OCI de teste em container (`registry:2`) com TLS de uma CA de teste confiada pelo agente, e um `config.json` de credencial montado em `DOKPOD_AGENT_REGISTRY_AUTH_FILE`.
- Agentes Docker e Podman da stack E2E com a capability `CAPABILITY_IMAGE_UPDATE_CHECK`.
- Usuários do Keycloak com e sem `image:pull`.

## Cenário 1: aviso nas três telas (Jornada 1, SC-001)

1. Publicar `teste/web:1.0` no registro e criar um container com essa referência nos dois hosts.
2. Acionar "Verificar agora": em até 1 minuto, Imagens, Containers e o detalhe mostram "Atualizada" com a data.
3. Publicar outro conteúdo na mesma tag e acionar "Verificar agora": "Nova versão disponível".
4. Container por digest, imagem construída no host e registro fora da política: "Não verificável" com o motivo, e o log do registro mostra que não houve consulta.

## Cenário 2: plataforma (caso de borda)

Publicar um índice multiarquitetura em que só a variante `arm64` muda: o host `amd64` continua "Atualizada".

## Cenário 3: falhas sem falso "Atualizada" (SC-002)

Registro parado, credencial errada, `429` simulado e tag removida: "Falha na verificação" com o motivo e a última verificação boa. Nenhum desses casos mostra "Atualizada".

## Cenário 4: baixar a nova versão (Jornada 2)

1. Com `image:pull`, acionar "Baixar nova versão": o pull da spec 012 acontece e é auditado. A imagem fica "Atualizada", e o container, "Versão nova no host; o container usa a anterior".
2. Sem `image:pull`, a ação não aparece, e `POST …/images/update-check` responde `403`.

## Cenário 5: alerta (Jornada 3)

Regra "imagem desatualizada há mais de 1 dia" em Produção, com relógio injetável nos testes de integração: o alerta abre e, depois do container recriado com a imagem nova e de uma nova verificação, resolve.

## Cenário 6: licença e composição (FR-008, FR-009)

1. Licença restrita: a verificação periódica continua, o estado segue visível e regras existentes continuam avaliadas. "Verificar agora" e criar ou editar a regra respondem `403 license_expired`; desativar e excluir a regra funcionam.
2. Licença sem a capability: o worker não agenda e o campo `updateCheck` some.
3. Imagem CE: nenhum campo, nenhuma rota e regra com a condição recusada com `422`.

## Cenário 7: segredos e desempenho (SC-003, SC-004)

1. Canário no `config.json` do host: ausente de respostas, banco, logs da API e do agente e telemetria.
2. Com 200 imagens verificadas em um host, o p95 da primeira página de Imagens e de Containers fica no máximo 10% acima do p95 sem dados de verificação.

## Comandos

```bash
dotnet test backend/tests/Dokpod.Agent.Application.Tests --filter ImageUpdate
dotnet test backend/tests/Dokpod.Agent.Infrastructure.Tests --filter ImageUpdate          # registro OCI real em container
dotnet test backend/tests/Dokpod.ControlPlane.Api.Tests --filter ImageUpdate              # PostgreSQL real
dotnet test backend/tests/Dokpod.Architecture.Tests --filter BusinessGate
frontend/tests/e2e/mock/run-mock-e2e.sh -- -k test_025
```
