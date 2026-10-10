---

description: "Template de tarefas para implementação de funcionalidades"
---

# Tarefas: [FEATURE NAME]

**Entrada**: Documentos de design em `/specs/[###-feature-name]/`

> Redija todas as tarefas e descrições em português brasileiro (pt-BR), preservando identificadores, comandos e caminhos técnicos.

**Pré-requisitos**: plan.md (obrigatório), spec.md (obrigatório para histórias), research.md,
data-model.md e contracts/

**Testes**: Toda mudanca de comportamento deve incluir tarefas de teste relevantes, conforme a
constitution do projeto. Testes podem ser omitidos para alteracoes sem mudanca de comportamento.

**Organização**: As tarefas são agrupadas por história de usuário para permitir implementação e
validação independentes.

## Formato: `[ID] [P?] [Story] Descricao`

- **[P]**: Pode ser executada em paralelo (arquivos diferentes, sem dependencias)
- **[Story]**: História de usuário relacionada (por exemplo, US1, US2, US3)
- Inclua caminhos exatos dos arquivos nas descricoes

## Convencoes de Caminhos

- **Dokpod**: hosts em `backend/apps/`, bibliotecas em `backend/libs/`, testes em `backend/tests/`, frontend em `frontend/web/`, `frontend/libs/` e `frontend/tests/`, contratos em `contracts/` e operação em `deploy/`
- Use a estrutura real definida em `plan.md`; não invente projetos ou diretórios.

<!--
  ============================================================================
  IMPORTANTE: As tarefas abaixo sao apenas exemplos ilustrativos.

  O comando /speckit-tasks DEVE substitui-las por tarefas reais, baseadas em:
  - Historias de usuario de spec.md e suas prioridades (P1, P2, P3...)
  - Requisitos e decisoes de plan.md
  - Entidades de data-model.md
  - Endpoints e contratos de contracts/

  As tarefas DEVEM ser organizadas por historia para permitir implementacao, teste e entrega
  independentes.

  NAO mantenha estas tarefas de exemplo no arquivo tasks.md gerado.
  ============================================================================
-->

## Fase 1: Preparação (Infraestrutura Compartilhada)

**Objetivo**: Inicialização e estrutura básica do projeto.

- [ ] T001 Preparar a estrutura conforme o plano de implementação
- [ ] T002 Configurar os componentes necessarios para a funcionalidade
- [ ] T003 [P] Configurar ferramentas de análise e formatação, se aplicável

---

## Fase 2: Fundação (Pré-requisitos Bloqueadores)

**Objetivo**: Concluir a infraestrutura que DEVE estar pronta antes das histórias de usuário.

**CRÍTICO**: Nenhuma história de usuário pode começar antes da conclusão desta fase.

Exemplos de tarefas de fundação (ajuste ao escopo real):

- [ ] T004 Preparar schema e migration do banco, se necessário
- [ ] T005 [P] Ajustar autorização, se necessário
- [ ] T006 [P] Ajustar rotas ou middleware, se necessário
- [ ] T007 Criar os modelos ou entidades compartilhados necessários
- [ ] T008 Configurar tratamento de erros e logging, se necessário
- [ ] T009 Preparar a configuração de ambiente, se necessário

**Checkpoint**: Fundação concluída; as histórias podem ser iniciadas conforme suas dependências.

---

## Fase 3: História de Usuário 1 - [Título] (Prioridade: P1) MVP

**Objetivo**: [Descreva brevemente o valor entregue por esta historia.]

**Teste independente**: [Explique como validar esta historia isoladamente.]

### Testes da História de Usuário 1

> Escreva os testes antes da implementação e confirme que falham para o comportamento ausente.

- [ ] T010 [P] [US1] Testar o contrato de [endpoint] em [caminho real do teste]
- [ ] T011 [P] [US1] Testar a jornada [nome] em [caminho real do teste]

### Implementação da História de Usuário 1

- [ ] T012 [P] [US1] Criar ou ajustar [componente] em [caminho real]
- [ ] T013 [P] [US1] Criar ou ajustar [componente] em [caminho real]
- [ ] T014 [US1] Implementar [comportamento] em [caminho real], dependendo de T012 e T013
- [ ] T015 [US1] Implementar [endpoint ou fluxo] em [caminho real]
- [ ] T016 [US1] Adicionar validação e tratamento de erros necessários
- [ ] T017 [US1] Adicionar logging operacional necessário

**Checkpoint**: A história deve estar funcional e ser validável de forma independente.

---

## Fase 4: História de Usuário 2 - [Título] (Prioridade: P2)

**Objetivo**: [Descreva brevemente o valor entregue por esta historia.]

**Teste independente**: [Explique como validar esta historia isoladamente.]

### Testes da História de Usuário 2

- [ ] T018 [P] [US2] Testar o contrato de [endpoint] em [caminho real do teste]
- [ ] T019 [P] [US2] Testar a jornada [nome] em [caminho real do teste]

### Implementação da História de Usuário 2

