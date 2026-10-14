# Especificação da Feature: Inventário, Lifecycle e Reconciliação

**Branch da migração**: `chore/adotar-spec-kit`; branch de implementação a definir após autorização.

**Criada em**: 2026-10-06

**Estado**: `draft` - rascunho de migração, aguardando revisão humana.

**Entrada**: P-01, P-02, P-04 e P-05 do [MVP](../../docs/plan/mvp.md).

## Cenários do Usuário e Testes (obrigatório)

### Jornada do Usuário 1 - Observar estado que converge (Prioridade: P1)

Como operador autorizado, quero inventário por ambiente com revisão e idade, reconstruível a partir do engine local.

**Valor desta prioridade**: A projeção não pode se tornar fonte de verdade dos workloads.

**Teste independente**: Produzir delta/lacuna/reconexão e snapshot paginado em engine real; comparar projeção convergente com a fonte.

**Cenários de aceite**:

1. **Dado** lacuna ou base divergente, **Quando** a sessão recebe delta, **Então** solicita ressincronização e só substitui a projeção atomicamente após snapshot completo.
2. **Dado** sessão antiga ou página fora de ordem/limite, **Quando** publica estado, **Então** não altera o ambiente ativo nem outro ambiente.
3. **Dado** agente offline, **Quando** o usuário consulta, **Então** vê conexão e idade do estado sem supor dados atuais ou acesso direto ao engine pela API.

### Jornada do Usuário 2 - Operar sem repetição indevida (Prioridade: P1)

Como operador, quero start, stop, restart e delete autorizados, confirmados e auditados.

**Valor desta prioridade**: Replay e perda de resposta não podem provocar efeito duplicado silencioso.

**Teste independente**: Repetir intenção idêntica e divergente com engine real, restart do agente e perda de resposta.

**Cenários de aceite**:

1. **Dado** comando válido, **Quando** aceito pelo agente, **Então** seu journal durável existe antes do ACK; o resultado terminal é persistido antes de reportado.
2. **Dado** mesmo ID/hash com resultado persistido, **Quando** redespachado, **Então** reproduz o resultado sem novo efeito; payload divergente é rejeitado.
3. **Dado** alvo recriado ou revisão stale, **Quando** recebe comando, **Então** não opera o novo container; delete não remove volumes implicitamente.

### Jornada do Usuário 3 - Recuperar operação indeterminada (Prioridade: P1)

Como operador, quero distinguir falha comprovada de efeito possível e reconciliar após indisponibilidade.

**Valor desta prioridade**: Não existe garantia distribuída de exactly-once.

**Teste independente**: Interromper após despacho/aceite, retomar por nova sessão e comparar journal, estado observado, comando durável e auditoria.

**Cenários de aceite**:

1. **Dado** expiração antes de despacho, **Quando** o sweep ocorre, **Então** o comando falha; se o efeito pode ter ocorrido, torna-se `Indeterminate`.
2. **Dado** resposta perdida e fencing renovado, **Quando** redespachado, **Então** preserva o fencing original e registra o de redespacho sem stream obsoleto consumir novos comandos.
3. **Dado** resultado definitivo tardio para expiração indeterminada, **Quando** reconciliado, **Então** preserva eventos de expiração e resultado append-only.

## Casos de Borda

- ID/hash/envelope inválido, enum/capability desconhecida, deadline expirado durante serialização e cancelamento.
- Clone/sessão stale, sequência não monotônica, snapshot incompleto, reconnect storm e payload acima do limite.
- Crash antes/depois do ACK ou resultado, journal reaberto, resposta perdida, claim concorrente e replay entre meses.
- Versões N/N-1, rollback expand-contract e falha de dependência na UI.

## Requisitos (obrigatório)

### Requisitos Funcionais

- **FR-001**: Engine local DEVE ser fonte de verdade; apenas o agente o acessa por socket/pipe local permitido. API nunca acessa socket remoto nem fornece proxy genérico.
- **FR-002**: Sessão DEVE usar gRPC bidirecional HTTP/2 com mTLS, ambiente derivado do certificado, sessão única, negociação N/N-1, capabilities e fencing; mensagens têm versão, sessão, sequência, UTC e correlation ID.
- **FR-003**: Heartbeat DEVE usar baseline de 15 segundos com jitter de 20%, sem histórico individual ilimitado; snapshots iniciais, solicitados após lacuna e periódicos de 15 minutos com jitter são paginados/limitados e aplicados atomicamente por ambiente/revisão.
- **FR-004**: Consulta DEVE usar autorização `environment:read`, cursor opaco/limite máximo, revisão/idade; SignalR emite apenas invalidações por ambiente, inicialmente no máximo uma por segundo.
- **FR-005**: Comando DEVE ser persistido no servidor antes de envio, com ID idempotente/hash canônico, alvo imutável, revisão, prazo, scope e capability. Só start/stop/restart/delete são aceitos.
- **FR-006**: Agente DEVE validar identidade, fencing, prazo, capability e deduplicação; journal-before-ack é obrigatório, assim como persistência de resultado antes de reporte. Linux exige write-through, flush físico, rename atômico e sincronização de diretório; equivalência Windows depende de 006.
- **FR-007**: Entrega DEVE ser at-least-once com dedup e reconciliação, sem promessa exactly-once; replay terminal não repete efeito, divergência é rejeitada e deadline é revalidado após espera.
- **FR-008**: Fila DEVE ter claim concorrente seguro, transições monotônicas, cancelamento de stream fenced, fencing original separado do redespacho, sweep e reconciliação tardia limitada a expiração indeterminada.
- **FR-009**: Intenção/resultado/expiração DEVEM manter auditoria atômica append-only, chave global de idempotência entre meses, partições mensais/DEFAULT e retenção de tombstones somente após decisão humana.
- **FR-010**: UI DEVE apresentar loading/vazio/erro/forbidden/indisponível, conexão, idade e resultado, ações por scope, confirmação contextual de delete e polling cancelável pelo BFF; nunca remover volume implicitamente.
- **FR-011**: Protobuf DEVE preservar números/tags, compatibilidade segura de campos desconhecidos e rejeição de comandos/enums desconhecidos; migrations mantêm rollback durante janela N-1.

### Entidades Principais

- **Snapshot/delta**: revisão monotônica, páginas ordenadas e projeção reconstruível.
- **Comando/execução/journal**: intenção, chave global, hash, fencing, deadline e resultado observado durável.

## Critérios de Sucesso (obrigatório)

### Resultados Mensuráveis

- **SC-001**: Projeção converge com engine real após lacuna/reconexão, sem troca de ambiente ou aplicação parcial de snapshot.
- **SC-002**: Cenários de replay/concorrência/crash comprovam journal-before-ack e nenhum novo efeito após resultado persistido.
- **SC-003**: Expiração e resposta perdida mantêm distinção Failed/Indeterminate e trilha íntegra, com recuperação reproduzível.
- **SC-004**: Quatro ações reais preservam volumes e autorização; N/N-1 é validado com versões consecutivas, não apenas handshake da mesma versão.

## Premissas e Proveniência

O legado registra domínio, transporte, fila, journal Linux, PostgreSQL, OpenAPI, UI e quatro ações E2E existentes. Não recriá-los. Avaliar provas 1/4/5/8 do MVP P-01, fundação P-02, inventário P-04 e lifecycle P-05. Carga nominal 56/1.120/30 por 30 minutos e margem >=100/2.000/50 pertencem a 005; Windows/Podman a 006. Retenção/replay, crash recovery e N/N-1 ainda exigem evidência específica.
