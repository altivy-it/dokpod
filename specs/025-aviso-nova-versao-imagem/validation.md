# Validação: Aviso de nova versão de imagem

**Spec**: 025, tarefas T030 e T028 · **Data**: 2026-11-07; T028 em 2026-11-27 · **Origem**: IA assistida · **Estado**: evidências registradas; aguarda revisão humana

Execução dos sete cenários do [quickstart](quickstart.md). Onde o cenário pede a stack E2E Business com agentes reais,
a evidência vem de outra camada (registro OCI real em container, PostgreSQL 17.6 real e E2E simulado pela borda
nginx), e o passo na stack real fica como `NOT RUN`, com o motivo.

## Por que a stack real não rodou em 2026-11-07 (T028)

Histórico: a T028 rodou depois, no quality, em 2026-11-27 ([Agentes reais](#agentes-reais-t028)).

- A stack de `deploy/e2e` deste host foi montada antes da 025: imagens `dokpod-api:e2e` e `dokpod-agent:e2e` de
  2026-10-31, API CE, e os dois agentes parados.
- O cenário pede a imagem Business com uma licença que liste `image-update-notice`. A imagem só aceita licença assinada
  pela chave do cofre do fornecedor; a chave de teste só existe num build local com trust store próprio, que este ciclo
  não montou, e o arquivo externo de secrets da stack não é lido por automação (regra do projeto).
- Por isso T028 fica `NOT RUN`. Para executá-la: build local de `Business.Dockerfile` com a chave de teste, licença com
  `image-update-notice`, stack de `deploy/e2e/README.md` com os agentes Docker e Podman, `registry:3.1.2` com TLS de CA
  de teste confiada pelos agentes e `config.json` em `DOKPOD_AGENT_REGISTRY_AUTH_FILE`; depois os cenários 1 e 3 abaixo
  pela borda `https://localhost:7443/dokpod/`.

## Ambiente

| Item | Valor |
| --- | --- |
| Commit base | `94a0340` (main, release 0.6.0) + branch `feat/025-refinamento` |
| SDK | `docker.io/lzocateli/dotnet-sdk:10.0.403-noble-v1` |
| PostgreSQL | `postgres:17.6-bookworm` em container, banco limpo, migrations aplicadas com `dotnet ef database update` |
| Registro OCI | `docker.io/library/registry:3.1.2` (CNCF Distribution, Apache-2.0) em rede podman isolada, TLS de CA de teste e `htpasswd` gerados na execução |
| Frontend | `lzocateli/angular-cli:22.2.4-node24.22.1-bookworm-v1`, E2E simulado por `frontend/tests/e2e/mock/run-mock-e2e.sh` |

## Resultado por cenário

| Cenário | Passo | Resultado | Evidência |
| --- | --- | --- | --- |
| 1. Aviso nas três telas | 1–3 com agentes reais | Passou depois das correções | T028-1 e T028-2 corrigidos e o passo 2 repetido, ver [Agentes reais](#agentes-reais-t028) |
| 1. Aviso nas três telas | 4 com agentes reais | Passou com a D-003a | Digest e registro fora da política sem consulta; imagem do host verificada como as demais em Podman 6 e Docker 29 (T028-3) |
| 1. Aviso nas três telas | Comparação contra registro real: tag republicada vai de `up_to_date` a `update_available` | Passou | `OciRegistryIntegrationTests.RepublishedTag_GoesFromUpToDateToUpdateAvailable` |
| 1. Aviso nas três telas | Imagens, Containers e detalhe mostram o estado e "Verificar agora" | Passou | E2E simulado `test_025_image_update.py` |
| 1. Aviso nas três telas | 4: digest, imagem local, registro fora da política e inseguro viram `not_checkable` sem HTTP | Passou | `ImageUpdateCheckerTests`, `ImageUpdateSchedulerTests` |
| 2. Plataforma | Índice em que só outra arquitetura muda continua `up_to_date` | Passou | `OciRegistryIntegrationTests.IndexWhereOnlyAnotherArchitectureChanges_StaysUpToDate` (registro real) |
| 3. Falhas | Credencial errada e tag removida no registro real | Passou | `WrongCredential_IsUnauthorized`, `RemovedTag_IsReferenceNotFound` (registro real) |
| 3. Falhas | Registro parado, `429`, `5xx`, lentidão, resposta inválida; falha preserva a última verificação boa | Passou | `OciDistributionClientTests`, `ImageUpdateStoreTests` |
| 3. Falhas | Mesmos casos com agentes reais | Passou | Docker e Podman, ver [Agentes reais](#agentes-reais-t028) |
| 4. Baixar a nova versão | Ação com `image:pull`, ausente sem ele; verificação adiantada depois do pull | Passou | E2E simulado (`business-images-readonly`), `ImageUpdateAfterPullTests` |
| 4. Baixar a nova versão | `POST …/images/update-check` sem `image:pull` responde `403` | Passou | `ImageUpdateEndpointsTests` |
| 5. Alerta | Abre depois de N dias, mantém em `container_behind` e `failed`, resolve ao recriar | Passou | `AlertSweepServiceTests`, `ImageOutdatedAlertsTests`, E2E simulado |
| 6. Licença e composição | Licença restrita: "Verificar agora" e regra nova recusados; periódica continua | Passou | `ImageUpdateEndpointsTests`, `AlertGovernanceServiceTests`, `ImageUpdateSchedulerTests` |
| 6. Licença e composição | Sem a capability: nada agendado e campo ausente | Passou | `LicenseWithoutTheCapability_SchedulesNothing`, `WithoutTheCapabilityInTheLicense_TheFieldIsAbsent` |
| 6. Licença e composição | CE: nenhum campo e regra recusada com `422` | Passou | `BusinessGateTests`, `AlertGovernanceServiceTests` |
| 7. Segredos (SC-003) | Senha e token do `config.json` só no registro | Passou | `ImageUpdateCanaryTests` (5 execuções seguidas), `RegistryCredentialCanaryTests` |
| 7. Desempenho (SC-004) | p95 com 200 imagens verificadas ≤ 110% do p95 sem | Passou depois da correção F2 | Ver [SC-004](#sc-004-execução-dedicada) |

## Execuções dedicadas fora do CI

### Registro OCI real (T012)

```bash
# registry:3.1.2 em https://dokpod-t012-registry:5000 na rede dokpod-it, CA e htpasswd gerados na execução
DOKPOD_TEST_OCI_REGISTRY=https://dokpod-t012-registry:5000 DOKPOD_TEST_OCI_REGISTRY_CA=/reg/ca.pem \
DOKPOD_TEST_OCI_REGISTRY_USER=dokpod \
  dotnet test backend/tests/Dokpod.Agent.Infrastructure.Tests
```

A senha do `htpasswd` foi gerada aleatoriamente na execução e passada em `DOKPOD_TEST_OCI_REGISTRY_PASSWORD`.

Resultado: 58 aprovados, nenhum ignorado. Sem porta na URL, o nome `dokpod-t012-registry` é lido como repositório do
Docker Hub (o primeiro componente não tem `.` nem `:`) e os 4 testes reais falham; é o comportamento esperado da
gramática de referências, não da 025.

### SC-004 (T025)

```bash
DOKPOD_TEST_SC025=1 dotnet test backend/tests/Dokpod.ControlPlane.Api.Tests --filter ImageUpdateListImpactTests
```

| Execução | Imagens (mediana das razões p95) | Containers (mediana das razões p95) | Resultado |
| --- | --- | --- | --- |
| Antes da correção | 125,9% | 165,2%–207,1% por comparação | Reprovado |
| Depois da correção F2 | 102,1% | 102,2% | Aprovado |

Valores por comparação depois da correção (p95 sem / com 200 verificadas, em ms): Imagens 9,13/10,30, 9,07/8,35,
10,97/9,73, 8,60/9,36 e 11,54/11,78; Containers 2,50/3,35, 3,31/3,35, 3,31/3,38, 3,47/3,98 e 3,26/3,11.

### Agentes reais (T028)

Executada no quality em 2026-11-27, com a autorização do solicitante para um agente Docker rootless ao lado do Podman,
um registro descartável e a remoção de tudo no fim.

| Item | Valor |
| --- | --- |
| API | imagem Business 0.12.3 no quality, licença de teste `lic-teste-t028` (slot `hml`, capabilities `change-windows` e `image-update-notice`), emitida pela CLI com a chave `dokpod-2026-1` |
| Agentes | `dokpod-ce-agent` 0.12.3, um por engine, com identidade mTLS própria: Podman 6.2.0 rootless (API 1.44) e Docker Engine 29.9.3 rootless (API 1.47), em dois ambientes do estágio `development` |
| Registro | `registry:3.1.2` em `127.0.0.1:5443`, TLS de CA de teste confiada pelos agentes (`SSL_CERT_FILE`), `htpasswd`, credencial em `DOKPOD_AGENT_REGISTRY_AUTH_FILE`; proxy `nginx:1.28.0` em `127.0.0.1:5444` para o `429` |
| Política | `development` restrita a `127.0.0.1:5443` e `127.0.0.1:5444` durante a execução |
| Chamadas | "Verificar agora" por `POST …/images/update-check` e leitura de `GET /api/v1/images`, `GET /api/v1/containers` e `…/containers/{id}/summary`, tudo pela borda nginx com sessão do Keycloak |

Capability: os dois agentes anunciam `ImageUpdateCheck` (`CAPABILITY_IMAGE_UPDATE_CHECK = 23`). Com a licença ainda
não iniciada, "Verificar agora" respondeu `403 license_invalid`; ativa, `202`.

Cenário 1 (mesmo resultado nos dois engines, salvo onde indicado):

| Passo | Imagens | Containers | Detalhe | Resultado |
| --- | --- | --- | --- | --- |
| 2. `teste/web:1.0` publicada, "Verificar agora" | `up_to_date` (Docker); `not_checkable`/`digest_reference` (Podman) | `container_behind` | `up_to_date` | Reprovado: T028-1 e T028-2 |
| 3. Mesma tag com outro conteúdo | `update_available` | `update_available` | `update_available` | Passou |
| 4. Container por digest | `not_checkable`/`digest_reference` | igual | igual | Passou |
| 4. Registro fora da política (`docker.io/library/busybox:1.37.0`) | `not_checkable`/`registry_not_allowed` | igual | igual | Passou |
| 4. Imagem do host (`export \| import`) com nome fora da política | `not_checkable`/`registry_not_allowed` | igual | igual | Passou (a política decide antes) |
| 4. Imagem do host com nome no registro permitido | `failed`/`reference_not_found` | igual | igual | Reprovado: T028-3 |

O registro recebeu só `HEAD /v2/teste/web/manifests/1.0` e, no último caso, `HEAD /v2/teste/local/manifests/1.0`.
Nenhum pedido foi feito para o container por digest nem para os registros fora da política. Todas as verificações
pedidas terminaram em até 30 segundos.

Cenário 3: cada falha foi provocada depois de uma verificação boa às 00:36:56 UTC, com o mesmo resultado nos dois engines.

| Falha | Estado | `lastSuccessAt` | Resultado |
| --- | --- | --- | --- |
| Proxy respondendo `429` | `failed`/`rate_limited` | preservado | Passou |
| Manifesto da tag apagado (`DELETE` 202, `GET` 404) | `failed`/`reference_not_found` | preservado | Passou |
| Senha errada no arquivo de credencial do agente | `failed`/`unauthorized` | preservado | Passou |
| Registro parado | `failed`/`registry_unavailable` | preservado | Passou |

Em nenhum caso de falha apareceu `up_to_date`. Desfeita cada falha, a verificação seguinte voltou a passar.

Achados (defeitos do produto; correção só pelo fluxo Bug Fixing, com autorização):

1. **T028-1**: a lista de containers mostra `container_behind` para todo container em dia. A consulta da lista
   (`PostgresContainerSearchStore`) não lê `image_id`, e `ImageUpdateCheck.StateForContainer` compara `null` com
   `local_image_id`. O detalhe usa `GetAsync`, que lê a coluna, e mostra `up_to_date`. Os testes de lista passavam o
   `imageId` diretamente e não cobriam a consulta real.
2. **T028-2**: no Podman, uma imagem com tag e digest locais aparece em Imagens como `not_checkable`/`digest_reference` e
   sem tag. O Podman lista primeiro o `RepoTag` por digest; `ImageUpdateLookup.ForImage` usa a primeira referência com
   resultado e a tela de Imagens usa a primeira tag. No Docker a ordem é inversa e a tela fica certa.
3. **T028-3**: Podman 6 e Docker 29 (containerd image store) criam `RepoDigests` também para imagens importadas ou
   construídas no host. Por isso a regra de `ImageUpdateChecker` ("sem `RepoDigests` é `local_image`") não identifica
   essas imagens: o agente consulta o registro e mostra falha em vez de "Não verificável". Não houve falso "Atualizada",
   mas uma imagem local com o mesmo nome de uma remota seria comparada com ela.

Tratamento (Bug Fixing autorizado em 2026-11-27, relatórios em `.specify/bugs/`):

| Achado | Bug Fixing | Tratamento |
| --- | --- | --- |
| T028-1 | `lista-containers-sem-image-id` | A consulta da lista lê `image_id` |
| T028-2 | `imagens-podman-tag-por-digest` | Tags antes das referências por digest no estado da imagem, na tag exibida e em `repoTags` |
| T028-3 | `imagem-local-com-repodigests` | Decisão humana [D-003a](research.md#d-003-referências-não-verificáveis): a imagem do host é verificada como as demais nesses engines; sem mudança de código além de comentário e teste |

Repetição do passo 2 do cenário 1 (2026-11-27, 02:02 UTC), com a API do quality trocada temporariamente por uma imagem
Business construída localmente com as correções (`localhost/dokpod-api-business:t028`, override `compose-t028.yaml`,
mesmo procedimento de `vm-swap-api.sh`); os agentes continuaram na 0.12.3, sem mudança:

| Momento | Imagens | Containers | Detalhe | Resultado |
| --- | --- | --- | --- | --- |
| Nova versão puxada, containers antigos | `update_available` | `update_available` | — | Esperado: o inventário de imagens ainda não refletia o pull quando o pedido saiu |
| `t028-web` recriado nos dois hosts | `up_to_date` com tag `1.0` (Podman) | `up_to_date` | `up_to_date` | Passou: T028-1 e T028-2 corrigidos |
| Imagem do host com nome permitido | `failed`/`reference_not_found` | igual | igual | Passou pela D-003a |

Observação, sem defeito: no Docker, a imagem nova recebeu também as tags `teste/remover:1.0` e `teste/proxy:1.0`. A linha
mostra o estado da primeira tag com resultado (`remover`, apagada no cenário 3), regra de `ImageUpdateLookup.ForImage` anterior a esta validação.

## Suíte completa

| Projeto | Resultado |
| --- | --- |
| `Dokpod.Architecture.Tests` | 14 aprovados |
| `Dokpod.Licensing.Tests` | 81 aprovados |
| `Dokpod.Domain.Tests` | 191 aprovados |
| `Dokpod.Bff.Tests` | 29 aprovados |
| `Dokpod.Agent.Application.Tests` | 158 aprovados |
| `Dokpod.ControlPlane.Application.Tests` | 277 aprovados |
| `Dokpod.Agent.Infrastructure.Tests` | 53 aprovados, 5 ignorados sem `DOKPOD_TEST_OCI_REGISTRY` (58 com o registro real) |
| `Dokpod.ControlPlane.Api.Tests` | 374 aprovados, 3 ignorados: as medições isoladas do SC-004 da 027 e da 025 e a licença emitida pela CLI (`CliIssuedLicenseTests`, dependente de variável) |
| E2E simulado `-k test_025` | 7 aprovados |

`dotnet format --verify-no-changes` não acusa nada nos arquivos desta branch; os avisos de espaço em
`AgentCommandProtocolHandler.cs` e `ContainerInspectSanitizer.cs` já existiam em `main`.

## Achados

Os achados F1 (corrida no cache de tokens do cliente OCI) e F2 (SC-004 reprovado) foram corrigidos nesta branch e estão
descritos na [revisão de segurança](threat-model.md#revisão-de-segurança-e-privacidade-t029).
