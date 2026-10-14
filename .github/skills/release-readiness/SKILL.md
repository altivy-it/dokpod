---
name: release-readiness
description: "Avalia gates de release rastreáveis ao Spec Kit do Dokpod, preservando qualidade, segurança, N/N-1, agentes Linux/Windows, engines e operação."
argument-hint: "Versão candidata, base anterior, plataformas e ambiente-alvo"
---

# Release Readiness

Esta skill reúne evidências como gate auxiliar do Spec Kit; não define requisitos,
planos, tarefas ou correções concorrentes. Validações só podem ser executadas
quando previstas em tarefa/remediação Spec Kit e explicitamente autorizadas;
caso contrário, inspecione evidências existentes e marque `NOT RUN`. Quando usada
por reviewer read-only, não execute comandos. Não publica, envia imagens ou faz
deploy; essas ações exigem solicitação explícita separada.

## 1. Definir release

- tarefas/remediações Spec Kit, autorização humana e evidências vinculadas à versão candidata;
- versão SemVer e base comparada;
- features, correções, breaking changes e deprecações;
- versões de servidor/agente N/N-1 e plataformas suportadas;
- engines/capabilities declaradas na matriz Docker/Podman x Linux/Windows, com versões, suporte, execução e lacunas, e riscos aceitos formalmente.

## 2. Validar produto

- restore/install reproduzível, format, lint e análise estática;
- builds de produção Angular e .NET;
- testes unitários, integração real, contrato, arquitetura e Playwright;
- journal-before-ack, reinício, reconexão, replay durável, fencing, idempotência e operações destrutivas;
- nenhum teste ignorado novo ou flakiness não explicado.

## 3. Validar segurança e dados

- autorização horizontal, Keycloak indisponível e agente falso/revogado testados;
- nenhum secret, token, certificado privado ou dado sensível em Git, logs ou imagens;
- migrations expand-contract testadas em PostgreSQL real;
- reconciliação do inventário a partir dos engines verificada;
- dependências/licenças, SBOM, vulnerabilidades e provenance revisados.

## 4. Validar distribuição

- imagens passam BuildKit, usam referências reproduzíveis, usuário não root e filesystem read-only quando aplicável;
- entrypoint, labels, portas, mounts e health checks foram inspecionados;
- agente Linux passa smoke test com engine real;
- agente Windows instala, executa e remove em host sem runtime .NET, com conta e ACL corretas;
- upgrade/rollback preserva identidade, journal e compatibilidade N/N-1.

## 5. Validar operação

- configuração, secrets, certificados e rotação estão documentados;
- health, métricas, traces e alertas reconhecem falhas novas;
- rollout, pós-deploy, rollback, backup e restore têm evidência;
- somente superfícies previstas estão expostas; sockets de engine permanecem locais.

## 6. Decisão

Classifique cada gate como `PASS`, `FAIL`, `WAIVED` ou `NOT RUN`. `GO` exige ausência de `FAIL`, ausência de `NOT RUN` obrigatório e waivers com responsável e prazo; caso contrário, recomende `NO-GO`.

Informe matriz de gates, referências às tarefas/remediações e à autorização,
evidências/comandos, bloqueios, waivers e risco residual. `GO` é parecer técnico,
não aprovação humana ou autorização de publicação. Pendências voltam ao Spec Kit
ou Bug Fixing oficial; nunca crie escopo nem corrija durante o gate.