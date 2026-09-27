# Plano: Desbloqueio da candidata de release

**Status:** proposed
**Data de criação:** 2026-09-25
**Última atualização:** 2026-09-25
**Responsáveis:** equipe Dokpod
**Origem:** IA assistida
**Revisor humano:** Lincoln Zocateli
**Relacionado:** [MVP do Dokpod](mvp.md), [prontidão para release](../release-readiness.md)

## Objetivo

Fechar os gates externos restantes para decidir, com evidências reproduzíveis,
se a candidata Docker Linux pode sair de `NO-GO` para `GO`.

## Contexto e premissas

- Build .NET, 183 testes backend, 23 integrações PostgreSQL reais, 12 testes
  frontend e a suíte E2E autenticada já passaram.
- As quatro imagens têm SBOM CycloneDX e scan Trivy local; não foram encontrados
  `CRITICAL`. A imagem web mantém 34 achados `HIGH` que precisam de triagem.
- O lifecycle real, autorização horizontal, SignalR, revogação/reconexão e
  backup/restore foram validados no laboratório.
- A matriz publicada inicialmente pode limitar-se a Docker Linux. Windows e
  Podman permanecem fora de suporte estável até completar suas qualificações.
- A IA não altera este plano para `approved` nem marca etapas `completed`; isso
  requer decisão ou evidência humana explícita.

## Não escopo

- Não publicar, assinar, promover ou fazer deploy de artefatos neste plano.
- Não declarar suporte estável a Windows ou Podman sem suas provas específicas.
- Não converter carga exploratória em aprovação sem baseline e thresholds
  aprovados antes da execução.

## Dependências e decisões

- Execução do workflow CI requer branch e revisão elegíveis no GitHub.
- Carga nominal requer VM isolada com recursos e baseline definidos.
- Assinatura/provenance requer identidade e política de release aprovadas.
- O escopo inicial deve declarar explicitamente se Windows e Podman são adiados.

## Etapas

### P-01: Executar CI remoto

**Status:** not-started
**Responsável:** equipe Dokpod
**Dependências:** workflow `.github/workflows/ci.yml` revisado e disponível na branch

**Objetivo:** comprovar que o gate automatizado funciona no GitHub, não apenas
localmente.

Entregas:

- execução verde de backend, migrations e testes PostgreSQL, frontend, imagens,
  scans e SBOM;
- artifacts JSON e CycloneDX preservados pelo workflow.

Validação:

- revisar todos os jobs, artifacts e ausência de skips nos testes PostgreSQL;
- corrigir falhas e executar novamente até o workflow terminar verde.

Evidências:

- pendente: URL/ID da execução remota e resumo dos jobs.

### P-02: Triar vulnerabilidades HIGH

**Status:** not-started
**Responsável:** equipe Dokpod com revisão de segurança
**Dependências:** artifacts Trivy da P-01 ou scans atualizados equivalentes

**Objetivo:** resolver ou aceitar formalmente os 34 achados HIGH da imagem web.

Entregas:

- cada achado classificado como aplicável, falso positivo ou não corrigível;
- correções reconstruídas e reescaneadas; exceções com responsável, impacto e
  expiração documentados.

Validação:

- novo scan das quatro imagens;
- nenhum `CRITICAL` corrigível e nenhum `HIGH` sem correção ou aceite formal.

Evidências:

- pendente: relatório de triagem e artifacts do scan final.

### P-03: Executar carga nominal

**Status:** not-started
**Responsável:** equipe de performance/operação
**Dependências:** VM isolada, build identificado, baseline e thresholds aprovados

**Objetivo:** medir capacidade e recuperação do perfil nominal do MVP.

Entregas:

- execução mínima de 30 minutos com 56 agentes, 1.120 containers e 30 usuários;
- registro de CPU, memória, rede, PostgreSQL, conexões, filas e idade da projeção.

Validação:

- aprovar previamente limites p95/p99, taxa de erro e tempo de convergência;
- testar snapshots/deltas, leituras, comandos repetidos, resposta perdida,
  reconexão e recuperação do backlog;
- nenhuma operação não autorizada, perda ou efeito duplicado; backlog retorna
  ao baseline; thresholds atendidos.

Evidências:

