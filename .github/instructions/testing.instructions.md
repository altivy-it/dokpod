---
name: "Testes Dokpod"
description: "Use ao criar ou alterar testes unitários, integração, contrato, Playwright, segurança ou desempenho."
applyTo: "backend/tests/**, frontend/tests/**, frontend/**/*.spec.ts, frontend/**/*.test.ts, **/*Tests.cs"
---

# Testes

- Gere ou altere testes somente para uma tarefa ou remediação Spec Kit explicitamente autorizada; registre a referência e a evidência nos artefatos correspondentes.
- Para engine/protocolo, cubra transições, idempotência, deadline, fencing, journal-before-ack e replay durável após reinício; transporte cobre duplicação, resposta perdida, reconexão e ordem.
- Contratos verificam serialização, evolução N/N-1, tags reservadas, valores desconhecidos e capabilities ausentes.
- Declare a matriz Docker/Podman x Linux/Windows com versões e resultados: engine real cobre sucesso, inexistente, conflito e indisponibilidade; marque combinações não suportadas ou não verificadas explicitamente.
- Smoke de Linux usa agente containerizado; Windows usa Windows Service self-contained, RID, named pipe e ACL em host sem runtime .NET. Segurança cobre operação fora da allowlist e entrada malformada.
- Execute runners de testes em containers. Quando um teste depender de serviços da aplicação em execução (Playwright, smoke HTTP ou integração), mantenha esses serviços em containers e use como URL somente a borda de um proxy reverso nginx containerizado. Para E2E real, use o gateway de `deploy/e2e/README.md`; para testes com respostas simuladas, prepare nginx na rede Docker antes de abrir o navegador. Nunca aponte o runner diretamente para Angular, BFF ou API. Testes isolados sem servidor HTTP não precisam subir nginx. Preserve os testes de instalação do agente Windows no host Windows sem runtime .NET.
- Teste comportamento observável e falhas, não detalhes privados.
- Use xUnit no .NET, Vitest no Angular e Playwright em jornadas.
- Testes de UI cobrem o comportamento Dokpod sobre componentes PO UI (dados, estado, integração); não reteste comportamento interno já validado pela biblioteca (foco, ARIA, teclado do próprio componente).
- O loop de verificação visual (`frontend-visual-verification.instructions.md`) é evidência de desenvolvimento; não substitui Vitest nem Playwright como gate formal.
- Domínio usa testes unitários; adapters usam engines e PostgreSQL reais.
- Não use mocks como evidência principal de compatibilidade Docker/Podman.
- Cubra sucesso, cancelamento, timeout, replay, resposta perdida e reconexão.
- Verifique que duas mutações no mesmo container são serializadas e ambientes independentes progridem em paralelo.
- Contract tests cobrem versões de API e capabilities ausentes.
- Segurança inclui Keycloak indisponível/revogado, token inválido, CSRF, autorização horizontal, agente falso, certificado revogado, SSRF e payload hostil.
- Testes Windows declaram versão do host, RID, conta do serviço e engine e executam sem runtime .NET instalado.
- Fixtures usam apenas dados sintéticos e nunca contêm credenciais.
- Testes de carga e capacidade seguem `.github/PERFORMANCE_TESTING_CRITERIA.md`.
- Teste alterado deve falhar sem a correção e passar com ela.
- Registre comandos e resultados no pull request; não marque gate sem executar.