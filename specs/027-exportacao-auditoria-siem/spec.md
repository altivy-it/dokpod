# Especificação da Feature: Exportação de auditoria para SIEM

**Branch da feature**: `027-exportacao-auditoria-siem` (rascunho original em `025-029-roteiro-recursos`)

**Criada em**: 2026-10-27

**Estado**: `approved` - aprovada pelo solicitante (lzocateli) em 2026-11-01: "aprovado, execute a T001 da spec 027", depois de esclarecida, planejada e analisada. Implementação autorizada a partir da T001 (threat model, gate antes de código).

**Entrada**: Pedido do solicitante em 2026-10-27: "Itens de referência que ainda não estão no dokpod, inclua todos no roadmap", item 3 de 5: exportação contínua da auditoria por syslog e OpenTelemetry.

**Depende de**: [017](../017-auditoria/spec.md) (trilha de auditoria e exportação CSV), [022](../022-configuracoes/spec.md) (Configurações), [031](../031-licenciamento-business-offline/spec.md) (licença Business, capability `audit-siem-export` e verificação de licença), [ADR 2026-0004](../../docs/adr/2026-0004-auditoria-persistente.md) (auditoria persistente), [ADR 2026-0007](../../docs/adr/2026-0007-precificacao-por-node-e-faixa-de-vcpu.md) (planos e capabilities).

## Esclarecimentos

### Sessão 2026-10-27 (recomendações adotadas, sujeitas à aprovação)

- P: Quais protocolos? → R: Syslog (formato estruturado padrão, com transporte TLS) e OpenTelemetry logs, os dois mais aceitos por ferramentas de SIEM. Webhooks genéricos ficam fora desta entrega.
- P: O que é enviado? → R: Os mesmos eventos da trilha de auditoria (spec 017), com os mesmos campos e a mesma sanitização; nunca conteúdo de terminal, valores de variáveis ou segredos.
- P: E se o destino cair? → R: Os eventos ficam em fila durável no plano de controle e são reenviados na volta, pelo menos uma vez, por até 72 horas; depois disso a lacuna é registrada e sinalizada. A trilha local nunca depende do envio.
- P: Quem configura? → R: Administradores, em Configurações, com destino, protocolo, filtros por categoria e teste de envio. Certificados e tokens do destino vêm de secret montado, não do console.

### Sessão 2026-11-01 (alinhamento com a spec 031; recomendações aceitas pelo solicitante: "execute o recomendado")

- Q: Com a licença restrita (vencida além da tolerância, revogada ou em excesso), o envio aos destinos já configurados continua? → A: Continua. A restrição bloqueia só o que é execução nova da capability: criar, editar ou reativar destino e enviar evento de teste (`403 license_*`). A regra de nunca bloquear exportação (`docs/licenciamento.md`) e a conformidade do cliente prevalecem.
- Q: Quais planos recebem a exportação para SIEM? → A: Quem tiver a capability `audit-siem-export` na licença, qualquer que seja o plano (Teams ou Business, variante A′ do ADR 2026-0007). Sem licença, ou sem a capability, o recurso não aparece na CE e a API responde como CE.
- Q: Como a fila durável é guardada sem criar tabela de crescimento alto? → A: Sem tabela de fila. Cada destino guarda um cursor (último evento entregue) sobre `audit_events`, que já é particionada por mês. O "tamanho da fila" é a distância entre o cursor e o fim da trilha, e a janela de 72 horas vale pela idade do evento seguinte ao cursor. O estado de entrega é uma linha por destino.
- Q: Que permissões o Keycloak decide para essa área? → A: Scopes `audit-export:read` (ver destinos, situação e fila) e `audit-export:manage` (criar, editar, desativar, testar) no recurso `urn:dokpod:installation`; falha do Keycloak nega. Toda mudança de destino é auditada sem segredos.
- Q: Quais formatos, limites e comportamento com várias réplicas da API? → A: Syslog RFC 5424 sobre TLS (RFC 5425, enquadramento por contagem de octetos) e OTLP/HTTP com protobuf sobre TLS; até 5 destinos por instalação; um único remetente por destino entre as réplicas, garantido por lease no PostgreSQL.

