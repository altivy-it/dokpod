# Pesquisa: Migração de Prompts Auxiliares para Skills

**Data**: 2026-10-27 | **Feature**: [spec.md](spec.md)
**Origem**: IA assistida, pesquisa documental e inspeção somente leitura delegada durante `/speckit-plan`.
**Estado**: decisões propostas para revisão; não constituem validação de runtime ou autorização de implementação.
**Revisor humano**: Solicitante; identificação nominal e revisão pendentes.

## D1: localização e formato nativos

**Decisão**: usar três diretórios em `.github/skills`, com `SKILL.md` e nomes `dokpod-create-adr`, `dokpod-generate-tests` e `dokpod-localize-speckit`.

**Justificativa**: o harness Copilot não carrega `*.prompt.md`; skills de projeto nessa localização são descobertas sem configuração de busca do editor. O prefixo identifica o contexto Dokpod e reduz colisões. O padrão já é usado pelas skills oficiais presentes no repositório.

**Alternativas consideradas**: apenas renomear os arquivos sem converter metadados não resolve descoberta; usar diretório pessoal não compartilha o fluxo com a equipe; plugin/MCP acrescenta distribuição e infraestrutura desnecessárias. A migração automática do VS Code pode auxiliar, mas não cobre a consolidação da revisão nem as restrições de agente.

## D2: acionamento explícito e entradas

**Decisão**: nas três skills novas, usar `user-invocable: true` e `disable-model-invocation: true`, preservando orientações de argumentos como linguagem natural. Manter a configuração atual de ativação de `pull-request-review`.

**Justificativa**: a origem exige invocação manual; a conversão não deve permitir que a mera relevância da descrição inicie ADR, teste ou tradução. Nenhum `$ARGUMENTS` será inventado como mecanismo de interpolação; as instruções consideram o pedido recebido no chat. Sem autorização/mode/alvo, aplicam-se os bloqueios originais.

**Alternativas consideradas**: carregamento automático das três novas skills altera a semântica; ocultá-las do menu dificulta o uso; adicionar `context: fork` experimental não preserva por si só ferramentas ou seleção de agente.

## D3: agentes e ferramentas são contratos distintos

**Decisão**: orientar seleção explícita dos três especialistas já existentes e verificar suas capacidades no harness de destino, sem copiar `agent`, `model` ou `tools` para o frontmatter da skill. A localização permanece um procedimento do agente padrão, não um novo agente especializado.

**Justificativa**: a conversão oficial não preserva seleção de agente, modelo ou ferramentas. As listas atuais são: Architect `[read, search, edit, web]`, Quality `[read, search, edit, execute, todo]` e Reviewer `[read, search]`. A documentação exige verificar disponibilidade por harness; não há evidência nesta fase de que um alias específico esteja quebrado na sessão do usuário. `agents: []` expressa ausência de delegação no formato Local, mas seu efeito no destino também precisa ser observado, não presumido.

**Alternativas consideradas**: remover `tools` ou usar `*` amplia permissões; substituir aliases por nomes adivinhados mascara incompatibilidades; criar três novos agentes duplica papéis; delegar automaticamente exige ferramentas adicionais e pode perder restrições. Por isso, não faz parte deste desenho.

**Limite de segurança**: uma lista de ferramentas não restringe sozinha o caminho de toda edição. A limitação do Architect a ADRs propostos continua normativa e é conferida por diff/hashes; não será descrita como sandbox por diretório. No Reviewer, ausência efetiva de ferramentas de escrita/execução deve ser demonstrada separadamente de uma recusa textual.

## D4: consolidação sem duplicar revisão

**Decisão**: manter [pull-request-review](../../.github/skills/pull-request-review/SKILL.md) como única fonte da revisão e transferir somente diferenças necessárias de `review-change`.

**Justificativa**: a skill existente já cobre rastreabilidade, base/head, comportamento anterior/proposto, dimensões de segurança, contratos, engines, testes e saída por severidade. A implementação deve conferir a matriz de regras antes de mudar conteúdo; não se presume que todo trecho do prompt exija nova redação.

**Alternativas consideradas**: criar `dokpod-review-change` mantém procedimentos concorrentes; substituir a skill pelo prompt mais curto pode perder cobertura. A consolidação conserva a restrição de leitura/busca do Code Reviewer, sem executar comandos para obter um diff.

