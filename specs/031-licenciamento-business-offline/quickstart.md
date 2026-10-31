# Guia de Validação: Licenciamento Business offline

**Feature**: [spec.md](spec.md) | **Plano**: [plan.md](plan.md)

Cenários ponta a ponta para comprovar a feature depois da implementação. Tudo roda em containers (constituição, restrições técnicas); navegador e cliente HTTP usam a borda nginx. Licenças e chaves são sintéticas, geradas pelos vetores de teste do verificador; a chave de teste nunca está no trust store de produção.

## Pré-requisitos

- Stack de desenvolvimento (`deploy/`) com PostgreSQL e Keycloak locais, nas composições CE e Business.
- Usuário com `licensing:read` e `licensing:manage`; outro usuário só com `environment:read`.
- Rede da stack sem egress externo para os cenários 1 e 2 (ver [D-012](research.md#d-012-comprovação-de-ausência-de-comunicação-externa)).

## Cenário 1: CE intacta sem licença (História 1, SC-001, SC-002)

1. Subir a composição CE sem licença e com egress bloqueado.
2. Rodar as suítes existentes de backend e E2E.
3. Capturar o tráfego do processo da API durante início, operação e reinício.

**Esperado**: suítes verdes; nenhuma tela ou endpoint de licença; zero tentativas externas do licenciamento.

Comandos (job `Licensing CE` do CI):

```bash
frontend/tests/e2e/mock/run-mock-e2e.sh -- -k test_031        # T014: Configurações sem seção Licença na CE
pwsh ./tools/scripts/test-licensing-no-egress.ps1 -Build       # T015: captura do namespace de rede da API
```

O relatório da captura fica em `artifacts/licensing-no-egress/report.json`.

## Cenário 2: importação e recusa (História 2, SC-003)

1. Na composição Business, `GET /api/v1/licensing/status` → `entitlement: ce_only`.
2. `PUT /api/v1/licensing/license` com licença válida e `If-Match` do status → `200`, `entitlement: granted`, evento `license.imported`.
3. Repetir com cada vetor inválido (bit alterado, `kid` desconhecido, `alg: none`, produto errado, propriedade duplicada, revisão menor) → `422` com o código esperado, e arquivo de 65 KiB → `413`; status inalterado.
4. Usuário sem `licensing:manage` → `403 authorization_denied`, mesmo com licença válida.

Comandos:

```bash
dotnet test backend/tests/Dokpod.Licensing.Tests                       # T018/T019: vetores e desempenho
dotnet test backend/tests/Dokpod.ControlPlane.Api.Tests --filter Licensing   # T021: API com PostgreSQL real
frontend/tests/e2e/mock/run-mock-e2e.sh -- -k test_031                  # T029: jornada pela borda nginx
```

## Cenário 3: apuração (História 3, SC-004)

Com hosts sintéticos e faixa 16:

| Hosts e vCPUs | Esperado |
| --- | --- |
| 16, 17, 32 | `current = 5` |
| host X com 32 vCPUs às 10h, redimensionado para 16 às 12h; host Y com 16 às 10h, redimensionado para 48 às 15h | `windowPeak = 4` (10h: 2 + 1 = 3; 15h: 1 + 3 = 4), não 5 (soma dos picos isolados 2 + 3) |
| host com Docker e Podman associados, 32 e 32 | conta 2, uma vez |
| agente N-1 sem origem de CPU | `unknownHosts = 1`, último valor confiável mantido |
| host removido há 10 dias | contribuição histórica ainda no pico |

Trocar para licença de faixa 32 → pico recalculado dos dados brutos.

Comandos:

```bash
dotnet test backend/tests/Dokpod.ControlPlane.Application.Tests --filter CapacityMetering   # T032: linhas da tabela
dotnet test backend/tests/Dokpod.ControlPlane.Api.Tests --filter Licensing                  # T031, T033, T034
DOKPOD_TEST_PODMAN_SOCKET=/run/podman.sock dotnet test backend/tests/Dokpod.Agent.Podman.IntegrationTests --filter CpuCountSource  # T067
```

## Cenário 4: tolerância e restrição (História 4, SC-005, SC-006)

Com relógio injetável:

1. Licença expira; avançar 10 dias → `granted_in_grace`, `grace.until = expiresAt + 30d`.
2. Reiniciar a API e reimportar a mesma licença → `grace.until` inalterado.
3. Avançar além do prazo → `restricted`; nova execução de capability Business → `403 license_expired`; containers seguem rodando; leitura, backup, exportação e renovação funcionam; suítes CE verdes.
4. Excesso e expiração ao mesmo tempo → prazo único, o menor.
5. Retroceder o relógio abaixo do último instante confiável → `indeterminate`, sem tolerância nova.
6. Recuperar com `POST /api/v1/licensing/trusted-instant` (`licensing:manage`, `If-Match`, justificativa) → sai de `indeterminate`, sem prazo novo, com `license.trusted_instant_reset` na cadeia.

Comandos:

```bash
dotnet test backend/tests/Dokpod.ControlPlane.Application.Tests --filter "LicenseStateEvaluator|ExpiryWarning|CapacityDailySummary"  # T041, T044, T070
dotnet test backend/tests/Dokpod.ControlPlane.Api.Tests --filter "GracePersistence|BusinessCapabilityGate|TrustedInstantReset"       # T042, T043, T069
```

## Cenário 5: emissão (História 5, SC-009) — repositório da CLI

1. `license issue --dry-run` → nada gravado nem assinado.
2. Emitir; repetir o mesmo pedido → mesmo arquivo, código 0.
3. Mesmo `requestId` com conteúdo diferente → código 3.
4. Interromper o processo em cada etapa e reexecutar → uma única revisão.
5. Importar o arquivo emitido no cenário 2 → aceito.

## Cenário 6: rotação e revogação (História 6)

1. Build com duas chaves no trust store; importar licenças assinadas por cada uma → ambas aceitas.
2. `POST /api/v1/licensing/revocations` com pacote válido → `restricted` para Business; CE e workloads intactos.
3. Pacote com sequência menor → `422`.

Comandos:

```bash
dotnet test backend/tests/Dokpod.Licensing.Tests --filter RevocationVerifier                 # T049: tipos, limites e rotação
dotnet test backend/tests/Dokpod.ControlPlane.Api.Tests --filter RevocationEndpoints          # T050: PostgreSQL real
```

Procedimentos: [runbook de rotação e revogação](../../docs/runbooks/licenciamento-rotacao-e-revogacao.md).

## Cenário 7: artefatos do cliente (SC-008)

Inspecionar camadas e histórico das imagens CE e Business: nenhum emissor, nenhuma chave privada; a CE não contém o módulo de licenciamento registrado.
