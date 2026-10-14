# Especificação da Feature: Qualificação de Release Docker Linux

**Branch da migração**: `chore/adotar-spec-kit`; branch de execução a definir após autorização.

**Criada em**: 2026-10-06

**Estado**: `draft` - rascunho de migração, aguardando revisão humana.

**Entrada**: [Desbloqueio de release](../../docs/plan/desbloqueio-release.md), MVP P-06 e gates da [avaliação de release](../../docs/release-readiness.md), somente consultada.

## Cenários do Usuário e Testes (obrigatório)

### Jornada do Usuário 1 - Verificar candidata e cadeia de fornecimento (Prioridade: P1)

Como revisor de segurança, quero CI, SBOMs, triagem e integridade verificáveis por artefato.

**Valor desta prioridade**: Build local e ausência de CRITICAL não encerram achados HIGH.

**Teste independente**: Conferir execução CI identificada e scans das quatro imagens, verificando assinatura/provenance em ambiente limpo.

**Cenários de aceite**:

1. **Dado** CI candidato, **Quando** revisado, **Então** inclui backend, migrations/PostgreSQL sem skips, frontend, imagens, scans e SBOMs com URL/ID/commit/artifacts.
2. **Dado** 34 HIGH históricos da web, **Quando** triados, **Então** cada achado tem correção/re-scan ou classificação/aceite humano formal com responsável, impacto e expiração; ausência de CRITICAL não libera o gate HIGH.
3. **Dado** digest alterado, assinatura inválida, emissor inesperado ou revogação, **Quando** verificados, **Então** o artefato é rejeitado.

### Jornada do Usuário 2 - Medir capacidade e recuperação (Prioridade: P1)

Como operador, quero evidência de carga nominal e margem em ambiente isolado antes de assumir capacidade de produção.

**Valor desta prioridade**: Laboratório de 16 containers não prova escala ou recuperação.

**Teste independente**: Medir dois perfis com build e VM identificados, baseline/thresholds humanos prévios e resultados brutos sanitizados.

**Cenários de aceite**:

1. **Dado** perfil nominal, **Quando** executado por pelo menos 30 minutos com 56 agentes, 1.120 containers e 30 usuários simultâneos, **Então** atende limites previamente aprovados de p95/p99, erro, convergência e recursos.
2. **Dado** perfil de margem, **Quando** executado com pelo menos 100 agentes, 2.000 containers e 50 usuários, **Então** atende thresholds aprovados, incluindo reconexão simultânea e inventário degradado.
3. **Dado** resposta perdida/restart/comandos concorrentes em ambientes distintos, **Quando** recupera, **Então** não há operação não autorizada, perda ou efeito duplicado, e o backlog retorna ao baseline.

### Jornada do Usuário 3 - Decidir suporte e GO/NO-GO (Prioridade: P1)

Como responsável humano pela release, quero distinguir evidência, limitações e decisão de suporte sem presumir aprovação.

**Valor desta prioridade**: Nenhum rascunho ou checkbox deve declarar release pronta.

**Teste independente**: Revisar matriz de gates e demonstrar que um obrigatório `NOT RUN` ou HIGH sem tratamento mantém NO-GO.

**Cenários de aceite**:

1. **Dado** gate obrigatório ausente para a matriz declarada, **Quando** revisada, **Então** a candidata permanece NO-GO.
2. **Dado** Windows/Podman não qualificados, **Quando** a matriz inicial é decidida, **Então** ficam experimentais/adiados por decisão humana explícita e não como suporte estável implícito.
3. **Dado** todos os gates comprovados, **Quando** o humano decide GO, **Então** a decisão é referenciada; publicação/deploy ainda exigem autorização separada.

## Casos de Borda

- CI verde com skips, artifacts ausentes, scan antigo, digest divergente ou exceção HIGH expirada.
- Carga exploratória sem baseline, ambiente compartilhado, threshold definido depois da execução e relatório parcial.
- N/N-1 não exercitado, rollback incompatível, restore sem auditoria, retenção/replay indefinidos e alertas sem evidência.
- Plataforma adiada confundida com stable, assinatura local sem origem verificável e revisão de threat model ausente.

## Requisitos (obrigatório)

### Requisitos Funcionais

- **FR-001**: Veredito atual **NO-GO** DEVE permanecer até decisão humana apoiada em gates; esta migração não altera [release-readiness](../../docs/release-readiness.md).
- **FR-002**: CI remoto DEVE comprovar gates já configurados, incluindo PostgreSQL real sem skips e inspeções de usuário/portas/mounts/redes; configuração local não equivale à execução remota.
- **FR-003**: Quatro imagens DEVEM ter SBOM/scan por build/digest; nenhum HIGH/CRITICAL sem resolução ou aceite formal aplicável pode ser ignorado. Nenhum CRITICAL corrigível pode contornar o gate; 34 HIGH da web são histórico a atualizar por evidência, não contagem atual presumida.
- **FR-004**: Carga nominal DEVE usar 56/1.120/30 por >=30 minutos em VM isolada; margem DEVE usar >=100/2.000/50. Baseline e thresholds p95/p99/erro/convergência/recursos são aprovados antes dos testes.
- **FR-005**: Carga/recuperação DEVEM incluir snapshots/deltas/leituras, replay, resposta perdida, reconnect storm, comandos concorrentes, restart e recuperação de backlog; preservar journal-before-ack, dedup e fencing de 003.
- **FR-006**: Threat model DEVE ser revisado; assinatura por digest/provenance verificável, identidade do workflow, emissor e revogação DEVEM ter testes positivos/negativos em ambiente limpo. Assinar/publicar requer autorização própria, não este rascunho.
- **FR-007**: Compatibilidade N/N-1, observabilidade, backup/restore, integridade de auditoria, atualização e rollback DEVEM possuir evidência da matriz candidata; reaproveitar provas de 001-004 sem replicar sua implementação.
- **FR-008**: Licença AGPL-3.0-only integral, avisos de terceiros, código-fonte correspondente, digests/versão/matriz e CE sem chave, telemetria comercial obrigatória ou serviço de licenciamento DEVEM ser verificados.
- **FR-009**: Docker Linux só recebe suporte publicado após seus gates; Windows/Podman seguem 006 ou adiamento humano formal. GO não é autorização de commit/push/publicação/deploy.

### Entidades Principais

- **Gate/evidência**: cenário, estado, commit/digest, data, ambiente, resultado e revisor, sem secrets.
- **Matriz/decisão**: capability e condição de suporte com referência humana, sem promoção automática.

## Critérios de Sucesso (obrigatório)

### Resultados Mensuráveis

- **SC-001**: Todos os gates obrigatórios da matriz têm evidências referenciadas e nenhum `NOT RUN` é tratado como PASS.
- **SC-002**: Ambos os perfis de carga atendem thresholds prévios e recuperam backlog sem perda/efeito duplicado ou acesso indevido.
- **SC-003**: Todos os HIGH/CRITICAL têm tratamento formal verificável; cadeia de fornecimento rejeita os cenários negativos.
- **SC-004**: Uma decisão humana GO/NO-GO identifica a matriz e seus adiamentos, sem mudar status por IA.

## Premissas e Proveniência

MVP P-06 e desbloqueio P-01 a P-06 migram para qualificação residual, com P-05 dividido entre Docker Linux aqui e Windows/Podman em 006. Build/testes/E2E/scans/restore locais já são evidências históricas, não tarefas de recriação. Carga nominal/margem, CI remoto, observabilidade, N/N-1 e assinatura permanecem gates a comprovar. Não executar nada externo nesta migração.
