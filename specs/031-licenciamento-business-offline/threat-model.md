# Threat model: licenciamento Business offline

**Spec**: 031, tarefa T002 (gate antes de código) · **Data**: 2026-10-31 · **Origem**: IA assistida, a partir de [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md) e [contratos](contracts/)
**Estado**: **revisado e aceito** pelo solicitante em 2026-10-31 (ver [decisão](#decisão-do-revisor)), inclusive a fronteira [B10](#b10-relatório-de-apuração-e-auditoria-obrigatória), acrescentada a partir dessa revisão e confirmada no mesmo dia.

Este documento cobre só as fronteiras novas da spec 031. As fronteiras já existentes (sessão do navegador, PEP com Keycloak, mTLS dos agentes, auditoria por ambiente) continuam regidas por [segurança](../../docs/seguranca.md) e pelo [threat model consolidado da 005](../005-qualificacao-release-docker-linux/threat-model-consolidado.md).

## Como revisar

1. Leia o [modelo do sistema](#modelo-do-sistema), em especial a [premissa central](#premissas-de-confiança): a licença offline é instrumento de conformidade contratual, não proteção contra quem controla o host.
2. Percorra as [fronteiras](#fronteiras-novas). Cada mitigação aponta o requisito (FR), a decisão (D) e a tarefa (T) que a implementa ou testa.
3. Decida cada item das [lacunas](#lacunas-encontradas) (corrigir antes do código, corrigir na tarefa indicada, ou aceitar).
4. Aceite ou recuse cada [risco residual](#registro-de-riscos-residuais).
5. Preencha a [decisão](#decisão-do-revisor). T002 só é marcada como concluída com a decisão registrada.

## Modelo do sistema

```text
FORNECEDOR (fora do produto)
  pedido aprovado ──▶ CLI dokpod-license-admin ──▶ provedor de chave (privada, F2)
                          │ registro de emissões
                          ▼
                   licença .jws / revogação .jws ──(canal fora de banda: e-mail, portal)──┐
                                                                                          │
CLIENTE (instalação offline)                                                              ▼
  administrador ──HTTPS──▶ web (nginx) ──▶ BFF ──▶ API Business (PEP) ──▶ Keycloak (licensing:read|manage)
                                                     │
                                                     ├─▶ Dokpod.Licensing.Verify(bytes, trust store embarcado, instante)
                                                     ├─▶ LicenseStateEvaluator ◀── relógio do host da API
                                                     ├─▶ BusinessCapabilityGate ◀── recursos exclusivos (specs 025–029)
                                                     └─▶ PostgreSQL: license_revisions, licensing_state,
                                                         licensing_hosts, licensing_capacity_observations,
                                                         licensing_capacity_anchors, licensing_events
  agente ──gRPC mTLS──▶ API: AgentHello.cpu_count + cpu_count_source (tag 14)
  PostgreSQL ──pg_dump/pg_restore──▶ backup (operação)
  Imagem CE: sem módulo, sem trust store, sem endpoints /api/v1/licensing/*
```

**Ativos**:
- Chave privada de assinatura do fornecedor (o ativo de maior impacto: quem a tem emite licenças válidas para qualquer cliente).
- Integridade da decisão de direito Business (autenticidade, vigência, apuração, prazos).
- Disponibilidade da CE e dos workloads: nenhum estado de licença pode afetá-los (FR-003).
- Privacidade do cliente: nada de licença ou apuração sai da instalação (FR-002, FR-026).
- Trilha de licenciamento (`licensing_events`) e registro de emissões do fornecedor.

**Atores e adversários**:

| # | Ator | Capacidade | Objetivo adverso |
| --- | --- | --- | --- |
| A1 | Usuário autenticado sem `licensing:*` | Sessão válida no console | Ler ou alterar licença, descobrir estado comercial |
| A2 | Administrador do cliente com `licensing:manage` | Importa arquivos, associa hosts | Usar mais capacidade ou mais tempo que o contratado |
| A3 | Operador do host ou do banco do cliente | Root no host, superusuário no PostgreSQL, controle do relógio e da imagem | Contornar o licenciamento por completo |
| A4 | Terceiro que entrega arquivo malicioso ao administrador | Só o conteúdo do arquivo | Explorar o parser, travar a API, induzir estado errado |
| A5 | Atacante ou insider no fornecedor | Acesso à CLI, ao pedido ou à chave | Emitir licença não aprovada, vazar a chave |
| A6 | Cadeia de fornecimento | Build e publicação das imagens | Trocar o trust store ou embutir chave de teste |

### Premissas de confiança

- **Premissa central**: quem controla o host, o banco ou a imagem (A3) pode contornar qualquer verificação local. A licença offline é dissuasão e evidência para o contrato (verificação anual por relatório, FR-033), não DRM. O produto não alega controle global ([casos de borda](spec.md#casos-de-borda)).
- Keycloak é a autoridade de autorização; licença nunca supre permissão (FR-022).
- A chave privada nunca existe no repositório do produto nem nas imagens (FR-028, SC-008).
- O agente é confiável para o próprio host, mas a CPU que ele informa é declaração do cliente.
- O canal de entrega do arquivo é inseguro: integridade e origem vêm só da assinatura.

## Fronteiras novas

### B1. Upload de licença (navegador → API)

**Fronteira**: `PUT /api/v1/licensing/license?slotId=` com corpo `application/jose` até 65536 bytes.

| STRIDE | Ameaça | Mitigação | Ligação |
| --- | --- | --- | --- |
| E | A1 importa licença sem permissão | Ordem autenticar → autorizar no Keycloak → avaliar → persistir; `403 authorization_denied` antes de ler o corpo | FR-022, FR-023, D-008; T010, T021, T027 |
| T | CSRF faz o navegador do administrador importar arquivo | `X-Dokpod-Antiforgery` obrigatório; cookie de sessão do BFF | D-008; T021 |
| D | Arquivo enorme ou upload repetido esgota memória | Limite de 65536 bytes aplicado no servidor antes de bufferizar (`413`); limites de D-003 no parser | FR-008, D-003; T018, T021 |
| D | Uploads recusados acumulam bytes em `license_revisions` sem limite | **Lacuna [L4](#lacunas-encontradas)** | — |
| T | Duas importações concorrentes deixam estado misto | `If-Match` com `version` otimista; troca de `active_revision_id` numa transação; recusa nunca toca o estado | FR-009; T021, T025 |
| T | Arquivo recusado substitui a licença íntegra | Recusa grava só a revisão com `outcome = rejected` | FR-009; T020, T025 |
| I | Conteúdo da licença aparece em erro, lista ou notificação | Interface nunca mostra o arquivo; `details` dos eventos em allowlist sem conteúdo | FR-026, FR-027; T009, C6 da análise |
| T | Campos textuais da licença (`plan`, IDs) usados para XSS ou injeção em log | Strings ≤ 128 (`plan` ≤ 64); renderização com escape do Angular; logs estruturados. Conjunto de caracteres dos IDs não restrito: **lacuna [L5](#lacunas-encontradas)** | D-003; T023, T028 |
| S | Slot de outra instalação escolhido para dobrar a cota | Slot precisa existir e ter cota ≠ 0; troca auditada. Offline, duas instalações podem escolher o mesmo slot: residual [R4](#registro-de-riscos-residuais) | D-008; T020 |

### B2. Verificador e trust store embarcado

**Fronteira**: bytes não confiáveis → `Dokpod.Licensing` (BCL, sem I/O) com chaves públicas embutidas na composição Business.

| STRIDE | Ameaça | Mitigação | Ligação |
| --- | --- | --- | --- |
| S | Arquivo escolhe o próprio algoritmo (`none`, HS256 com a chave pública como segredo) | Só `ES256`; algoritmo vem do perfil, não do arquivo | FR-005, D-001; T018, T022 |
| S | Arquivo traz a própria chave (`jwk`, `jku`, `x5u`, `x5c`) ou `crit` | Header com exatamente `alg`, `kid`, `typ`; qualquer outro membro recusa | D-001; T018, T022 |
| S | Licença assinada com chave de teste aceita em produção | Vetores usam chave sintética; trust store de produção não a contém. Sem teste que garanta isso: **lacuna [L8](#lacunas-encontradas)** | T018, T027 |
| T | Parser interpreta claims antes de verificar a assinatura | Assinatura verificada sobre os segmentos recebidos, antes de interpretar; nada é reserializado | D-003; T022 |
| T | JSON ambíguo (chave duplicada, campo desconhecido, número fora de `int32`, UTF-8 inválido) lido de forma diferente pela CLI e pelo produto | Parser estrito único, compartilhado pelos dois lados, com vetores publicados | FR-008, D-002, D-003; T018, T023, T054 |
| D | Profundidade ou tamanho extremos travam a verificação | Profundidade 8, 64 capabilities, 2 slots, 64 KiB | D-003; T018, T019 |
| T | Assinatura ECDSA maleável (`s` e `n − s`) gera arquivo diferente com a mesma licença | Inofensivo para a autenticidade, mas colide com a regra de idempotência por hash do arquivo: **lacuna [L6](#lacunas-encontradas)** | I1 da análise; T020 |
| E | Documento de revogação aceito como licença, ou o contrário | `typ` distinto e propósito próprio | D-011; T049 |
| T | Resultado de verificação gravado usado como prova depois de adulterado no banco | Conteúdo persistido é reverificado a cada avaliação | FR-010; T020, T026 |
| E | Chave do fornecedor comprometida | Trust store com várias chaves por `kid` permite rotação. Clientes offline só deixam de aceitar a chave antiga ao atualizar a imagem: residual [R7](#registro-de-riscos-residuais); procedimento de comprometimento ausente: **lacuna [L9](#lacunas-encontradas)** | D-011; T024, T053 |

### B3. Pacote de revogação

**Fronteira**: `POST /api/v1/licensing/revocations` com JWS `dokpod-revocation+jws`.

| STRIDE | Ameaça | Mitigação | Ligação |
| --- | --- | --- | --- |
| T | Reaplicar pacote antigo para "desfazer" uma revogação mais nova | `sequence` estritamente crescente; menor ou igual → `422` | D-011; T050, T052 |
| E | Pacote de outra organização revoga licença alheia | Só IDs da organização da licença ativa; diferente → recusa | D-011; T050 |
| I | Lista de revogação expõe outros clientes | Escopo mínimo por organização, até 64 entradas, sem lista global | D-011; T049 |
| D | Revogação para containers ou bloqueia a CE | Só novas execuções Business são recusadas; workloads e CE seguem | FR-003, FR-021; T043, T050 |
| T | Restauração de backup anterior à revogação reativa a licença | Runbook manda reimportar a revogação após restore (L3); a auditoria seguinte acusa `discontinuous` ou `mismatch`; residual [R5](#registro-de-riscos-residuais) | SC-006, D-013; T063, T071 |
| S | Antiforgery ausente no `POST` | O contrato exige `X-Dokpod-Antiforgery`; `If-Match` não existe neste endpoint, mas a sequência monotônica serializa a aplicação | D-008; T050 |

### B4. Relógio local e prazos

**Fronteira**: relógio do host da API → `LicenseStateEvaluator` e job periódico.

| STRIDE | Ameaça | Mitigação | Ligação |
| --- | --- | --- | --- |
| T | A3 volta o relógio para estender vigência ou tolerância | `last_trusted_instant_utc` monotônico; relógio abaixo dele → `indeterminate` sem tolerância nova | FR-019, D-006; T041, T045 |
| T | Reinício da API, reimportação ou alteração local recomeça a tolerância | Prazos gravados na primeira detecção e nunca recomeçados; tolerância de expiração derivada de `expiresAt` assinado | FR-019, FR-020; T042, T045 |
| D | Correção legítima de NTP (alguns segundos para trás) deixa tudo `indeterminate` | Recuo de até 5 minutos não muda o estado nem avança o instante (L1) | D-006; T041 |
| D | Relógio adiantado por engano empurra `last_trusted_instant_utc` para o futuro; ao corrigir, o estado fica `indeterminate` até a data alcançar o valor | `POST /api/v1/licensing/trusted-instant` (`licensing:manage`, auditado), sem tolerância nova (L2) | D-006; T069 |
| T | A2 volta o relógio e usa a recuperação para "rejuvenescer" a licença | Cada uso grava `license.trusted_instant_reset` com os dois instantes na cadeia; a auditoria obrigatória da renovação mostra o uso (FR-034) | D-006, D-013; T069, T071 |
| E | Cache de decisão do gate mantém direito depois do prazo | Reavaliação a cada operação exclusiva e a cada job, sem cache que estenda prazo | D-006; T043, T046 |
| R | Mudança de estado sem rastro | `license.state_changed`, `grace_started`, `grace_ended`, `expiry_warning` | FR-025, FR-032; T044, T047 |

### B5. Persistência, backup e restauração

**Fronteira**: tabelas de licenciamento no PostgreSQL do cliente → `pg_dump`/`pg_restore`.

| STRIDE | Ameaça | Mitigação | Ligação |
| --- | --- | --- | --- |
| T | Edição direta de `licensing_state`, observações ou âncoras | Licença reverificada a cada uso (FR-010); `licensing_events` append-only por trigger para a role da aplicação. O superusuário do banco contorna tudo: residual [R2](#registro-de-riscos-residuais) | FR-010; T006, T007 |
| R | Apagar a trilha de licenciamento | Sem `UPDATE`/`DELETE` para a role da aplicação; sem retenção automática | T006, T007 |
| T | Retenção por partição apaga a âncora e a apuração conta errado | `licensing_capacity_anchors` gravada antes da retenção | D-005; T007, T033 |
| T | Restaurar backup antigo apaga observações recentes e o início do excesso: a tolerância de excesso recomeça | SC-006 restrito à expiração (L3); restauração anterior ao último relatório aceito aparece como `discontinuous` na auditoria seguinte | FR-019, FR-035, SC-006, D-013; T071 |
| T | Restaurar backup antigo e voltar o relógio juntos | Indetectável localmente: residual [R6](#registro-de-riscos-residuais) | D-006 |
| I | Backup expõe a licença | A licença não contém dado pessoal, preço ou inventário; o backup já é tratado como sensível (R10 da 005) | FR-007 |

### B6. Capacidade informada pelos agentes e associação de hosts

**Fronteira**: `AgentHello` (mTLS) → `CapacityLedger`; endpoints de hosts com `licensing:manage`.

| STRIDE | Ameaça | Mitigação | Ligação |
| --- | --- | --- | --- |
| S | Agente N-1 ou fallback do processo conta como CPU confiável | Só `ENGINE` grava `vcpus` confiável; `PROCESS_FALLBACK` e `UNSPECIFIED` entram como medição desconhecida | FR-013, FR-017, D-004; T030, T037, T066–T068 |
| T | Agente modificado informa menos vCPU com origem `ENGINE` | O agente é software do cliente: residual [R3](#registro-de-riscos-residuais); o relatório (FR-033) mostra origem e valores | D-004; T060 |
| T | A2 associa muitos ambientes a um único host para contar menos | Associação exige `licensing:manage`, um ambiente em no máximo um host ativo, eventos `host.linked`/`unlinked`/`removed`; idempotência indefinida (D2 da análise). Residual [R8](#registro-de-riscos-residuais) | FR-012, FR-025; T034, T039 |
| T | Remover host apaga o histórico da janela | Remoção fecha o intervalo sem apagar observações | FR-014; T032 |
| D | Falha ao gravar observação derruba a sessão de comandos | Falha isolada; a sessão continua | contrato do agente; T037 |
| D | Rajada de `AgentHello` enche as observações | Gravação só na mudança ou na conexão; sessões já limitadas por identidade e fencing | D-005; T033 |

### B7. Composição CE/Business e imagens

**Fronteira**: build (`-p:DokpodEdition=Business`) → imagens publicadas.

| STRIDE | Ameaça | Mitigação | Ligação |
| --- | --- | --- | --- |
| E | Variável de ambiente transforma CE em Business | Edição fixada no build; não há interruptor em runtime | FR-001, D-009; T011, T013 |
| D | Licença ausente ou inválida degrada a CE | CE não registra o módulo nem o gate; testes rodam sobre a imagem CE | FR-001, FR-003; T013, T014, T017 |
| E | Recurso Business chamado sem passar pelo gate | Cada caso de uso exclusivo consulta `IBusinessCapabilityGate`; teste do gate. Sem teste de arquitetura que obrigue a consulta: **lacuna [L7](#lacunas-encontradas)** | FR-021; T043, T046 |
| T | Build troca o trust store ou embute chave privada | Inspeção de camadas e histórico; assinatura e atestação das imagens | SC-008; T061, T062 |
| T | A3 altera a imagem Business para remover o gate | Residual [R1](#registro-de-riscos-residuais) | premissa central |
| I | Canal e nome da imagem Business indefinidos | U4 da análise; bloqueia T061 | T061 |

### B8. Ausência de comunicação externa e privacidade

| STRIDE | Ameaça | Mitigação | Ligação |
| --- | --- | --- | --- |
| I | Licenciamento "liga para casa" em erro, expiração ou revogação | Nenhuma chamada de rede; prova por captura de DNS/TCP/UDP em rede sem egress, nas duas edições | FR-002, SC-002, D-012; T015, T065 |
| I | Telemetria, logs ou relatório de erro levam dados de licença ou apuração | Canários de licença procurados em logs, métricas e traces; `details` em allowlist | FR-026; T015, T064 |
| I | Relatório de apuração enviado automaticamente | Exportação só local, sob `licensing:read`, auditada | FR-033; T060 |

### B10. Relatório de apuração e auditoria obrigatória

**Fronteira**: `GET /api/v1/licensing/report?challenge=` → arquivo entregue fora de banda → `dokpod-license-admin report verify` → emissão. Acrescentada na revisão de 2026-10-31: toda contratação e renovação exige o relatório (FR-034), que precisa dificultar a manipulação pelo administrador do host (FR-035). Contrato em [report-format.md](contracts/report-format.md).

| STRIDE | Ameaça | Mitigação | Ligação |
| --- | --- | --- | --- |
| T | A2/A3 apaga ou altera um evento (importação, excesso, reset de relógio) antes de exportar | Cadeia SHA-256 sem lacunas; remoção, alteração ou reordenação dá `tampered` | FR-035, D-013; T009, T071 |
| T | A3 reescreve a história anterior à última auditoria | Registro de emissões guarda `headSeq`/`headHash` aceitos; divergência dá `discontinuous` | FR-035, D-013; T071, T072 |
| T | A3 recalcula a cadeia inteira com dados falsos depois da última auditoria | Reescrita precisa bater com todas as licenças e revogações que o fornecedor emitiu (`mismatch`) e com o resumo diário contínuo; o resto é residual [R9](#registro-de-riscos-residuais) | D-013; T071 |
| S | Relatório de outra instalação, ou de uma instalação recriada para zerar a história | `installationId` aleatório gravado na contratação; troca dá `discontinuous` e exige aprovação dupla com justificativa | FR-034; T072 |
| R | Relatório antigo reapresentado na renovação | `challenge` do pedido gravado na cadeia antes da exportação; ausente ou diferente dá `stale` | FR-034; T060, T071 |
| E | Fornecedor emite renovação sem auditoria | CLI exige `--report` com resultado `ok` (código 6); exceção só com aprovação dupla registrada | FR-029, FR-034; T072, T073 |
| I | Relatório leva dado pessoal, hostname ou endereço ao fornecedor | Só IDs opacos, contagens e códigos; o autor de cada evento vai como `actorRef` (HMAC com chave da instalação que nunca é exportada), não como sujeito do Keycloak (decisão de 2026-10-31) | FR-007, FR-026; T009, T060 |
| I | Envio pela internet expõe dados e cria dependência do fornecedor | Não adotado (D-013): o produto nunca envia; entrega fora de banda pelo cliente | FR-002, D-013; T015 |
| D | Cadeia cresce sem limite | Volume baixo (importações, mudanças de host, um resumo por dia); sem retenção por ser trilha de auditoria | D-013 |

### B9. CLI de emissão e chave privada do fornecedor (desenho preliminar)

**Fronteira**: pedido aprovado → `dokpod-license-admin` → provedor de chave. Repositório, armazenamento e provedor são decisões de F2 (D-010); esta seção fixa os requisitos mínimos e deve ser revista em F2.

| STRIDE | Ameaça | Mitigação | Ligação |
| --- | --- | --- | --- |
| I | Vazamento da chave privada | Chave em provedor dedicado (HSM ou equivalente, F2); nunca em arquivo do repositório, argumento ou variável registrada; nenhum emissor nas imagens | FR-028, D-010; T055, T062 |
| E | Operador emite licença sem aprovação | Conferência dupla registrada; aprovação ligada ao hash do pedido (código 4) | FR-029; T057 |
| T | Repetição ou alteração de um pedido já emitido | Idempotência por `requestId` + hash (0 na repetição, 3 com conteúdo diferente) | FR-029; T057 |
| T | Emissão com cotas acima da capacidade ou datas inválidas | Validação antes de assinar (código 2) e verificação do artefato com `Dokpod.Licensing` antes de entregar | FR-016, FR-029; T057, T058 |
| T | Falha no meio da emissão gera duas revisões | Saída atômica e recuperação para o mesmo resultado lógico | FR-029, SC-009; T057 |
| I | `--dry-run` acessa a chave ou numera revisão | `--dry-run` não assina, não grava, não acessa o provedor | contrato da CLI; T057 |
| R | Emissão sem rastro | Registro de emissões com operador, aprovação e hash, sem inventário do cliente | FR-029 |
| I | Segredo em log ou stderr | Diagnóstico redigido; passphrase nunca por argumento | contrato da CLI; T058 |

## Lacunas encontradas

| # | Lacuna | Recomendação | Decisão |
| --- | --- | --- | --- |
| L1 | Não existe tolerância para correções pequenas do relógio (NTP); qualquer recuo abaixo de `last_trusted_instant_utc` leva a `indeterminate` | Definir em D-006 uma tolerância fixa (proposta: 5 minutos) abaixo da qual o recuo não muda o estado e não avança `last_trusted_instant_utc`; testar em T041 | Aceita. D-006 e T041 |
| L2 | Nenhuma tarefa implementa a saída de `indeterminate` (relógio adiantado por engano, estado corrompido) | Nova tarefa na US4: operação `licensing:manage`, auditada (`license.trusted_instant_reset`), que restabelece o instante confiável sem conceder tolerância nova; teste junto de T041 | Aceita. T069 e contrato `POST /api/v1/licensing/trusted-instant` |
| L3 | Restaurar backup anterior desfaz revogação e pode recomeçar a tolerância de excesso; SC-006 promete que restauração "não estende nenhum prazo" | Restringir SC-006 ao que é verificável offline (tolerância de expiração, derivada de `expiresAt`), documentar no runbook que após restore a revogação deve ser reimportada, e aceitar R5 | Aceita. SC-006 restrito; runbook em T063; continuidade da cadeia (D-013) detecta na auditoria seguinte |
| L4 | Uploads recusados ficam com até 64 KiB cada em `license_revisions`, sem limite | Para documentos recusados, guardar só hash, tamanho e código de falha (sem `content`), e aplicar rate limit aos dois endpoints de upload; ajustar data-model.md e T025 | Aceita. data-model.md, T025 e rate limit de 10 por minuto em T021/T027 |
| L5 | `licenseId` e `organizationId` aceitam qualquer texto até 128 caracteres; comparação na revogação e escrita em logs ficam sujeitas a normalização Unicode e caracteres de controle | Restringir a `[A-Za-z0-9_-]{1,128}` no formato, no verificador e nos vetores (T018, T023) | Aceita. license-format.md, data-model.md, T018 |
| L6 | A idempotência por hash do arquivo inteiro (I1) trata uma assinatura maleada como "mesma revisão com outro hash" e recusa como `revision_reused` | Basear a idempotência no hash do conteúdo assinado (`header.payload`), não do arquivo; ajustar T020 e data-model.md | Aceita. `content_sha256` sobre `header.payload`; T020 |
| L7 | Nada garante que futuros casos de uso Business (specs 025–029) consultem o gate | Teste em `Dokpod.Architecture.Tests` exigindo que handlers marcados como Business dependam de `IBusinessCapabilityGate` | Aceita. T074 |
| L8 | Nenhum teste garante que a chave de teste dos vetores não está no trust store de produção | Incluir em T062 (ou teste de unidade da composição Business) a verificação de que nenhum `kid` de teste está embarcado | Aceita. T062 |
| L9 | O runbook de rotação (T053) não cobre comprometimento da chave | Acrescentar a T053 o procedimento de comprometimento: nova chave, release com a antiga removida, reemissão das licenças e comunicação aos clientes | Aceita. T053 |

## Registro de riscos residuais

| # | Risco | Fronteira | Mitigação atual | Aceite |
| --- | --- | --- | --- | --- |
| R1 | Quem controla a imagem ou o binário remove o gate ou troca o trust store | B7 | Contrato, relatório anual, assinatura das imagens distribuídas | Aceito (2026-10-31) |
| R2 | Superusuário do PostgreSQL altera estado, observações ou âncoras | B5 | Reverificação da licença; trilha append-only para a role da aplicação | Aceito (2026-10-31) |
| R3 | Agente modificado informa CPU menor com origem `ENGINE` | B6 | Relatório com origem por host; verificação contratual | Aceito (2026-10-31) |
| R4 | A mesma licença ou o mesmo slot usados em mais de uma instalação | B1 | Contrato; caso de borda já registrado na spec | Aceito (2026-10-31) |
| R5 | Restauração de backup anterior desfaz revogação ou recomeça a tolerância de excesso | B3, B5 | Runbook de restore (L3); detectado como `discontinuous` na auditoria seguinte (D-013) | Aceito (2026-10-31) |
| R6 | Restauração de backup combinada com recuo do relógio não é detectada | B4, B5 | Nenhuma além do contrato | Aceito (2026-10-31) |
| R7 | Chave do fornecedor comprometida continua aceita até o cliente atualizar a imagem | B2 | Rotação com sobreposição; procedimento de comprometimento (L9) | Aceito (2026-10-31) |
| R8 | Administrador com `licensing:manage` associa ambientes de hosts diferentes a um só host | B6 | Eventos auditados e relatório exportável | Aceito (2026-10-31) |
| R9 | Quem controla o host recalcula a cadeia com dados falsos depois do último relatório aceito, de forma coerente com as emissões | B10 | Cadeia, continuidade, cruzamento com o registro de emissões, resumo diário, contrato | Aceito (2026-10-31) |

## Controles transversais

- **Autorização**: scopes `licensing:read` e `licensing:manage` no recurso `urn:dokpod:installation`; falha do Keycloak = negado; recusa por licença distinguível de falha de autenticação (FR-022 a FR-024).
- **Disponibilidade**: nenhum estado de licença para containers, agentes, leitura, backup, exportação ou renovação (FR-003, FR-021).
- **Rede**: zero tentativas de comunicação externa, provadas por captura (FR-002, D-012).
- **Auditoria**: `licensing_events` global, append-only, sem conteúdo da licença (FR-025, FR-026).
- **Dados**: a licença não contém dado pessoal, inventário ou preço (FR-007).

## Decisão do revisor

| Campo | Valor |
| --- | --- |
| Revisor | lzocateli (solicitante) |
| Data | 2026-10-31 |
| Premissa central (licença como conformidade, não DRM) | Aceita |
| Lacunas L1–L9 | Recomendações aceitas; correções aplicadas nos artefatos da spec (ver coluna Decisão) |
| Riscos R1–R9 aceitos | Sim (R9 confirmado com B10) |
| Decisões novas da revisão | Auditoria obrigatória em toda contratação e renovação (FR-034); relatório com evidência de adulteração (FR-035); envio online avaliado e não adotado (D-013) |
| Confirmação de B10, D-013 e R9 | Confirmados pelo solicitante em 2026-10-31 |
| Condições para iniciar o código | Nenhuma nova; `/speckit-implement` continua exigindo autorização explícita |
| Resultado do gate T002 | **PASS** |