## D5: preservar componentes gerenciados e localizar sem instalar

**Decisão**: converter apenas o procedimento auxiliar de localização, mantendo suas condições e exclusões. Não regenerar skills oficiais, atualizar extensões, alterar manifests ou migrar hooks como efeito colateral.

**Justificativa**: o procedimento de localização documentado na [análise da origem](../../.github/plano-migracao-prompts-skills.md) distingue fontes, materializações, templates e configurações. Sua migração para skill não autoriza executar traduções nos componentes reais; casos com escrita usam cópia descartável. Inventário sem alvo é um resultado de auditoria, não um novo valor de CLI `modo=inventariar`.

**Alternativas consideradas**: atualizar/reinstalar Spec Kit para materializar a skill altera outro escopo; copiar fonte sobre skill ignora aliases/proveniência; criar configuração fictícia de idioma ou hook de instalação viola o contrato original.

## D6: referências canônicas e recuperação

**Decisão**: recalcular links da nova profundidade, atualizar guias ativos após criar destinos e excluir os quatro prompts somente após os gates. Preservar menções históricas em artefatos de origem, sem transformá-las em orientação atual.

**Justificativa**: a partir de `.github/skills/<nome>/SKILL.md`, os alvos são `../../ADR_TEMPLATE.md`, `../../instructions/`, `../../agents/` e `../../../.specify/`, `../../../docs/` ou `../../../specs/`. Não se deve reutilizar `../` da pasta de prompts. Os documentos operacionais conhecidos são `.github/ENGENHARIA_ASSISTIDA.md` (antigo `.github/README.md`), `.github/COPILOT_GUIDE.md`, `.github/copilot-instructions.md` e `.github/GOVERNANCE.md`; as referências dos agentes também serão verificadas.

**Alternativas consideradas**: manter prompts como pontes contraria FR-013 e não ajuda o Copilot; apagar fontes antes de testar rompe a recuperação; atualizar retrospectivamente todos os documentos históricos destrói a rastreabilidade. Recuperar somente o diff desta migração se necessário, sem reset, recheckout indiscriminado ou regeneração.

## D7: evidência estática e funcional complementares

**Decisão**: usar checagens em containers existentes para estrutura, paridade e integridade; qualificar descoberta, seleção de agentes e efeitos em sessão nova do Copilot com cenários descartáveis.

**Justificativa**: checar `name`, links e hashes não comprova carregamento pelo harness; uma única resposta não comprova paridade de todas as regras. O teste negativo de revisão não deve executar comando destrutivo para provar sua ausência: inspecionar ferramentas disponíveis e solicitar operação inofensiva em fixture para observar a recusa. Nenhuma aplicação HTTP é necessária; eventual fixture HTTP futura permanece condicionada a nginx em container.

**Alternativas consideradas**: usar a sessão Local como prova não atende ao alvo; executar `/speckit-implement` real para testar a nova skill amplia indevidamente o escopo; um validador novo com parser instalado aumenta dependências. Sem parser disponível, declarar a limitação e manter o gate estrutural pendente, em vez de tratar regex como parser YAML.

## Fontes e limites

Consultadas em 2026-10-27:

- [Migração de customizações no VS Code](https://code.visualstudio.com/docs/agent-customization/migrate-customizations): ausência de prompts no harness Copilot, conversão manual e metadados não herdados.
- [Agent Skills no VS Code](https://code.visualstudio.com/docs/agent-customization/agent-skills): diretórios nativos, formato, invocação explícita e descoberta progressiva.
- [Referência de agentes](https://code.visualstudio.com/docs/agents/reference/custom-agent-file-reference): dependência do harness, listas de ferramentas e distinção do alvo cloud.
- [Constituição](../../.specify/memory/constitution.md), [instruções globais](../../.github/copilot-instructions.md) e [rascunho anterior](../../.github/plano-migracao-prompts-skills.md): limites locais, gates, componentes protegidos e mapeamento inicial.

Não há dúvida de desenho bloqueante restante. Compatibilidade efetiva dos aliases, descoberta na versão instalada e execução dos cenários permanecem validações futuras, registradas como `NOT RUN`, sem alegar que a pesquisa documental as resolveu.