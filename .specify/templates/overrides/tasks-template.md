---
description: "Modelo de tarefas para implementação de feature"
---

# Tarefas: [FEATURE NAME]

**Entrada**: Artefatos de desenho em `/specs/[###-feature-name]/`

**Pré-requisitos**: `plan.md` e `spec.md`; `research.md`, `data-model.md` e `contracts/` quando aplicáveis.

**Idioma**: Escreva descrições, objetivos e explicações integralmente em português brasileiro (pt-BR). Preserve IDs, marcadores, comandos, código e caminhos literais.

**Testes**: Inclua tarefas de teste sempre que exigidas pelos riscos, critérios de aceite ou constituição. Não omita testes necessários apenas porque não foram pedidos literalmente.

**Organização**: Agrupe as tarefas por jornada de usuário para permitir incrementos independentes e verificáveis.

## Formato: `[ID] [P?] [Story?] Descrição com caminho do arquivo`

- **[P]**: Use somente quando a tarefa puder executar em paralelo sem dependências pendentes e sem disputar arquivos.
- **[Story]**: Use `[US1]`, `[US2]`, `[US3]` etc. nas tarefas de uma jornada.
- Inclua caminhos exatos dos arquivos nas descrições de implementação.

## Convenções de Caminhos

- Backend: `backend/apps/`, `backend/libs/` e `backend/tests/`.
- Frontend: `frontend/web/`, `frontend/libs/` e `frontend/tests/`.
- Contratos: `contracts/`.
- Deployment e operação: `deploy/`.
- Ajuste os caminhos à estrutura real registrada em `plan.md`.

<!--
  As tarefas abaixo são somente exemplos estruturais. Substitua-as por tarefas
  concretas derivadas da spec e do plano; não mantenha exemplos como entregas.
-->

## Fase 1: Preparação

**Objetivo**: Preparar apenas a estrutura compartilhada necessária.

- [ ] T001 Preparar a estrutura da feature conforme o plano de implementação
- [ ] T002 [P] Atualizar documentação em [caminho real]

---

## Fase 2: Fundação (Pré-requisitos Bloqueantes)

**Objetivo**: Concluir os pré-requisitos compartilhados antes das jornadas.

- [ ] T003 Preparar a migration necessária em [caminho real]
- [ ] T004 [P] Preparar os contratos compartilhados em [caminho real]

**Ponto de verificação**: A fundação está pronta para iniciar as jornadas autorizadas.

---

## Fase 3: Jornada do Usuário 1 - [Título] (Prioridade: P1)

**Objetivo**: [Valor entregue por esta jornada.]

**Teste independente**: [Critério que demonstra o comportamento desta jornada.]

### Testes da Jornada do Usuário 1

- [ ] T005 [P] [US1] Adicionar teste de contrato em [caminho real]
- [ ] T006 [P] [US1] Adicionar teste de integração em [caminho real]

### Implementação da Jornada do Usuário 1

- [ ] T007 [P] [US1] Implementar regra de domínio em [caminho real]
- [ ] T008 [US1] Integrar o caso de uso em [caminho real]
- [ ] T009 [US1] Implementar a interface ou endpoint em [caminho real]

**Ponto de verificação**: A jornada funciona e pode ser validada independentemente.

---

## Fase 4: Jornada do Usuário 2 - [Título] (Prioridade: P2)

**Objetivo**: [Valor entregue por esta jornada.]

**Teste independente**: [Critério que demonstra o comportamento desta jornada.]

- [ ] T010 [P] [US2] Adicionar teste necessário em [caminho real]
- [ ] T011 [US2] Implementar o comportamento em [caminho real]

**Ponto de verificação**: As jornadas 1 e 2 continuam independentes e verificáveis.

---

## Fase N: Acabamento e Aspectos Transversais

**Objetivo**: Concluir aspectos transversais exigidos pelo escopo e pela constituição.

- [ ] TXXX [P] Atualizar documentação do projeto em [caminho real]
- [ ] TXXX Validar segurança e autorização
- [ ] TXXX Executar testes de desempenho ou recuperação, se aplicáveis
- [ ] TXXX Validar o guia `quickstart.md`

---

## Dependências e Ordem de Execução

### Dependências entre Fases

- **Preparação (Fase 1)**: sem dependências anteriores.
- **Fundação (Fase 2)**: depende da preparação e bloqueia as jornadas que a utilizam.
- **Jornadas (Fase 3+)**: siga as dependências explícitas; paralelize somente tarefas sem conflitos.
- **Acabamento**: depende das jornadas incluídas no escopo autorizado.

### Dependências entre Jornadas

- Registre as dependências reais extraídas da spec e do plano; não presuma independência.

### Oportunidades de Paralelismo

- Marque `[P]` apenas quando arquivos, contratos e dependências permitirem execução independente.

## Estratégia de Implementação

### Incremento Mínimo

1. Conclua preparação e fundação necessárias.
2. Implemente a jornada P1 e valide-a isoladamente.
3. Pare em cada ponto de verificação para analisar resultados antes de ampliar o escopo.

### Entrega Incremental

1. Implemente e valide uma jornada por vez, em ordem de prioridade e dependência.
2. Preserve compatibilidade e evidências das jornadas anteriores.

## Observações

- `[P]` indica paralelismo real; não use o marcador apenas para acelerar o cronograma.
- `[USn]` relaciona cada tarefa à jornada definida em `spec.md`.
- Cada jornada deve ser concluível e testável dentro da tarefa autorizada.
- Marque uma tarefa `[x]` somente após implementar e validar sua evidência; isso não representa aprovação humana.
- Siga os critérios da spec, o desenho de `plan.md` e os princípios da constituição.
