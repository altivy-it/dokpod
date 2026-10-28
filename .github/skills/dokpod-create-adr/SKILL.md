---
name: dokpod-create-adr
description: "Use para criar ou atualizar ADR proposed do Dokpod vinculado a uma decisão justificada por /speckit-plan, mediante autorização humana explícita; não implementa código nem aceita a decisão."
argument-hint: "Referência a /speckit-plan, decisão, autorização e revisor humano"
user-invocable: true
disable-model-invocation: true
---

# Criar ADR Proposto do Dokpod

## Papel e Pré-requisitos

Antes de invocar esta skill, selecione explicitamente o agente [Dokpod Solution Architect](../../agents/solution-architect.agent.md) no Session Target Copilot. A skill não seleciona agente ou modelo, não altera ferramentas e não concede autorização de implementação. Se o papel ou as capacidades necessárias não puderem ser comprovados no harness, informe o bloqueio e não execute a operação dependente.

Considere a solicitação e as referências fornecidas pelo usuário no chat. Sem planejamento, decisão justificada, autorização e revisor identificável, informe o que falta antes de escrever. O planejamento é o artefato Spec Kit ativo; não retome planos legados congelados nem crie fluxo concorrente.

## Procedimento

Avalie a decisão solicitada conforme o plano mestre, arquitetura, segurança e ADRs existentes.

Exija referência ao artefato e à decisão de `/speckit-plan` que justificam o ADR, além da autorização humana explícita. Sem isso, retorne ao planejamento oficial; esta skill não cria requisitos, tarefas ou planos paralelos. Vincule o ADR ao artefato de origem.

1. Confirme que a decisão é arquitetural e duradoura; caso contrário, recomende o artefato adequado.
2. Compare no máximo três opções plausíveis com segurança, operação, compatibilidade, custo e reversibilidade.
3. Recomende a opção mais simples que cumpra os requisitos e defina como falsificá-la.
4. Se justificado, crie `docs/adr/AAAA-NNNN-titulo.md` seguindo `.github/ADR_TEMPLATE.md` e a sequência anual existente.
5. Mantenha status `proposed`, registre origem como IA assistida e identifique revisão humana pendente.

Não implemente código nem marque a decisão como `accepted`.

## Referências Canônicas

- [Modelo de ADR](../../ADR_TEMPLATE.md), sem copiar seu conteúdo para esta skill.
- [Regras de arquitetura](../../instructions/architecture.instructions.md) e [documentação](../../instructions/documentation.instructions.md).
- [Constituição](../../../.specify/memory/constitution.md), [visão geral técnica](../../../docs/visao-geral-tecnica.md), [arquitetura](../../../docs/arquitetura.md) e [segurança](../../../docs/seguranca.md).

Os caminhos do procedimento são relativos à raiz do Dokpod. Preserve alterações locais e a sequência anual; não sobrescreva ADR existente. Relate a referência de origem, alternativas, recomendação falsificável e proposta produzida, ou o bloqueio e as informações ausentes, sem apresentar revisão pendente como aceite humano.