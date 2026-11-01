# Threat model: aviso de nova versão de imagem

**Spec**: 025, tarefa T001 (gate antes de código) · **Data**: 2026-11-01 · **Origem**: IA assistida, a partir de [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md) e [contratos](contracts/)
**Estado**: **revisado e aceito** pelo solicitante em 2026-11-01 ([decisão](#decisão-do-revisor)).

Este documento cobre só as fronteiras novas da 025. As que já existem continuam regidas por [segurança](../../docs/seguranca.md), pelo [threat model da 031](../031-licenciamento-business-offline/threat-model.md) e pelos das specs 012 (pull) e 020 (registros):
- sessão do navegador;
- PEP com Keycloak;
- canal mTLS agente-servidor;
- pull pelo engine;
- gate de licença.

## Como revisar

1. Leia o [modelo do sistema](#modelo-do-sistema) e as [premissas](#premissas-de-confiança). A principal: o agente só consulta registros que o próprio host já usa, com a credencial que o host já tem.
2. Percorra as [fronteiras](#fronteiras-novas). Cada mitigação aponta o requisito (FR), a decisão (D) e a tarefa (T).
3. Decida cada [lacuna](#lacunas-encontradas): corrigir na tarefa indicada ou aceitar.
4. Aceite ou recuse cada [risco residual](#registro-de-riscos-residuais).
5. Preencha a [decisão](#decisão-do-revisor). A T001 só é marcada com a decisão registrada.

## Modelo do sistema

```text
CLIENTE (instalação)
  operador ──HTTPS──▶ web (nginx) ──▶ BFF ──▶ API Business (PEP) ──▶ Keycloak (environment:read | image:pull)
                                               │
                                               ├─▶ ImageUpdateCheckService ──▶ BusinessCapabilityGate ("image-update-notice")
                                               ├─▶ PostgreSQL: image_update_schedules, image_update_checks, alert_rules.outdated_days
                                               └─▶ ImageUpdateWorker (todas as réplicas; lease por host)
                                                        │ mensagem 17 (só a agentes com CAPABILITY_IMAGE_UPDATE_CHECK), canal mTLS existente
                                                        ▼
HOST GERENCIADO
  agente ──▶ RegistryCredentialFile (config.json do host, só leitura)
     │
     └──HTTPS (TLS verificado; HTTPS_PROXY do container)──▶ registro OCI (HEAD/GET de manifesto; token Bearer/Basic)
     ▲
     └── mensagem 27 (resultado: estado, motivo, digests)
```

### Premissas de confiança

- As referências consultadas vêm só dos containers do inventário do próprio host. Nenhum usuário informa host, registro ou URL para a verificação.
- O host já confia nos registros que suas imagens usam: o engine faz pull deles. A verificação não alcança um registro que o host não alcançaria.
- A credencial de registro é do host. Quem controla o host controla a credencial e o resultado da verificação.
- O resultado é informativo (FR-004). Nada no Dokpod muda o container a partir dele.

## Fronteiras novas

### B1. Saída do agente para o registro

| Ameaça | Mitigação | Rastro |
| --- | --- | --- |
| Interceptação ou registro falso respondendo digest | TLS verificado com o trust store do sistema e sem opção de desligar; registro inseguro vira "Não verificável" sem HTTP | D-001, D-003, T009 |
| Registro malicioso (ou comprometido) que redireciona para endereço interno ou outro host | Redirecionamento só para o mesmo host; `Authorization` nunca segue para outro | D-005, T009 |
| `realm` do desafio `Bearer` apontando para outro host, que receberia a credencial ou viraria SSRF | Ver [L1](#lacunas-encontradas) | T009 |
| Resposta enorme ou lenta esgota o agente | 10 s por requisição, 5 min por pedido, 4 MiB de manifesto, 4 consultas simultâneas | D-005, T009, T011 |
| Cota do registro esgotada pelo Dokpod | `HEAD` (fora da cota do Docker Hub); `GET` só quando o índice mudou; 1 pedido manual por minuto por host; `429` vira `rate_limited` sem nova tentativa | D-001, D-005, D-006, [L5](#lacunas-encontradas) |
| Proxy corporativo ignorado, levando a conexão direta para fora | `HTTPS_PROXY` do container do agente respeitado; sem proxy configurado, falha em vez de contornar | D-001, T027 |

### B2. Credencial do host fora do engine

| Ameaça | Mitigação | Rastro |
| --- | --- | --- |
| Credencial enviada ao plano de controle, registrada em log ou exibida | Lida só no agente, por registro, a cada pedido; nenhum campo do protocolo a carrega; canário em respostas, banco, logs e telemetria | FR-007, D-012, T024 |
| Mensagem de exceção com URL, token ou cabeçalho no log do agente | Ver [L2](#lacunas-encontradas) | T009, T011 |
| Token Bearer reutilizado fora do escopo | Ver [L3](#lacunas-encontradas) | T009 |
| Credencial de um registro usada em outro (por exemplo `docker.io` x espelho) | Credencial escolhida pelo registro normalizado da referência (`RegistryCredentialFile.HeaderFor`), sem curinga | T009 |

### B3. Protocolo agente-servidor (mensagens 17 e 27)

| Ameaça | Mitigação | Rastro |
| --- | --- | --- |
| Agente antigo recebendo mensagem desconhecida | Só enviada com `CAPABILITY_IMAGE_UPDATE_CHECK`; campos e tags novos (N/N-1) | D-004, T003 |
| Agente comprometido forjando resultados (falso "Atualizada" ou campos gigantes) | Resultado só vale para o próprio host; `request_id` pendente e prazo de 6 min; estado, motivo e digest validados e página inteira recusada fora do contrato | T014, [R2](#registro-de-riscos-residuais) |
| Pedido com referências de registros fora da política | O servidor filtra pela política do estágio antes de enviar e grava `registry_not_allowed` | FR-002, T013 |

### B4. API e telas

| Ameaça | Mitigação | Rastro |
| --- | --- | --- |
| Usuário sem acesso ao host vendo quais imagens estão desatualizadas | `updateCheck` só nas respostas que já exigem `environment:read`; falha do Keycloak nega | FR-010, T015 |
| "Verificar agora" usado em massa | `image:pull` no host, 1 por minuto por host, auditado | FR-010, D-006, T016 |
| Recurso comercial na CE ou sem licença | Módulo só na composição Business; gate em "Verificar agora" e na regra; CE recusa a condição de alerta com `422` | FR-008, D-009, D-010, T007, T016, T021 |
| Licença restrita cortando o aviso de segurança | A verificação periódica continua; só execuções novas a pedido são recusadas | FR-009, T013 |
| Digest remoto ou registro revelado a quem só lê | A API não devolve digest remoto nem registro resolvido | contracts/api.md, T015 |

### B5. Telemetria

| Ameaça | Mitigação | Rastro |
| --- | --- | --- |
| Referência, registro ou host privado do cliente em métrica | Métricas só por estado, motivo e host; nenhuma dimensão de referência ou registro | D-012, T026 |
| Spans ou métricas de HTTP da API com endereço (lição da T038 da 027) | O registro é chamado pelo agente, não pela API; a View da API já tira `server.address` das métricas `http.client.*` | T024 |

## Lacunas encontradas

| # | Lacuna | Recomendação | Tarefa |
| --- | --- | --- | --- |
| L1 | O `realm` do desafio `Bearer` vem do registro e pode apontar para outro host: a credencial do host seria enviada a ele, ou o agente seria levado a um endereço interno | Exigir `https` no `realm`. Enviar a credencial ao `realm` só quando ele é do mesmo host do registro, ou no par conhecido `docker.io` → `auth.docker.io`; nos demais casos, pedir token anônimo. Testar com `realm` em outro host | T009 |
| L2 | O agente registra exceções inteiras em outros pontos (`LogWarning(exception, …)`); a mensagem de uma `HttpRequestException` pode trazer a URL do registro, e a referência pode trazer nome de registro privado | No caminho da verificação, registrar só o tipo da exceção e o motivo mapeado, nunca a mensagem, a URL ou a referência | T009, T011, T024 |
| L3 | Um token Bearer guardado entre pedidos poderia ser usado depois do fim do escopo ou vazar em despejo de memória | Token só em memória e só durante o pedido, por repositório; nada em disco nem entre pedidos | T009 |
| L4 | Resposta do token endpoint sem limite de tamanho | Ler até 64 KiB; acima disso, `invalid_response` | T009 |
| L5 | Vários hosts com as mesmas imagens multiplicam as consultas ao mesmo registro | Aceitar: `HEAD` não consome a cota do Docker Hub, e a agenda espalha os hosts (5 h 24 min a 6 h). Reavaliar se um registro do cliente reclamar | D-006, [R3](#registro-de-riscos-residuais) |

## Registro de riscos residuais

| # | Risco | Fronteira | Mitigação atual | Aceite |
| --- | --- | --- | --- | --- |
| R1 | Quem controla o host altera o `config.json` ou o trust store do agente e faz a verificação mentir | B1, B2 | Fora do modelo: quem controla o host já controla engine e imagens | Aceito (2026-11-01) |
| R2 | Um agente comprometido mostra "Atualizada" para imagens do próprio host | B3 | Só afeta o host comprometido; o agente já é confiável para o inventário desse host | Aceito (2026-11-01) |
| R3 | Muitos hosts verificando as mesmas imagens geram carga no registro do cliente | B1 | `HEAD`, agenda espalhada, limite manual por host | Aceito (2026-11-01) |
| R4 | Em host com proxy só no daemon do engine, a verificação falha (`registry_unavailable`) até configurar `HTTPS_PROXY` no agente | B1 | Falha visível com motivo; runbook | Aceito (2026-11-01) |
| R5 | O estado mostra o resultado da última verificação; entre duas verificações, uma tag pode mudar sem aviso por até 6 h | B4 | Data da verificação sempre visível; "Verificar agora" | Aceito (2026-11-01) |

## Controles transversais

- **Autorização**: Keycloak por host (`environment:read`, `image:pull`, `alert:manage`); falha fechada.
- **Disponibilidade**: nenhuma consulta ao registro em requisição do console (SC-004); falha do registro não afeta workloads nem o console.
- **Dados**: credencial nunca sai do host; sem digest remoto nem registro resolvido na API.
- **Rede**: só HTTPS com TLS verificado; nenhuma porta nova de entrada.
- **Auditoria**: "Verificar agora", pull e regras de alerta auditados pelos fluxos existentes.

## Decisão do revisor

| Campo | Valor |
| --- | --- |
| Revisor | Solicitante (Origem: humano) |
| Data | 2026-11-01 |
| Premissa central (referências só do inventário do host; credencial do host) | Aceita |
| Lacunas L1–L5 | Recomendações aceitas: L1–L4 aplicadas na T009 e na T011; L5 aceita como está |
| Riscos R1–R5 | Aceitos |
| Agente Windows sem a capability até passar no teste contra o registro | Aceito |
| Saída direta do agente para registros exige ADR? (plan.md, itens para a revisão humana) | Recomendado registrar em ADR `proposed`, porque cria precedente de saída do agente além do engine. O ADR depende de o solicitante rodar `/dokpod-create-adr`, skill reservada à invocação humana; não bloqueia a implementação, que segue esta decisão |
| Declaração | "Minha decisão é executar suas recomendações, escreva a ADR se fizer sentido e não causar prejuizo ou regreção a funcionalidades e robustes do projeto como um todo" |