- pendente: relatório bruto sanitizado, resumo e identificação da VM/commit.

### P-04: Fechar threat model e supply chain

**Status:** not-started
**Responsável:** equipe Dokpod e revisor humano de segurança
**Dependências:** decisão da matriz inicial e artifacts finais da P-02

**Objetivo:** permitir verificação de origem e integridade dos artefatos da
matriz suportada.

Entregas:

- threat model revisado;
- imagens assinadas por digest e provenance verificável;
- política documentada para emissor, digest, versão e revogação.

Validação:

- verificar assinatura e provenance a partir de um ambiente limpo;
- confirmar que digest alterado, assinatura inválida e emissor inesperado são
  rejeitados.

Evidências:

- pendente: aprovação humana e resultados dos testes positivos e negativos.

### P-05: Qualificar matriz de distribuição

**Status:** not-started
**Responsável:** equipe de release
**Dependências:** matriz Docker Linux/Windows/Podman decidida por humano

**Objetivo:** validar cada capability que for anunciada como suportada.

Entregas:

- Docker Linux permanece na matriz somente após os gates de release;
- para Windows, instalação real em host limpo sem runtime .NET, conta dedicada,
  ACL do named pipe/dados, update e rollback;
- para Podman, testes rootless/rootful, capabilities, mount e diferenças Libpod;
- capability não exercitada permanece `experimental` ou `adiada`.

Validação:

- comprovar operação e remoção sem perda de identidade/journal;
- comprovar que principais não autorizados não acessam pipe/socket.

Evidências:

- pendente: logs sanitizados, checklist por SO/engine e decisão da matriz.

### P-06: Aprovar release

**Status:** not-started
**Responsável:** Lincoln Zocateli/revisor humano designado
**Dependências:** P-01 a P-05 ou decisão humana formal de adiar capabilities

**Objetivo:** decidir GO/NO-GO e congelar o escopo realmente comprovado.

Entregas:

- runbooks de backup/restore, atualização, rollback, incidentes e operação;
- compatibilidade N/N-1 e observabilidade da matriz inicial verificadas;
- documentação, licença, avisos de terceiros, versão, digests e matriz de suporte
  alinhados ao artefato candidato.

Validação:

- nenhum gate obrigatório `NOT RUN` para a matriz declarada;
- nenhum achado HIGH/CRITICAL sem resolução ou aceite formal;
- backup/restore e rollback aprovados;
- decisão humana GO/NO-GO registrada.

Evidências:

- pendente: checklist assinado e referência à decisão humana.

## Critérios de aceite finais

- CI remoto verde, incluindo integrações PostgreSQL sem skips;
- vulnerabilidades triadas, SBOMs e provenance verificáveis;
- carga nominal aprovada em ambiente isolado com thresholds prévios;
- cada plataforma anunciada passou suas provas; as demais estão explicitamente
  adiadas/experimentais;
- operação, recuperação, observabilidade e rollback documentados;
- decisão humana GO/NO-GO referenciada.

## Riscos e mitigação

| Risco                                             | Impacto                              | Mitigação                                            |
| ------------------------------------------------- | ------------------------------------ | ---------------------------------------------------- |
| carga executada sem baseline                      | resultado inconclusivo               | aprovar thresholds antes da execução                 |
| capability não qualificada anunciada como estável | falha operacional                    | restringir a matriz ao Docker Linux comprovado       |
| artefato sem provenance verificável               | cadeia de fornecimento não confiável | bloquear promoção até validar assinatura e emissor   |
| achados HIGH sem triagem                          | risco residual desconhecido          | triagem por pacote/CVE e aceite humano com expiração |

## Rollout e rollback

Este plano não executa publicação ou deploy. A promoção só poderá ocorrer após
decisão humana GO, usando digests imutáveis e runbook de rollback aprovado.

## Histórico de status

| Data       | Escopo | De  | Para     | Evidência ou motivo                                                            | Autor        |
| ---------- | ------ | --- | -------- | ------------------------------------------------------------------------------ | ------------ |
| 2026-09-25 | plano  | -   | proposed | plano físico criado a partir dos gates NO-GO restantes; aguarda revisão humana | IA assistida |

Este plano aguarda revisão e aprovação humana antes de sua execução formal.
