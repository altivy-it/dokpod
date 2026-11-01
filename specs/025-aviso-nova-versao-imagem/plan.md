# Plano de Implementação: Aviso de nova versão de imagem

**Branch**: `025-aviso-nova-versao-imagem` | **Data**: 2026-11-01 | **Spec**: [spec.md](spec.md)

**Entrada**: Especificação da funcionalidade em `/specs/025-aviso-nova-versao-imagem/spec.md`

**Nota**: Este template é preenchido pelo comando `/speckit-plan`; sua definição descreve o fluxo de execução.

> Redija este plano e todos os artefatos derivados em português brasileiro (pt-BR). Preserve nomes de código, caminhos, comandos e identificadores técnicos.

## Resumo

A edição comercial com a capability `image-update-notice` na licença passa a avisar quando a tag usada por um container aponta, no registro, para um conteúdo diferente do que está no host.

- **Quem consulta**: o agente, direto na API OCI Distribution (`HEAD` do manifesto), com as credenciais que já lê do host. Assim há um único caminho para Docker e Podman, e a credencial não sai do host. O Podman 5.7 não tem o endpoint `distribution` do Docker.
- **Comparação**: o digest remoto contra os `repo_digests` locais, considerando a plataforma do host.
- **Quem decide**: o plano de controle decide a agenda (6 h ou "Verificar agora"), a política de registros e a licença, e envia o pedido ao agente por uma mensagem nova do protocolo, com capability própria.
- **O que fica guardado**: só o último resultado por host, referência e plataforma.
- **Onde aparece**: Imagens, Containers e o detalhe do container. A spec 016 ganha a condição "imagem desatualizada há N dias".
- **Licença restrita**: a verificação periódica continua; "Verificar agora" e criar ou editar a regra são recusados.

## Contexto Tecnico

**Linguagem/Versão**: C# / .NET 10 (agente, API, aplicação, infraestrutura); TypeScript / Angular 22 (estado nas telas e condição de alerta).

