# Plano de Implementação: Migração de Prompts Auxiliares para Skills

**Branch**: `030-migrar-prompts-skills` | **Data**: 2026-10-27 | **Spec**: [spec.md](spec.md)

**Entrada**: Especificação em `specs/030-migrar-prompts-skills/spec.md`; diretório selecionado explicitamente por `SPECIFY_FEATURE_DIRECTORY`.

**Estado**: desenho proposto; implementação não iniciada nem aprovada por este documento.

**Origem**: IA assistida, execução de `/speckit-plan 025` solicitada pelo humano.

**Revisor humano**: Solicitante; identificação nominal e revisão dos artefatos pendentes.

## Resumo

Disponibilizar os quatro fluxos auxiliares no Session Target Copilot do VS Code, mantendo Spec Kit como único processo de manutenção. Criar três skills de projeto com acionamento manual (`dokpod-create-adr`, `dokpod-generate-tests`, `dokpod-localize-speckit`) e consolidar `review-change` na skill existente `pull-request-review`.

A conversão não transfere o campo `agent` do prompt. O guia orientará a seleção explícita dos especialistas existentes; as listas de ferramentas serão verificadas no harness, sem remover restrições para obter descoberta. Os quatro prompts só serão excluídos após validação estática e funcional, com evidência por fluxo. Não haverá pontes Local, atualização do Spec Kit, novo agente genérico ou preparação do cloud agent.

## Contexto Tecnico

**Linguagem/Versão**: Markdown UTF-8 em pt-BR com frontmatter YAML nas futuras skills; Spec Kit e integração Copilot 1.0.13, extensões assess/bug/git 1.0.1. Nenhuma mudança em C#, Angular ou versões de ferramentas.

**Dependências Principais**: suporte a skills do harness Copilot, agentes de projeto, instruções e modelos canônicos existentes. PowerShell 7 orquestra scripts oficiais e containers; validações usam ferramentas existentes, sem instalar parser ou biblioteca.

**Persistência**: arquivos versionados de customização e evidências desta feature; nenhum schema ou datastore. O seletor `.specify/feature.json` é gerenciado pelo fluxo oficial e não constitui estado de aprovação.

**Testes**: comparação de regras e contratos técnicos, resolução de links, frontmatter por parser existente quando disponível, hashes de arquivos protegidos e cenários em sessão nova do Copilot sobre cópia descartável. Testes da aplicação não são necessários para validar o texto das customizações; um cenário positivo de testes usa somente fixture autorizada e runner em container.

**Plataforma Alvo**: VS Code com Session Target Copilot. Local é apenas a origem; GitHub cloud agent, CLI e outros harnesses não são qualificados por esta entrega. Linux é o ambiente de preparação atual; a migração não certifica engines ou Windows.

**Tipo de Projeto**: manutenção de customizações de engenharia de um monorepo, sem alteração do produto.

**Metas de Desempenho**: nenhuma meta de latência nova; carregamento sob demanda, sem copiar procedimentos para instruções globais. Aceite por cobertura dos quatro fluxos e ausência de efeitos proibidos, conforme SC-001 a SC-006.

**Restrições**: nenhuma alteração em skills `speckit-*`, fontes/manifests de extensões, hooks, constituição, produto, secrets ou política de aprovação. ADRs continuam `proposed`. Ausência de ferramenta ou sessão de destino bloqueia a retirada dos prompts. Não adicionar `allowed-tools` com pré-aprovação de shell, `context: fork`, MCP, plugin ou novo script global nesta migração.

**Escala/Escopo**: três skills novas, uma skill canônica de revisão, quatro prompts a remover depois dos gates, quatro documentos operacionais conhecidos e referências aos três agentes associados. Ajustes de agentes limitam-se a referências e compatibilidade comprovadamente necessária, sem ampliar capacidades.

## Verificação da Constituição

Avaliação técnica inicial e reavaliação do desenho, sem equivaler a aprovação humana da implementação:

| Princípio ou restrição | Antes da pesquisa | Após o desenho |
| --- | --- | --- |
| I: documentação pt-BR | Aplicável; textos humanos em pt-BR | Atendido no desenho; identificadores técnicos preservados |
| II: engine e workloads | Nenhuma mudança de engine | Preservado; nenhum socket ou operação de produto requerido |
| III: menor privilégio e secrets | Não ampliar permissões nem acessar secrets | Seleção explícita de agente e gate de ferramentas; evidências sanitizadas |
| IV: operações recuperáveis | Sem mudança de journal, fencing ou reconciliação | Regras de testes/revisão preservadas; recuperação por diff da migração |
| V: modularidade e compatibilidade | Reutilizar donos existentes | Procedimento em skills, ferramentas em agentes, regras em instruções; nenhum datastore ou contrato público alterado |
| VI: qualidade por plataforma | Não confundir análise estática com execução | Casos positivos/negativos e matriz de evidência; `NOT RUN` bloqueia aceite do fluxo afetado |
| Containers e nginx | Aplicável às ferramentas e eventual fixture HTTP | Validadores em containers; nenhum serviço HTTP necessário para esta manutenção; futura fixture HTTP exige nginx |
| Processo Spec Kit | Esta etapa apenas planeja | Tarefas, análise, revisão e autorização precedem implementação; nenhuma aprovação inferida |

**Resultado**: nenhuma violação constitucional identificada no desenho. Identificação do revisor e autorização de tarefas continuam pendentes; não há dispensa de gate ou necessidade de novo ADR para esta conversão de formato.

## Estrutura do Projeto

### Documentação desta Funcionalidade

```text
specs/030-migrar-prompts-skills/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── checklists/requirements.md
├── contracts/workflow-contract.md
└── tasks.md                         # Gerado posteriormente por /speckit-tasks
```