## Cenários do Usuário e Testes (obrigatório)

### Jornada do Usuário 1 - Enviar a auditoria ao SIEM (Prioridade: P1)

O administrador configura um destino syslog com TLS e passa a ver os eventos do Dokpod no SIEM da empresa em segundos.

**Valor desta prioridade**: empresas com SOC exigem a trilha no SIEM central para correlação e retenção regulatória.

**Teste independente**: configurar um coletor de teste, executar ações no console e comparar os eventos recebidos com a trilha local.

**Cenários de aceite**:

1. **Dado** um destino configurado e ativo, **Quando** uma ação auditada acontece, **Então** o evento chega ao destino com os mesmos campos da trilha local.
2. **Dado** o botão "Enviar evento de teste", **Quando** acionado, **Então** o destino recebe um evento identificado como teste e a tela mostra sucesso ou o erro de conexão.
3. **Dado** filtros por categoria, **Quando** uma categoria está desmarcada, **Então** seus eventos não são enviados e continuam na trilha local.

---

### Jornada do Usuário 2 - Não perder eventos quando o destino cai (Prioridade: P1)

O SIEM fica fora do ar por uma hora; quando volta, recebe tudo o que aconteceu no período.

**Valor desta prioridade**: lacunas na trilha invalidam a auditoria para conformidade.

**Teste independente**: derrubar o coletor, gerar eventos, religá-lo e conferir que todos chegaram, sem duplicar na trilha local.

**Cenários de aceite**:

1. **Dado** o destino indisponível, **Quando** eventos são gerados, **Então** o cursor do destino para e a tela mostra a situação do destino e o tamanho da fila (eventos da trilha ainda não entregues).
2. **Dado** o destino de volta, **Quando** a fila é processada, **Então** todos os eventos chegam, na ordem em que foram gravados na trilha, cada um com o horário original e identificador único para deduplicação no destino.
3. **Dado** uma indisponibilidade maior que 72 horas, **Quando** eventos expiram da fila, **Então** a lacuna é registrada na auditoria, aparece como aviso persistente na tela do destino e é contada numa métrica.

---

### Jornada do Usuário 3 - Enviar por OpenTelemetry (Prioridade: P2)

A empresa já centraliza logs com OpenTelemetry e prefere receber a auditoria pelo mesmo coletor.

**Valor desta prioridade**: evita um segundo canal de ingestão.

**Teste independente**: configurar um coletor OpenTelemetry de teste e repetir os cenários da jornada 1.

**Cenários de aceite**:

1. **Dado** um destino OpenTelemetry, **Quando** eventos são gerados, **Então** chegam como registros de log com atributos equivalentes aos campos da trilha.

---

## Casos de Borda

- Certificado do destino inválido ou expirado: o envio para, a fila cresce e a tela mostra o motivo; nada é enviado sem TLS válido.
- Mais de um destino: cada um tem fila e situação próprias.
- Volume alto (ações em lote): o envio é agrupado sem atrasar a resposta das ações.
- Alteração de filtros: vale para eventos novos; não reenvia o passado.
- Licença restrita: os destinos ativos continuam recebendo; criar, editar, reativar e testar respondem `403 license_*` e a tela explica a restrição sem esconder a situação dos destinos.
- Licença sem a capability `audit-siem-export` (ou instalação CE): a seção não aparece no console e as rotas respondem como CE.
- Várias réplicas da API: só uma envia para cada destino; a troca de réplica retoma do cursor, e eventos repetidos levam o mesmo identificador para deduplicação no destino.
- Destino criado com a trilha já cheia: o cursor começa no evento mais recente, sem reenviar o passado.

## Requisitos (obrigatório)

### Requisitos Funcionais

