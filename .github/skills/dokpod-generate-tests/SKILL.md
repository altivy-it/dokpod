---
name: dokpod-generate-tests
description: "Use para produzir evidência de teste de uma tarefa Spec Kit autorizada via /speckit-implement ou de uma remediação autorizada via /speckit-bug-fix no Dokpod; não cria fluxo de implementação independente nem corrige defeitos fora do processo."
argument-hint: "Artefato Spec Kit, ID da tarefa ou remediação e autorização humana"
user-invocable: true
disable-model-invocation: true
---

# Gerar Testes do Dokpod

## Papel e Pré-requisitos

Antes de invocar esta skill, selecione explicitamente o agente [Dokpod Quality Engineer](../../agents/quality-engineer.agent.md) no Session Target Copilot. A skill não seleciona agente ou modelo, não altera ferramentas e não concede autorização de implementação ou de execução. Se o papel ou as capacidades necessárias, como o runner de testes em container, não puderem ser comprovados no harness, informe o bloqueio e não execute a operação dependente; não use o host como alternativa.

Considere a solicitação e as referências fornecidas pelo usuário no chat. Sem artefato Spec Kit, ID da tarefa ou remediação e autorização humana explícita, informe o que falta antes de escrever ou executar testes.

## Procedimento

Produza evidência de teste para o comportamento solicitado.

Confirme referência e autorização da tarefa via `/speckit-implement` ou da remediação via `/speckit-bug-fix`; sem elas, retorne ao processo Spec Kit. Não derive requisitos novos do diff nem corrija defeitos por fora de Bug Fixing. Devolva evidências ao artefato de origem e à verificação `/speckit-bug-test` quando aplicável.

1. Derive critérios e riscos observáveis.
2. Monte uma matriz curta entre risco e nível de teste.
3. Implemente primeiro o teste de maior valor e demonstre que discrimina o comportamento.
4. Cubra erros e bordas relevantes, sem duplicar testes equivalentes.
5. Use Docker/Podman e PostgreSQL reais quando o comportamento depender deles.
6. Para protocolo, inclua compatibilidade N/N-1, duplicação, deadline, fencing e reconexão quando aplicáveis.
7. Execute os testes novos e a suíte relacionada.

Reporte cobertura, comandos/resultados, plataformas verificadas e lacunas.

## Regras de Infraestrutura

As regras vigentes ficam nas instruções canônicas, sem cópia para esta skill:

- [Testes](../../instructions/testing.instructions.md): runners em containers; serviços HTTP só pela borda de um nginx containerizado; matriz Docker/Podman x Linux/Windows; testes do agente Windows no host Windows sem runtime .NET, que são a exceção ao container.
- [Containers](../../instructions/containers.instructions.md) e [PostgreSQL](../../instructions/database-postgresql.instructions.md), quando o comportamento depender deles.
- [Contratos](../../instructions/contracts.instructions.md), para compatibilidade N/N-1 do protocolo.

Distinga a infraestrutura realmente usada nos testes das plataformas apenas citadas por essas regras; inspeção textual não qualifica engine nem Windows.

## Referências Canônicas

- [Constituição](../../../.specify/memory/constitution.md) e [processo de Bug Fixing](../speckit-bug-test/SKILL.md).

Os caminhos do procedimento são relativos à raiz do Dokpod. Relate a referência de origem, a matriz risco-teste, os testes criados, os comandos e resultados, as plataformas verificadas e as lacunas, ou o bloqueio e as informações ausentes, sem apresentar a evidência como aprovação humana da tarefa.
