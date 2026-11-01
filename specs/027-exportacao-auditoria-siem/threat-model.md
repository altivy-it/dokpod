# Threat model: exportação de auditoria para SIEM

**Spec**: 027, tarefa T001 (gate antes de código) · **Data**: 2026-11-01 · **Origem**: IA assistida, a partir de [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md) e [contratos](contracts/)
**Estado**: **revisado e aceito** pelo solicitante em 2026-11-01 ([decisão](#decisão-do-revisor)).

Este documento cobre só as fronteiras novas da 027. Ficam fora dele as fronteiras que já existem: sessão do navegador, PEP com Keycloak, a própria trilha de auditoria (spec 017, ADR 2026-0004) e o gate de licença. Elas continuam regidas por [segurança](../../docs/seguranca.md), pelo [threat model da 031](../031-licenciamento-business-offline/threat-model.md) e pelo [consolidado da 005](../005-qualificacao-release-docker-linux/threat-model-consolidado.md).

## Como revisar

1. Leia o [modelo do sistema](#modelo-do-sistema) e as [premissas](#premissas-de-confiança). A principal delas: quem tem `audit-export:manage` é administrador confiável da instalação, e o SIEM é do próprio cliente.
2. Percorra as [fronteiras](#fronteiras-novas). Cada mitigação aponta o requisito (FR), a decisão (D) e a tarefa (T).
3. Decida cada [lacuna](#lacunas-encontradas): corrigir na tarefa indicada ou aceitar.
4. Aceite ou recuse cada [risco residual](#registro-de-riscos-residuais).
5. Preencha a [decisão](#decisão-do-revisor). A T001 só é marcada com a decisão registrada.

## Modelo do sistema

```text
CLIENTE (instalação)
  administrador ──HTTPS──▶ web (nginx) ──▶ BFF ──▶ API Business (PEP) ──▶ Keycloak (audit-export:read|manage)
                                                     │
                                                     ├─▶ AuditExportDestinationService ──▶ BusinessCapabilityGate ("audit-siem-export")
                                                     ├─▶ PostgreSQL: audit_export_destinations, audit_export_cursors,
                                                     │               audit_events (+ tx_id, export_seq)
                                                     ├─▶ /run/secrets/dokpod-audit-export/<secretRef>/ (montado, só leitura)
                                                     └─▶ AuditExportWorker (todas as réplicas; lease por destino)
                                                               │  syslog RFC 5424 / TLS   ou   OTLP/HTTP / TLS
                                                               ▼
                                                       SIEM do cliente (rede do cliente)
```

### Premissas de confiança

- Quem tem `audit-export:manage` é administrador da instalação. Esse papel pode escolher para onde a auditoria vai, mas não pode apagá-la, e a mudança de destino é ela mesma auditada.
- O SIEM pertence ao cliente, que é o controlador dos dados exportados. O Dokpod não envia nada ao fornecedor (FR-002 da 031 continua valendo).
- Quem controla o host, o PostgreSQL ou os secrets montados controla a exportação. Não há defesa contra o próprio operador da infraestrutura.
- A trilha local é a fonte de verdade; a exportação é uma cópia (FR-004).

## Fronteiras novas

### B1. Configuração de destino (navegador → API)

| Ameaça | Mitigação | Rastro |
| --- | --- | --- |
| Usuário sem permissão cria ou vê destinos | Scopes `audit-export:read`/`audit-export:manage` decididos pelo Keycloak antes de revelar qualquer destino; falha do Keycloak nega | FR-005, T002, T016, T020 |
| Configuração com campos maliciosos (propriedades extras, caminho, nome) | Contrato fechado (propriedades desconhecidas recusadas), restrições do [modelo de dados](data-model.md) no banco e na aplicação | T003, T006, T016 |
| Corrida entre dois administradores | `If-Match`/`version`; `Idempotency-Key` por 24 h | T009, T016 |
| Recurso Business acionado sem licença | `[BusinessCapability("audit-siem-export")]` com gate; desativar e excluir nunca bloqueados | FR-007, FR-008, D-008, T019 |

### B2. Conexão de saída ao SIEM (API → destino)

| Ameaça | Mitigação | Rastro |
| --- | --- | --- |
| SSRF: destino apontado para serviços internos ou metadados de nuvem | Recusa de loopback, link-local (inclusive `169.254.169.254`), `0.0.0.0`/`::` e sub-redes do plano de controle | D-007, T010 |
| DNS que muda entre a validação e a conexão (rebinding) | A política vale sobre o IP resolvido no momento da conexão, e a conexão usa exatamente esse IP | D-007, T010, [L3](#lacunas-encontradas) |
| Redirecionamento HTTP do OTLP para endereço proibido | Redirecionamentos desligados no cliente OTLP | [L1](#lacunas-encontradas) |
| Interceptação ou servidor falso | TLS 1.2/1.3 com validação de cadeia e nome sem opção de desligar; mTLS opcional | D-007, T015, T018, T029 |
| Destino lento ou hostil esgota recursos | Tempos limite de conexão e de envio, corpo de resposta limitado, no máximo 5 destinos, espera exponencial até 60 s | [L2](#lacunas-encontradas), D-011 |
| Envio de teste usado para varrer portas da rede interna permitida | Envio de teste com limite de taxa e auditado | [L8](#lacunas-encontradas) |

### B3. Credenciais montadas

| Ameaça | Mitigação | Rastro |
| --- | --- | --- |
| Travessia de path ou symlink para fora da raiz | `secretRef` em `^[a-z0-9-]{1,64}$`, caminho normalizado e conferido dentro de `/run/secrets/dokpod-audit-export/` antes do acesso, symlink para fora recusado | D-006, T011 |
| Vazamento do conteúdo do secret | A API devolve só a presença de cada arquivo; nada em logs, métricas, eventos ou respostas | D-006, T020, T038 |
| Arquivo gigante ou malformado | Tamanho máximo por arquivo e formato conferido (PEM, token sem CR/LF) | [L4](#lacunas-encontradas), [L5](#lacunas-encontradas) |
| Rotação de certificado ignorada | Arquivos lidos a cada conexão, sem cache além dela | [L9](#lacunas-encontradas) |

### B4. Dados de auditoria fora da instalação

| Ameaça | Mitigação | Rastro |
| --- | --- | --- |
| Segredos, terminal ou variáveis exportados | Mesma projeção sanitizada do console e do CSV (spec 017); canário nos dois transportes | FR-002, D-010, T007, T034 |
| Administrador cria um destino para extrair a auditoria | O destino nasce no fim da trilha, sem o passado; a criação gera `audit_export.destination_created`, que vai a todos os destinos existentes (o SOC vê) | D-001, T009, [L7](#lacunas-encontradas) |
| Esconder do SOC a criação de um destino desmarcando a categoria `audit_export` | A categoria `audit_export` é sempre enviada e não pode ser desmarcada | [L7](#lacunas-encontradas) |
| Injeção no formato do destino (quebra de quadro syslog, campos falsos) | Contagem de octetos, JSON escapado, `MSGID` só ASCII imprimível sem espaço, atributos OTLP tipados | T014, [L6](#lacunas-encontradas) |
| Dado pessoal (nome do ator) no SIEM | O cliente é o controlador e o SIEM é dele; o runbook registra isso | T036, [R5](#registro-de-riscos-residuais) |

### B5. Cursor, réplicas e durabilidade

| Ameaça | Mitigação | Rastro |
| --- | --- | --- |
| Perda de eventos confirmados fora de ordem | Cursor por `(tx_id, export_seq)` abaixo do `xmin` | D-001, T008 |
| Duas réplicas enviando o mesmo destino | Lease de 30 s; repetição com o mesmo `eventId` | D-003, T025 |
| Transação longa segura a exportação (negação de serviço acidental ou intencional por quem tem acesso ao banco) | Sem perda; métrica e tela de atraso; runbook | D-001, T024, T027, [R3](#registro-de-riscos-residuais) |
| Lacuna silenciosa | Evento `audit_export.gap`, aviso persistente e métrica | FR-006, D-002, T024 |

### B6. Composição e licença

| Ameaça | Mitigação | Rastro |
| --- | --- | --- |
| Rotas ou worker presentes na CE | Módulo registrado só pelo `ProductEdition` Business; `BusinessGateTests` estendido | FR-007, T012 |
| Licença restrita cortando a conformidade do cliente | O envio continua; só operações novas são bloqueadas | FR-008, T017 |

## Lacunas encontradas

| # | Lacuna | Recomendação | Tarefa |
| --- | --- | --- | --- |
| L1 | O cliente HTTP segue redirecionamentos por padrão; um coletor (ou quem o controla) poderia redirecionar o OTLP para endereço proibido | `AllowAutoRedirect = false`; 3xx vira `rejected`; teste | T029, T031 |
| L2 | Sem tempos limite e sem limite de resposta definidos | Conexão 10 s, envio 30 s, corpo de resposta lido até 64 KiB e descartado | T015, T018, T029, T031 |
| L3 | A política de endereços pode ser contornada se o transporte resolver o nome de novo depois da validação | Resolver uma vez, validar os IPs e conectar ao IP validado (syslog: `Socket.ConnectAsync(IPAddress)`; OTLP: `ConnectCallback`), mantendo o nome para SNI e validação do certificado | T010, T018, T031 |
| L4 | Arquivos do secret sem limite de tamanho | Máximo de 64 KiB por arquivo; acima disso, `secret_missing` com motivo | T011 |
| L5 | Token com CR/LF permitiria injetar cabeçalhos | Token só com caracteres visíveis ASCII, sem espaço nas pontas; senão `secret_missing` | T011, T031 |
| L6 | `MSGID` derivado da ação poderia levar espaço ou caractere não ASCII | Só ASCII 33–126, até 32 caracteres; o resto vira `_` | T014 |
| L7 | Um administrador poderia criar um destino e desmarcar a categoria `audit_export` nos outros para o SOC não ver | A categoria `audit_export` é obrigatória em todo destino (aceita na API, sempre incluída) | T005, T016, T019, T022 |
| L8 | O envio de teste revela `connection_refused` ou `timeout`, o que dá um varredor de portas da rede privada permitida | Limite de 5 testes por minuto por instalação, todos auditados; códigos mantidos (necessários para diagnóstico) | T016, T019 |
| L9 | Certificados e token trocados no secret não seriam percebidos se a leitura fosse cacheada | Ler os arquivos a cada conexão | T011, T018, T031 |

## Registro de riscos residuais

| # | Risco | Fronteira | Mitigação atual | Aceite |
| --- | --- | --- | --- | --- |
| R1 | Administrador com `audit-export:manage` envia os eventos futuros a um destino próprio na rede privada | B4 | Destino nasce sem o passado; criação auditada e enviada aos destinos existentes; categoria `audit_export` obrigatória (L7) | Aceito (2026-11-01) |
| R2 | Quem controla o PostgreSQL altera cursor ou destinos (por exemplo, pula eventos) | B5 | Mudanças pela API são auditadas; acesso direto ao banco está fora do modelo (como R2 da 031) | Aceito (2026-11-01) |
| R3 | Uma transação longa no banco atrasa todos os destinos | B5 | Sem perda; métrica e tela de atraso; runbook | Aceito (2026-11-01) |
| R4 | Entrega pelo menos uma vez: o SIEM pode receber duplicatas | B5 | `eventId` estável para deduplicação no destino | Aceito (2026-11-01) |
| R5 | Nomes e identificadores de usuários vão para o SIEM do cliente | B4 | O cliente é o controlador; mesma informação do console e do CSV | Aceito (2026-11-01) |
| R6 | Redes privadas comuns são permitidas, então um destino pode atingir serviços internos do cliente nelas | B2 | Só administradores; TLS obrigatório; teste limitado e auditado (L8) | Aceito (2026-11-01) |

## Controles transversais

- **Autorização**: Keycloak no recurso `urn:dokpod:installation`; falha fechada.
- **Disponibilidade**: o envio nunca roda no request; a trilha local não depende dele (FR-004).
- **Dados**: mesma sanitização da spec 017; segredos nunca no banco, no console, em logs ou métricas.
- **Rede**: só conexões de saída para destinos configurados, sempre TLS, filtradas por endereço; nenhuma porta nova de entrada.
- **Auditoria**: toda mudança de destino, teste e lacuna vira evento na trilha.

## Decisão do revisor

| Campo | Valor |
| --- | --- |
| Revisor | Aceito (2026-11-01) |
| Data | Aceito (2026-11-01) |
| Premissa central (administrador confiável, SIEM do cliente) | Aceito (2026-11-01) |
| Lacunas L1–L9 | Recomendações aceitas; aplicadas nas tarefas indicadas, no contrato e no modelo de dados |
| Riscos R1–R6 | Aceito (2026-11-01) |
| Declaração | "aceito todas, marque a T001 e abra o PR" |