### Código-Fonte (raiz do repositório)

```text
.github/
├── skills/
│   ├── dokpod-create-adr/SKILL.md        # Criar após autorização
│   ├── dokpod-generate-tests/SKILL.md    # Criar após autorização
│   ├── dokpod-localize-speckit/SKILL.md  # Criar após autorização
│   └── pull-request-review/SKILL.md     # Consolidar apenas diferenças
├── agents/
│   ├── solution-architect.agent.md
│   ├── quality-engineer.agent.md
│   └── code-reviewer.agent.md
├── prompts/                            # Quatro fontes preservadas até aceite
├── copilot-instructions.md
├── README.md
├── COPILOT_GUIDE.md
└── GOVERNANCE.md
```

**Decisão de Estrutura**: manter `.github/skills` como localização nativa compartilhada pelo repositório. Modelos e instruções permanecem nos donos atuais. O contrato nesta spec descreve a interface de uso pelo mantenedor, não uma API de produto. O modelo descreve artefatos, regras e estados, sem tabelas ou migrations.

### Fase 0: decisões de pesquisa

As decisões e alternativas estão em [research.md](research.md): formato nativo, três skills manuais, consolidação da revisão, seleção explícita dos especialistas, integridade de componentes gerenciados e validação em dois níveis. A pesquisa resolve as decisões de desenho; a comprovação de compatibilidade em uma sessão de destino é gate operacional futuro, não resultado presumido.

### Fase 1: desenho dos fluxos

1. Criar os três destinos com `name` igual ao diretório, `description` específica em pt-BR, `argument-hint`, `user-invocable: true` e `disable-model-invocation: true`. Não copiar `agent`, `model` ou `tools` do prompt para a skill.
2. Preservar cada procedimento original, recalculando links a partir de `SKILL.md`. A localização segura deve manter todas as listas, exclusões, limites e verificações; seu tamanho atual não justifica fragmentação ou automação nova.
3. Selecionar Solution Architect para ADR e Quality Engineer para testes. Para revisão, selecionar Code Reviewer com ferramentas efetivamente restritas a leitura/busca. Localização usa o agente padrão com os gates de modo/alvo originais; `agent: agent` da origem não cria dependência de um agente especializado.
4. Verificar no harness cada alias atual (`read`, `search`, `edit`, `web`, `execute`, `todo`) e ausência de delegação não autorizada. Alterar apenas nomes comprovadamente incompatíveis para capacidades equivalentes. Nunca omitir `tools`, usar curinga ou adicionar execução ao revisor como correção de descoberta.
5. Em `pull-request-review`, manter o nome, a política de ativação existente, a revisão somente leitura e o escopo multidimensional. Incorporar somente lacunas de entrada ou formato comprovadas pela matriz de paridade.
6. Atualizar os quatro guias/documentos operacionais e as referências dos agentes necessárias ao uso correto, depois de existirem os destinos. Fazer busca dirigida adicional antes da exclusão, distinguindo instrução ativa de evidência histórica.
7. Validar em cenário descartável: descoberta não prova execução, recusa textual não prova restrição de ferramenta e um teste de amostra não prova toda a paridade. Reunir as três evidências separadamente.
8. Após todos os gates, excluir os quatro prompts, conferir ausência de links operacionais quebrados e relatar os resultados. Se falhar um fluxo, não concluir a remoção; preservar a fonte correspondente e registrar o bloqueio, sem ponte permanente.

### Matriz de rastreabilidade para a geração de tarefas

| Recorte | Requisitos | Evidência futura |
| --- | --- | --- |
| Descoberta e metadados | FR-001, FR-003, FR-012 | Frontmatter, nomes, seleção e invocação na sessão de destino |
| ADR | FR-002, FR-004, FR-011 | Paridade, proposta autorizada, recusa sem justificativa/autorização |
| Testes | FR-002, FR-005, FR-006, FR-011 | Matriz risco-teste, fixture autorizada, infraestrutura e lacunas |
| Localização | FR-002, FR-007, FR-008 | Auditoria sem escrita, inventário, tradução isolada e integridade |
| Revisão | FR-002, FR-009, FR-010, FR-011 | Fonte única, achados completos e ferramentas restritas |
| Retirada e preservação | FR-013, FR-014, FR-015, FR-016 | Gates por fluxo, referências atualizadas, hashes e diff de escopo |

### Validação documental desta etapa

Preparação por `setup-plan.ps1 -Json` com diretório explícito, comparação ao template ativo e checagens em container somente leitura: estrutura, links locais, ausência de placeholders, cobertura dos 16 requisitos, quatro interfaces e dez cenários conferidos. O check oficial `check-prerequisites.ps1 -Json`, executado em processo próprio com `SPECIFY_FEATURE_NO_PERSIST=1`, identificou a feature 025 e os quatro conjuntos de documentos de desenho esperados. As skills futuras não foram criadas nem executadas; validação funcional no harness: `NOT RUN`.

### Limites da entrega de planejamento

Esta etapa gera somente plano, pesquisa, modelo de artefatos, contrato de uso e guia de validação. Não gera `tasks.md`, não implementa fixtures ou skills, não exclui prompts e não modifica a spec para acomodar limitações de ferramenta. A próxima etapa é `/speckit-tasks` para este diretório, seguida de `/speckit-analyze`, revisão e autorização humana.

## Registro de Complexidade

Nenhuma violação constitucional a justificar. Não serão introduzidos scripts persistentes, novos agentes, dependências, plugins ou serviços. Se a compatibilidade exigir expansão além dos três agentes associados, interromper e encaminhar a decisão ao responsável antes de alterar o escopo.