**Dependências Principais**: nenhum pacote novo. O agente usa `HttpClient` da BCL para a API OCI Distribution ([D-001](research.md#d-001-onde-e-como-consultar-o-registro)). `Google.Protobuf` e `Grpc.Tools` já geram o protocolo.

**Persistência**: PostgreSQL existente, schema `dokpod`:
- tabelas `image_update_schedules` (uma linha por host) e `image_update_checks` (uma linha por referência em uso por host);
- coluna nula `outdated_days` em `alert_rules` ([data-model.md](data-model.md)).

O inventário de imagens e containers continua sendo a projeção da spec 012.

**Testes**:
- unidades em `Dokpod.Agent.Application.Tests`: comparação de digests, plataforma, motivos e mapeamento HTTP;
- `Dokpod.Agent.Infrastructure.Tests` contra um registro OCI real (`registry:2`) em container, com TLS e autenticação;
- `Dokpod.ControlPlane.Application.Tests`: agenda, estados, estado derivado do container, gate e condição de alerta;
- `Dokpod.ControlPlane.Api.Tests` com PostgreSQL real: migration `Up`/`Down`, lease entre réplicas, rotas, canário e p95 das listas;
- `Dokpod.Architecture.Tests` (`BusinessGateTests`);
- E2E Playwright pela borda nginx (respostas simuladas);
- agentes Docker e Podman reais na stack E2E.

**Plataforma Alvo**: plano de controle em containers Linux atrás do nginx; agente Linux em container e agente Windows. O agente Windows anuncia a capability quando passar no mesmo teste contra o registro; até lá, fica sem anunciar.

**Tipo de Projeto**: plano de controle self-hosted (API, BFF, web) e agente.

**Metas de Desempenho**:
- nova versão no console em até 6 h, ou 1 minuto com "Verificar agora" (SC-001);
- 200 imagens verificadas em cerca de 10 s por host (4 consultas simultâneas);
- p95 das listas no máximo 10% acima do p95 sem dados de verificação, e nenhuma consulta ao registro durante requisições (SC-004).

**Restrições**:
- credencial de registro só no host (FR-007);
- TLS verificado e sem HTTP para registros inseguros ([D-003](research.md#d-003-referências-não-verificáveis));
- política de registros da spec 020 aplicada antes do pedido;
- nenhuma atualização automática (FR-004);
- nada no CE;
- o worker não consulta o gate (FR-009).

**Escala/Escopo**: matriz de 100 hosts (ADR 2026-0003), cerca de 200 imagens em uso por host, uma verificação a cada 6 h por host.

## Verificação da Constituição

*GATE: Deve ser aprovado antes da pesquisa da Fase 0 e revisado novamente após o design da Fase 1.*

| Princípio | Como o plano atende | Situação |
| --- | --- | --- |
| I. pt-BR | Artefatos em pt-BR; identificadores preservados | ok |
| II. Engine local e workloads | Só leitura no registro; nada muda containers nem imagens (FR-004); o engine continua a fonte de verdade do que está no host | ok |
| III. Identidade externa e menor privilégio | `environment:read` para ver e `image:pull` para "Verificar agora", decididos pelo Keycloak com falha fechada; credencial nunca sai do host | ok |
| IV. Operações duráveis e capabilities | Capability `CAPABILITY_IMAGE_UPDATE_CHECK` anunciada pelo agente e validada em Docker e Podman separadamente; agenda durável com lease; "Verificar agora" auditado e limitado | ok |
| V. Modularidade e compatibilidade | Protocolo só com campos novos (N/N-1); OpenAPI com `x-dokpod-edition`; migrations expand-contract com `Down`; sem datastore, broker ou cache novos | ok |
| VI. Qualidade por plataforma | Registro OCI real em container; agentes Docker e Podman reais; PostgreSQL real; E2E pela borda | ok |
| Dependências permissivas | Nenhum pacote novo; `registry:2` (Apache-2.0) só nos testes | ok |
| Tabelas de alto crescimento particionadas | As duas tabelas crescem com hosts e imagens em uso, não com o tempo, e não têm retenção temporal | ok |
| Threat model para fronteira nova | Saída nova do agente para registros, credencial do host usada fora do engine e nova mensagem do protocolo: [threat model](threat-model.md) aceito em 2026-11-01 (T001) | ok |
| ADR para decisão duradoura | O agente passa a falar direto com registros, antes só pelo engine. A decisão fica em D-001 e no threat model aceito; [ADR 2026-0009](../../docs/adr/2026-0009-agente-consulta-registros-oci.md), aceito em 2026-11-01 | ok |

Reavaliação após o desenho da Fase 1: sem violações; a tabela de complexidade fica vazia. O threat model segue como entrega obrigatória antes do código.

## Estrutura do Projeto

### Documentação desta Funcionalidade

```text
specs/025-aviso-nova-versao-imagem/
├── spec.md
├── plan.md                # este arquivo
├── research.md            # D-001 a D-012
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── agent-protocol.md  # capability 23, mensagens 17 e 27
│   └── api.md             # updateCheck, POST update-check, condição de alerta
├── checklists/requirements.md
└── tasks.md               # gerado por /speckit-tasks
```

### Código-Fonte (raiz do repositório)

```text
contracts/agent/v1/agent.proto                       # capability e mensagens novas
contracts/openapi/dokpod-control-plane.v1.yaml       # updateCheck, rota business, condição de alerta

backend/libs/Dokpod.Domain/Images/ImageUpdateCheck.cs              # estados, motivos, plataforma
backend/libs/Dokpod.Domain/Alerts/AlertModels.cs                   # AlertConditionKind.ImageOutdated, OutdatedDays

backend/libs/Dokpod.Agent.Application/Images/ImageUpdateChecker.cs # comparação de digests e plataforma (D-002)
backend/libs/Dokpod.Agent.Infrastructure/Registries/OciDistributionClient.cs  # HEAD/GET, desafio Bearer/Basic, limites (D-005)

backend/libs/Dokpod.ControlPlane.Application/ImageUpdates/
├── ImageUpdatePorts.cs            # agenda, verificações, despacho ao agente
├── ImageUpdateCheckService.cs     # [BusinessCapability("image-update-notice")] "Verificar agora"
├── ImageUpdateScheduler.cs        # host vencido, política de registros, pedido, resultado
└── ImageUpdateAlertCondition.cs   # provedor da condição de alerta (só Business)

backend/libs/Dokpod.ControlPlane.Infrastructure/ImageUpdates/
├── ImageUpdateStore.cs            # tabelas e lease
└── (junção nas consultas de imagens e containers existentes)

backend/libs/Dokpod.ControlPlane.Infrastructure/Migrations/Script/<data>_ImageUpdateCheck/{Up,Down}/

backend/apps/Dokpod.ControlPlane.Api/ImageUpdates/
├── ImageUpdateEndpoints.cs        # POST update-check
├── ImageUpdateModule.cs           # registrado só por ProductEdition (Business)
└── ImageUpdateWorker.cs           # BackgroundService com lease por host

backend/apps/Dokpod.Agent/         # anuncia a capability e atende a mensagem 17

frontend/web/src/app/features/{images,containers}/   # estado, data, "Verificar agora", "Baixar nova versão"
frontend/web/src/app/features/alerts/                # condição "imagem desatualizada há N dias"

backend/tests/…/ImageUpdate*/      # unidades, registro OCI real, PostgreSQL real
frontend/tests/e2e/mock_ui/test_025_image_update.py
```

**Decisão de Estrutura**: segue a 027 e a 031.
- O agente ganha uma consulta de host, como as do agente das specs 013 e 018, sem estado.
- No plano de controle, portas e regras ficam na aplicação; PostgreSQL na infraestrutura; rotas e worker num módulo que só o `ProductEdition` Business registra.
- A condição de alerta entra no domínio da 016, mas só fica disponível quando o módulo Business registra o provedor; a CE recusa a regra.

## Registro de Complexidade

Sem violações a justificar.

## Itens para a revisão humana

- **Saída direta do agente para registros ([D-001](research.md#d-001-onde-e-como-consultar-o-registro))**: antes, só o engine falava com registros. O agente passa a usar a mesma credencial e a respeitar `HTTPS_PROXY` do seu container. Em host com proxy só no daemon, a verificação dá `failed` até configurar o agente. Decisão do solicitante em 2026-11-01 ([threat model](threat-model.md#decisão-do-revisor)): seguir as recomendações; registrado no [ADR 2026-0009](../../docs/adr/2026-0009-agente-consulta-registros-oci.md), aceito em 2026-11-01.
- **Registros inseguros ([D-003](research.md#d-003-referências-não-verificáveis))**: ficam como "Não verificável", sem HTTP.
- **Licença sem a capability ([D-009](research.md#d-009-licença-e-composição))**: diferente da 027, o worker para e o estado some, como pede o esclarecimento. Com a licença restrita, continua.
- **Agente Windows**: só anuncia a capability depois de passar no teste contra o registro; até lá, os hosts Windows mostram "Não verificável" (`agent_unsupported`).
- **Condição de alerta na 016 ([D-010](research.md#d-010-condição-de-alerta))**: o domínio da CE ganha o valor `ImageOutdated`, mas a CE recusa regras com ele.
