# Validação: Aviso de nova versão de imagem

**Spec**: 025, tarefa T030 · **Data**: 2026-11-07 · **Origem**: IA assistida · **Estado**: evidências registradas; aguarda revisão humana

Execução dos sete cenários do [quickstart](quickstart.md). Onde o cenário pede a stack E2E Business com agentes reais,
a evidência vem de outra camada (registro OCI real em container, PostgreSQL 17.6 real e E2E simulado pela borda
nginx), e o passo na stack real fica como `NOT RUN`, com o motivo.

## Por que a stack real não rodou (T028)

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
| 1. Aviso nas três telas | 1–3 com agentes reais | `NOT RUN` | Motivo acima (T028) |
| 1. Aviso nas três telas | Comparação contra registro real: tag republicada vai de `up_to_date` a `update_available` | Passou | `OciRegistryIntegrationTests.RepublishedTag_GoesFromUpToDateToUpdateAvailable` |
| 1. Aviso nas três telas | Imagens, Containers e detalhe mostram o estado e "Verificar agora" | Passou | E2E simulado `test_025_image_update.py` |
| 1. Aviso nas três telas | 4: digest, imagem local, registro fora da política e inseguro viram `not_checkable` sem HTTP | Passou | `ImageUpdateCheckerTests`, `ImageUpdateSchedulerTests` |
| 2. Plataforma | Índice em que só outra arquitetura muda continua `up_to_date` | Passou | `OciRegistryIntegrationTests.IndexWhereOnlyAnotherArchitectureChanges_StaysUpToDate` (registro real) |
| 3. Falhas | Credencial errada e tag removida no registro real | Passou | `WrongCredential_IsUnauthorized`, `RemovedTag_IsReferenceNotFound` (registro real) |
| 3. Falhas | Registro parado, `429`, `5xx`, lentidão, resposta inválida; falha preserva a última verificação boa | Passou | `OciDistributionClientTests`, `ImageUpdateStoreTests` |
| 3. Falhas | Mesmos casos com agentes reais | `NOT RUN` | Motivo acima (T028) |
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

`NOT RUN`: motivo em [Por que a stack real não rodou](#por-que-a-stack-real-não-rodou-t028).

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