- [ ] T020 [P] [US2] Criar ou ajustar [componente] em [caminho real]
- [ ] T021 [US2] Implementar [comportamento] em [caminho real]
- [ ] T022 [US2] Implementar [endpoint ou fluxo] em [caminho real]
- [ ] T023 [US2] Integrar com componentes da História de Usuário 1, se necessário

**Checkpoint**: As histórias 1 e 2 devem funcionar e ser validáveis de forma independente.

---

## Fase 5: História de Usuário 3 - [Título] (Prioridade: P3)

**Objetivo**: [Descreva brevemente o valor entregue por esta historia.]

**Teste independente**: [Explique como validar esta historia isoladamente.]

### Testes da História de Usuário 3

- [ ] T024 [P] [US3] Testar o contrato de [endpoint] em [caminho real do teste]
- [ ] T025 [P] [US3] Testar a jornada [nome] em [caminho real do teste]

### Implementação da História de Usuário 3

- [ ] T026 [P] [US3] Criar ou ajustar [componente] em [caminho real]
- [ ] T027 [US3] Implementar [comportamento] em [caminho real]
- [ ] T028 [US3] Implementar [endpoint ou fluxo] em [caminho real]

**Checkpoint**: Todas as histórias devem estar funcionais e ser validáveis de forma independente.

---

[Adicione outras fases de história de usuário seguindo o mesmo padrão.]

---

## Fase N: Acabamento e Aspectos Transversais

**Objetivo**: Concluir melhorias que afetam varias historias.

- [ ] TXXX [P] Atualizar documentação em pt-BR
- [ ] TXXX Revisar limpeza e refatoração do código
- [ ] TXXX Avaliar desempenho nos fluxos afetados
- [ ] TXXX [P] Adicionar testes unitários relevantes
- [ ] TXXX Revisar segurança dos fluxos alterados
- [ ] TXXX Executar as validações descritas em quickstart.md, se houver

---

## Dependências e Ordem de Execução

### Dependências entre Fases

- **Preparação (Fase 1)**: Sem dependências; pode começar imediatamente.
- **Fundação (Fase 2)**: Depende da preparação e bloqueia as histórias de usuário.
- **Histórias (Fase 3+)**: Dependem da fundação e podem seguir em paralelo ou por prioridade.
- **Acabamento (fase final)**: Depende da conclusão das histórias selecionadas.

### Dependências entre Histórias

- **História 1 (P1)**: Pode iniciar após a fundação; não depende de outras histórias.
- **História 2 (P2)**: Pode iniciar após a fundação e deve permanecer testável de forma independente.
- **História 3 (P3)**: Pode iniciar após a fundação e deve permanecer testável de forma independente.

### Dentro de Cada História

- Testes de comportamento DEVEM ser escritos e falhar antes da implementação correspondente.
- Implemente os componentes na ordem exigida por suas dependencias reais.
- Conclua a validação independente da história antes de avançar quando houver dependência de prioridade.

### Oportunidades de Paralelismo

- Tarefas [P] sem dependencias entre si podem ser executadas em paralelo.
- Histórias distintas podem ser paralelizadas após a conclusão da fundação, se a equipe permitir.
- Confirme conflitos de arquivos e dependencias antes de paralelizar tarefas.

---

## Exemplo de Paralelismo: História de Usuário 1

```bash
# Execute testes independentes da Historia de Usuario 1 em paralelo:
Task: "Testar o contrato de [endpoint] em [caminho real do teste]"
Task: "Testar a jornada [nome] em [caminho real do teste]"

# Preparar componentes independentes em paralelo:
Task: "Criar ou ajustar [componente 1] em [caminho real]"
Task: "Criar ou ajustar [componente 2] em [caminho real]"
```

---

## Estrategia de Implementacao

### MVP Primeiro (Somente Historia de Usuario 1)

1. Conclua a Fase 1: Preparação.
2. Conclua a Fase 2: Fundação, que bloqueia as demais histórias.
3. Conclua a Fase 3: História de Usuário 1.
4. **PARE E VALIDE**: Teste a História 1 de forma independente.
5. Implante ou demonstre se estiver pronta e autorizado.

### Entrega Incremental

1. Conclua Preparação e Fundação.
2. Adicione a História 1, valide-a e prepare a entrega do MVP.
3. Adicione e valide a História 2.
4. Adicione e valide a História 3.
5. Cada história deve agregar valor sem regredir as anteriores.

### Estratégia para Trabalho em Paralelo

Com vários desenvolvedores:

1. A equipe conclui Preparação e Fundação em conjunto.
2. Após a fundação, distribua histórias sem dependências entre membros da equipe.
3. Cada história deve ser concluída e integrada de forma verificável.

---

## Observações

- Tarefas [P] devem atuar em arquivos distintos e não possuir dependências mútuas.
- O identificador [Story] relaciona cada tarefa a uma história para rastreabilidade.
- Cada história deve ser concluível e testável de forma independente.
- Confirme a falha dos testes antes de implementar o comportamento correspondente.
- Evite tarefas vagas, conflitos no mesmo arquivo e dependências que quebrem a independência.
