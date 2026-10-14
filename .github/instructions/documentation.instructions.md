---
name: "Documentação"
description: "Use ao criar ou alterar README, documentação técnica, runbook, ADR, plano ou contrato documentado."
applyTo: "**/*.md, docs/**, contracts/**/*.yaml, contracts/**/*.json, contracts/**/*.proto"
---

# Documentação

- Escreva em português brasileiro, com termos técnicos consistentes.
- Isso inclui specs, planos, tarefas, checklists e relatórios de Bug Fixing. Preserve comandos, identificadores e labels literais exigidos pelas ferramentas.
- O README raiz é autoridade para visão, escopo e princípios.
- A constituição `.specify/memory/constitution.md` registra princípios duradouros; `.github/` define procedimentos; `specs/<feature>/spec.md`, `plan.md` e `tasks.md` são os artefatos exclusivos de mudanças da aplicação.
- Não duplique conteúdo extenso; use links entre documentos proprietários.
- ADRs vivem em `docs/adr`, usam `AAAA-NNNN-titulo.md` e começam como `proposed`.
- ADRs registram decisões duradouras justificadas por um plan/tarefa; não substituem requisitos, tarefas ou autorização. Registre origem, revisor humano e referências.
- `docs/plan` está congelado e `.github/PLAN_TEMPLATE.md` está obsoleto. Preserve histórico e evidências dos cinco planos; os seis conjuntos migrados em `specs/README.md` são drafts pendentes de revisão, não autorização de implementação.
- IA não marca ADR como `accepted` nem artefato como aprovado sem decisão humana explícita. Checkboxes de tarefas registram execução validada, não aprovação.
- Features seguem Spec Kit com `/speckit-analyze` antes da autorização de implementação; defeitos seguem Bug Fixing oficial com assessment, fix e test em `.specify/bugs/<slug>/`.
- README, arquitetura, segurança, constituição e ADRs aceitos restringem specs. Conflitos exigem decisão humana; não crie fonte paralela de requisitos em guias, prompts ou Issues.
- Runbooks incluem sinais, diagnóstico, mitigação, recuperação e escalonamento.
- Comandos informam diretório, pré-requisitos, resultado esperado e impacto.
- Exemplos usam valores fictícios; nunca inclua secret, host ou dado real.
- Registre limitações, matriz suportada, riscos e validações não executadas.
- Links locais devem resolver e headings devem ser únicos.
- Diferencie requisito, proposta, decisão aceita e comportamento implementado.