# Especificação da Feature: Exportação de auditoria para SIEM

**Branch da feature**: `025-029-roteiro-recursos`

**Criada em**: 2026-10-27

**Estado**: `draft` - aguardando revisão humana.

**Entrada**: Pedido do solicitante em 2026-10-27: "Itens de referência que ainda não estão no dokpod, inclua todos no roadmap", item 3 de 5: exportação contínua da auditoria por syslog e OpenTelemetry.

**Depende de**: [017](../017-auditoria/spec.md) (trilha de auditoria e exportação CSV), [022](../022-configuracoes/spec.md) (Configurações), [ADR 2026-0004](../../docs/adr/2026-0004-auditoria-persistente.md) (auditoria persistente).

## Esclarecimentos

### Sessão 2026-10-27 (recomendações adotadas, sujeitas à aprovação)

- P: Quais protocolos? → R: Syslog (formato estruturado padrão, com transporte TLS) e OpenTelemetry logs, os dois mais aceitos por ferramentas de SIEM. Webhooks genéricos ficam fora desta entrega.
- P: O que é enviado? → R: Os mesmos eventos da trilha de auditoria (spec 017), com os mesmos campos e a mesma sanitização; nunca conteúdo de terminal, valores de variáveis ou segredos.
- P: E se o destino cair? → R: Os eventos ficam em fila durável no plano de controle e são reenviados na volta, pelo menos uma vez, por até 72 horas; depois disso a lacuna é registrada e sinalizada. A trilha local nunca depende do envio.
- P: Quem configura? → R: Administradores, em Configurações, com destino, protocolo, filtros por categoria e teste de envio. Certificados e tokens do destino vêm de secret montado, não do console.

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

1. **Dado** o destino indisponível, **Quando** eventos são gerados, **Então** ficam em fila e a tela mostra a situação do destino e o tamanho da fila.
2. **Dado** o destino de volta, **Quando** a fila é processada, **Então** todos os eventos chegam em ordem por horário, cada um com identificador único para deduplicação no destino.
3. **Dado** uma indisponibilidade maior que 72 horas, **Quando** eventos expiram da fila, **Então** a lacuna é registrada na auditoria e aparece como alerta.

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

## Requisitos (obrigatório)

### Requisitos Funcionais

- **FR-001**: O sistema DEVE exportar continuamente os eventos da trilha de auditoria para destinos syslog (com TLS) e OpenTelemetry logs.
- **FR-002**: Os eventos exportados DEVEM ter os mesmos campos e a mesma sanitização da trilha local; conteúdo de terminal, variáveis e segredos NÃO DEVEM ser enviados.
- **FR-003**: O envio DEVE ser pelo menos uma vez, com fila durável de até 72 horas por destino e identificador único por evento.
- **FR-004**: A trilha local NÃO DEVE depender do sucesso do envio.
- **FR-005**: Administradores DEVEM configurar destinos, protocolo, filtros por categoria e enviar evento de teste em Configurações; segredos de conexão vêm de secret montado.
- **FR-006**: A tela DEVE mostrar a situação de cada destino, a hora do último envio e o tamanho da fila; lacunas DEVEM gerar evento de auditoria e alerta.

## Entidades Principais

- **Destino de exportação**: nome, protocolo, endereço, filtros, situação, último envio.
- **Fila de exportação**: eventos pendentes por destino, idade do mais antigo.

## Critérios de Sucesso (obrigatório)

### Resultados Mensuráveis

- **SC-001**: Com o destino disponível, 95% dos eventos chegam em até 10 segundos.
- **SC-002**: Após 1 hora de indisponibilidade, 100% dos eventos do período chegam ao destino.
- **SC-003**: A varredura por canário não encontra segredos nem conteúdo de terminal nos eventos exportados.
- **SC-004**: Ações em lote de 100 itens não ficam mais lentas com a exportação ligada.

## Premissas

- Edição prevista: comercial (Business), como "exportação de auditoria" ([licenciamento](../../docs/licenciamento.md#business)); a exportação CSV da spec 017 continua na CE.
- A fila durável usa o PostgreSQL existente, sem novo datastore (constituição, princípio V).
- Fora do escopo: webhooks genéricos, envio de métricas e traces ao SIEM e retenção configurável da trilha local.
