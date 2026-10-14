---
name: "Gerar Testes"
description: "Apoia testes de tarefa ou remediação Spec Kit explicitamente autorizada, sem criar fluxo de implementação independente."
argument-hint: "Artefato Spec Kit, ID da tarefa ou remediação e autorização humana"
agent: "Dokpod Quality Engineer"
---

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