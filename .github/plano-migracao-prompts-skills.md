# Dokpod — plano de migração de prompts para skills

| Campo | Valor |
| --- | --- |
| Estado | rascunho para revisão; execução não autorizada |
| Origem | IA assistida |
| Revisor humano | a designar pelo solicitante |
| Criado em / atualizado em | 2026-10-22 |
| Projeto | `/home/u_zocatel/projetos/dokpod` |
| Escopo | quatro prompts locais; nenhuma alteração no produto |

Este é um subsídio à revisão, não um plano executável concorrente ao Spec Kit.
Não criar ou atualizar planos em `docs/plan`, que está congelado.

## Compatibilidade e fontes

O [guia oficial de migração](https://code.visualstudio.com/docs/agent-customization/migrate-customizations#_convert-prompt-files-to-skills),
consultado em 2026-10-22, informa que o harness Copilot não carrega
`*.prompt.md`. No Local, um prompt mínimo pode referenciar a skill, sem copiar
seu procedimento, mas isso exige teste de encaminhamento e não resolve a
migração para Copilot. A recomendação é migração completa, com pontes Local
apenas se aprovadas para uma necessidade concreta.

A conversão não preserva seleção de `agent`, `model` ou `tools`; revisar
esses contratos sem copiá-los para frontmatter de skill como se fossem
equivalentes. As novas skills devem usar o
[formato suportado](https://code.visualstudio.com/docs/agent-customization/agent-skills).

## Inventário e destino proposto

Todos os caminhos abaixo são relativos à raiz do projeto. Destinos novos
ainda não existem e dependem de aprovação.

| Prompt atual | Destino canônico | Ação proposta |
| --- | --- | --- |
| `.github/prompts/create-adr.prompt.md` | `.github/skills/dokpod-create-adr/SKILL.md` | criar skill auxiliar para ADR justificado pelo Spec Kit |
| `.github/prompts/generate-tests.prompt.md` | `.github/skills/dokpod-generate-tests/SKILL.md` | criar skill auxiliar para testes de tarefa/remediação autorizada |
| `.github/prompts/localize-speckit.prompt.md` | `.github/skills/dokpod-localize-speckit/SKILL.md` | migrar o procedimento local de localização segura |
| `.github/prompts/review-change.prompt.md` | `.github/skills/pull-request-review/SKILL.md` | consolidar diferenças úteis na skill existente; não criar outra revisão equivalente |

Resultado proposto: **três skills novas e uma existente revisada**, com os
quatro prompts retirados após validação. Pontes Local são exceção, não padrão.

O prompt `localize-speckit` é um auxiliar local: seu caminho não aparece no
manifesto Copilot consultado. Ele opera sobre componentes gerenciados, mas isso
não o torna um comando gerado do Spec Kit. Sua migração não autoriza editar
skills `speckit-*`, fontes de extensões ou hashes.

## Trabalho por fluxo

### D1 — ADR

- Preservar entrada: referência a `/speckit-plan`, decisão, autorização e
  revisor humano.
- Preservar o agente `Dokpod Solution Architect` como papel de execução;
  avaliar sua seleção explícita no Copilot, sem presumir herança de `agent`.
- Referenciar `.github/ADR_TEMPLATE.md`, instruções arquiteturais e artefato
  Spec Kit; não copiar o template para dentro da skill.
- Manter caminho `docs/adr/AAAA-NNNN-titulo.md`, sequência anual, origem e
  estado `proposed`. Não aceitar ADR nem inventar aprovação.
- Falhar de modo explícito quando faltar justificativa ou autorização.

### D2 — Testes

- Preservar entrada: artefato Spec Kit, ID de tarefa/remediação e autorização.
- Preservar papel `Dokpod Quality Engineer`, critérios e escopo do artefato.
- Referenciar instruções de testes e scripts existentes; não inventar runner.
- Não transformar invocação da skill em autorização de trabalho não revisado.
- Preservar execução em containers. Fluxos HTTP usam borda nginx em container;
  unitários sem aplicação HTTP não exigem nginx ocioso.

### D3 — Localização do Spec Kit

- Preservar entradas `extensão`, `arquivos`, `modo` e exclusões como linguagem
  natural, não parâmetros de CLI.
- Sem modo: somente auditar. Sem alvo: inventariar e apresentar candidatos.
- Preservar distinção entre fonte de extensão e skill materializada, avisos
  de alterações locais, contratos, placeholders, IDs, aliases e hashes.
- Manter exclusões de `.specify/extensions/bug/README.md`, overrides já
  localizados e arquivos fora do alvo.
- Referenciar documentação/constituição sem copiá-las. Não instalar ou
  reconciliar extensões para testar a nova skill.
- Não alterar manifests para disfarçar mudanças locais.

### D4 — Revisão

- Comparar o prompt com `pull-request-review` e transferir apenas diferenças
  de cobertura, entradas ou formato de achados que ainda não estejam nela.
- Preservar diff/branch/PR, base de comparação e tarefa/remediação Spec Kit.
- Preservar `Dokpod Code Reviewer` e sua restrição real de ferramentas;
  não substituir seleção de agente por uma promessa textual de read-only.
- Não executar implementação ou comandos mutantes ao testar descoberta.
- Revisar se a configuração de invocação da skill existente deve permanecer
  igual; não mudar ativação automática silenciosamente.

## Arquivos afetados e referências a revisar

| Arquivo ou conjunto | Alteração na implementação futura |
| --- | --- |
| quatro prompts da tabela | preservar durante a transição; remover após aceite, ou reduzir a pontes Local explicitamente aprovadas |
| três novos destinos da tabela | `name` igual ao diretório, descrição pt-BR, entradas e procedimento sem duplicação |
| `.github/skills/pull-request-review/SKILL.md` | receber diferenças necessárias e referências do fluxo de revisão |
| `.github/ENGENHARIA_ASSISTIDA.md` (antigo `.github/README.md`) | atualizar inventário e links para prompts |
| `.github/COPILOT_GUIDE.md` | atualizar seleção de fluxos, exemplos e nomes de comandos |
| `.github/copilot-instructions.md` | substituir referência ao prompt de localização pela skill local equivalente |
| `.github/GOVERNANCE.md` | revisar descrição de `prompts/` e `skills/`, sem alterar autoridade ou aprovação |
| `.github/agents/solution-architect.agent.md`, `.github/agents/quality-engineer.agent.md`, `.github/agents/code-reviewer.agent.md` | confirmar compatibilidade dos agentes e seus nomes de exibição; editar somente se o contrato do harness exigir |

Não confundir o nome de exibição do agente com o nome físico do arquivo.
Revisar também referências externas à pasta `.github` antes de excluir fontes.
A busca inicial nos READMEs raiz não encontrou referências diretas aos prompts.

**Protegidos:** `.specify/integrations/copilot.manifest.json`,
`.specify/init-options.json`, extensões, constituição, skills `speckit-*`,
planos históricos, código, contratos, deployment e secrets.

## Sequência, dependências e aceite

1. Revisão humana do mapeamento, nomes com prefixo e política de pontes.
2. Formalização pelo fluxo Spec Kit de manutenção; selecionar explicitamente
   o diretório da feature e obter autorização das tarefas.
3. Registrar referência anterior dos quatro prompts e arquivos protegidos.
4. Implementar D1, D2 e D3 independentemente; implementar D4 como consolidação.
5. Atualizar os consumidores somente após existirem os destinos.
6. Validar os quatro fluxos e retirar os prompts apenas após aceite.

Critérios:

- quatro prompts cobertos, nenhuma regra exclusiva perdida;
- três novas skills manuais com `user-invocable: true` e
  `disable-model-invocation: true`;
- nenhum nome novo colide com AltivyNotes na workspace multirraiz;
- links recalculados a partir de `SKILL.md`, não reutilizados cegamente;
- testes negativos: falta de autorização, ADR sem justificativa, revisão
  solicitando escrita e localização sem modo não produzem alteração indevida;
- invocação em Copilot carrega o fluxo da raiz Dokpod; qualquer ponte Local
  carrega a mesma fonte sem procedimento duplicado;
- hashes dos componentes gerenciados e arquivos protegidos permanecem iguais.

Testes com escrita devem usar cópia descartável ou cenário autorizado, nunca
aprovar ou executar uma tarefa real apenas para testar a customização.

## Pontos para decisão humana

- Aprovar os três nomes prefixados e a consolidação em `pull-request-review`.
- Confirmar se o Local precisa continuar suportado; caso contrário, remover
  todos os prompts ao final.
- Confirmar como o Copilot selecionará os agentes especializados sem perder
  restrições de ferramentas.
- Designar revisor e autorizar posteriormente o recorte Spec Kit.

## Recuperação e limites

Se descoberta, argumentos ou restrições falharem, não retirar o prompt de
origem. Reverter apenas a fatia da migração por diff revisado; não usar reset
nem regenerar Spec Kit. Nesta entrega documental, nenhuma customização foi alterada e
nenhum fluxo foi executado. Validação de runtime: **NOT RUN**.
