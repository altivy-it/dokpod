---
name: "Verificação Visual Frontend"
description: "Use ao criar, alterar ou substituir uma tela ou componente Angular; define o loop obrigatório de screenshot, análise e ajuste até a UI/UX estar correta."
applyTo: "frontend/**"
---

# Verificação visual

- Toda tela ou componente Angular criado, alterado ou substituído passa por um loop de verificação visual antes de ser considerado pronto: capturar screenshot, analisar contra os critérios abaixo e ajustar até não haver defeito relevante.
- Sirva a aplicação em container e abra a rota afetada somente pela borda de um nginx também containerizado (`open_browser_page`, `navigate_page`). Para o E2E real, use o gateway documentado em `deploy/e2e/README.md`; com respostas simuladas, configure nginx antes de testar. O alias `ng serve` pode iniciar o Angular em container, mas sua porta não é URL de navegador ou Playwright. Não reutilize servidor sem confirmar que o acesso passa pelo proxy.
- Capture screenshot em pelo menos dois viewports por iteração: desktop (largura ≥ 1280px) e mobile (largura ≤ 480px), com `screenshot_page`. Capture também qualquer estado relevante da tela (loading, vazio, erro, forbidden, foco, validação) antes de concluir.
- Analise cada screenshot contra estes critérios antes de decidir que a tela está correta:
  - usa exclusivamente componentes PO UI, conforme `frontend-po-ui.instructions.md`; nenhum elemento parece reimplementado ou de outra biblioteca;
  - layout sem sobreposição, corte de texto, quebra inesperada ou espaçamento inconsistente em nenhum dos viewports capturados;
  - hierarquia visual, contraste e densidade coerentes com o tema PO UI do Dokpod (`docs/frontend.md`), sem estética genérica de SaaS;
  - estados de loading, vazio, erro, forbidden, indisponibilidade e operação pendente são visualmente distintos e legíveis quando aplicáveis;
  - foco visível e ordem de navegação plausível, cruzando a captura com `read_page`;
  - console e rede sem erro novo introduzido pela mudança.
- Se algum critério falhar, corrija o código, capture novamente e reanalise. Não entregue uma tela com defeito visual conhecido e não corrigido.
- Limite o loop a no máximo 3 iterações de ajuste. Se o terceiro ciclo ainda apresentar defeito, pare, registre o que foi tentado, a hipótese restante e pergunte ao responsável antes de continuar ajustando às cegas.
- Guarde a captura final de cada viewport analisado e cite, no resumo de entrega, quais telas, estados e viewports foram verificados e o resultado da análise.
- Este loop é verificação de desenvolvimento; não substitui Vitest nem Playwright exigidos por `testing.instructions.md`.
- Nunca capture nem registre token, secret, container real, ambiente de produção ou log sensível; use somente dados sintéticos do ambiente de desenvolvimento local.

Consulte `frontend-angular.instructions.md`, `frontend-po-ui.instructions.md` e `docs/frontend.md`.
