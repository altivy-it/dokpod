---
name: "Criar ADR"
description: "Apoia uma decisão justificada por /speckit-plan com ADR auxiliar proposed, mediante autorização explícita."
argument-hint: "Referência a /speckit-plan, decisão, autorização e revisor humano"
agent: "Dokpod Solution Architect"
---

Avalie a decisão solicitada conforme o plano mestre, arquitetura, segurança e ADRs existentes.

Exija referência ao artefato e à decisão de `/speckit-plan` que justificam o ADR, além da autorização humana explícita. Sem isso, retorne ao planejamento oficial; este prompt não cria requisitos, tarefas ou planos paralelos. Vincule o ADR ao artefato de origem.

1. Confirme que a decisão é arquitetural e duradoura; caso contrário, recomende o artefato adequado.
2. Compare no máximo três opções plausíveis com segurança, operação, compatibilidade, custo e reversibilidade.
3. Recomende a opção mais simples que cumpra os requisitos e defina como falsificá-la.
4. Se justificado, crie `docs/adr/AAAA-NNNN-titulo.md` seguindo `.github/ADR_TEMPLATE.md` e a sequência anual existente.
5. Mantenha status `proposed`, registre origem como IA assistida e identifique revisão humana pendente.

Não implemente código nem marque a decisão como `accepted`.