- **FR-001**: O sistema DEVE exportar continuamente os eventos da trilha de auditoria para destinos syslog (com TLS) e OpenTelemetry logs.
- **FR-002**: Os eventos exportados DEVEM ter os mesmos campos e a mesma sanitização da trilha local; conteúdo de terminal, variáveis e segredos NÃO DEVEM ser enviados.
- **FR-003**: O envio DEVE ser pelo menos uma vez, com identificador único por evento. Cada destino DEVE retomar do ponto em que parou, lendo da própria trilha local, sem cópia dos eventos em outra fila; eventos pendentes há mais de 72 horas DEVEM ser pulados, registrando a lacuna.
- **FR-004**: A trilha local NÃO DEVE depender do sucesso do envio.
- **FR-005**: Usuários com `audit-export:manage` DEVEM configurar destinos, protocolo, filtros por categoria e enviar evento de teste em Configurações; `audit-export:read` DEVE bastar para ver destinos, situação e fila. As decisões vêm do Keycloak no recurso `urn:dokpod:installation` e a falha dele nega. Segredos de conexão vêm de secret montado.
- **FR-006**: A tela DEVE mostrar a situação de cada destino, a hora do último envio, o tamanho da fila e o atraso do pendente mais antigo; lacunas DEVEM gerar evento de auditoria, aviso persistente na tela e métrica (não uma regra do motor de alertas da spec 016, que é por ambiente).
- **FR-007**: O recurso DEVE existir só na edição Business e só com a capability `audit-siem-export` na licença; criar, editar, reativar e testar destino DEVEM passar pela verificação de licença da spec 031.
- **FR-008**: Com a licença restrita, o envio aos destinos já ativos NÃO DEVE parar; só as operações do FR-007 DEVEM ser recusadas com o código de licença correspondente.
- **FR-009**: Os protocolos DEVEM ser syslog RFC 5424 sobre TLS (RFC 5425, contagem de octetos) e OTLP/HTTP com protobuf sobre TLS; até 5 destinos por instalação; com várias réplicas da API, cada destino DEVE ser enviado por uma réplica de cada vez, e outra DEVE assumir se ela parar.

## Entidades Principais

- **Destino de exportação**: nome, protocolo (`syslog` ou `otlp`), endereço, referência do secret de conexão, filtros por categoria, ativo ou não, situação, último envio; no máximo 5 por instalação.
- **Cursor de entrega**: uma linha por destino com o último evento entregue da trilha, a hora do último envio e a última falha; o tamanho da fila e a idade do mais antigo são calculados a partir dele (formerly referred to as "Fila de exportação").

## Critérios de Sucesso (obrigatório)

### Resultados Mensuráveis

- **SC-001**: Com o destino disponível, 95% dos eventos chegam em até 10 segundos.
- **SC-002**: Após 1 hora de indisponibilidade, 100% dos eventos do período chegam ao destino.
- **SC-003**: A varredura por canário não encontra segredos nem conteúdo de terminal nos eventos exportados.
- **SC-004**: Em 30 execuções de uma ação em lote de 100 itens, o p95 com a exportação ligada fica no máximo 5% acima do p95 com ela desligada.
- **SC-005**: Com a licença restrita, 100% dos eventos continuam chegando aos destinos ativos, e 100% das tentativas de criar, editar ou testar destino são recusadas com código de licença.
- **SC-006**: Com duas réplicas da API, cada evento sai uma vez por destino em operação normal; numa troca de réplica, as repetições têm o mesmo identificador.

## Premissas

- Edição: comercial, pela capability `audit-siem-export` da licença (planos Teams e Business, variante A′ do ADR 2026-0007); a exportação CSV da spec 017 continua na CE.
- A fila durável é o cursor sobre `audit_events` no PostgreSQL existente, sem novo datastore nem tabela de crescimento alto (constituição, princípio V; regra de particionamento atendida pela própria trilha).
- Fora do escopo: webhooks genéricos, envio de métricas e traces ao SIEM e retenção configurável da trilha local.
