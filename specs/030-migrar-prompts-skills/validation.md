# Evidências de Implementação da Feature 030

**Data**: 2026-10-27
**Origem**: IA assistida.
**Identificador ativo**: `specs/030-migrar-prompts-skills`, branch `030-migrar-prompts-skills`.
**Referência anterior**: branch `025-migrar-prompts-skills`, commit `7ef07c5445cae97133c00fd1ca37ca32278e843c`.
**Estado**: execução iniciada; nenhuma qualificação funcional ou retirada de fonte aprovada por este relatório.
**Autorização humana**: invocação explícita de `/speckit-implement` com argumento `025` nesta conversa, após a segunda análise que não encontrou achados pendentes. Escopo: tarefas T001 a T033, observando suas dependências e condições de parada; não inclui aceite de ADR, commit, push, publicação ou deploy.
**Revisor humano**: solicitante desta invocação; identificação nominal não informada, sem atribuição automática de identidade ou aprovação histórica.

## Renumeração e Publicação Solicitadas

Em 2026-10-27, após a execução parcial, o solicitante determinou renumerar esta spec de 025 para 030 e fazer commit e push para permitir merge posterior com outra branch. Essa autorização específica permite versionar e enviar os artefatos desta feature e a implementação parcial já registrada, sem realizar merge, dispensar a qualificação funcional ou excluir prompts.

Diretório e branch ativos foram alinhados a `030-migrar-prompts-skills`; caminhos operacionais e seletor local foram atualizados. As invocações históricas com argumento `025`, a referência Git anterior, os digests e os caminhos do baseline permanecem evidências da execução original, não instruções para selecionar o diretório antigo. A checklist foi movida e seu caminho operacional atualizado por esta autorização; o digest histórico não foi reescrito para ocultar a mudança.

A cópia já existente em `/tmp/dokpod-025-validation/` mantém seu caminho real por continuidade das evidências; não foi renomeada nem regenerada. Os 33 IDs, as dez conclusões e o bloqueio de T011 foram preservados. Antes de retomar, usar explicitamente `SPECIFY_FEATURE_DIRECTORY=specs/030-migrar-prompts-skills`. O check oficial em container já resolveu esse diretório com spec e tarefas exigidas. Esta renumeração não requalifica os cenários `NOT RUN`.

## Preparação e Checklists

- T001: referência da análise e autorização desta invocação conferidas; diretório selecionado explicitamente como `specs/025-migrar-prompts-skills`.
- Check oficial em container PowerShell, mount somente leitura, sem rede e com `SPECIFY_FEATURE_NO_PERSIST=1`: `PASS`; `research.md`, `data-model.md`, `contracts/`, `quickstart.md` e `tasks.md` disponíveis.
- Checklist `checklists/requirements.md`: 16 itens marcados, nenhum desmarcado. A checklist foi somente lida; não equivale a aprovação humana da implementação.
- Repositório Git identificado; arquivos de exclusão pertinentes já existentes e sem necessidade de alteração neste recorte documental.
- Hook opcional `before_implement`: não executado; apesar da configuração habilitada, o recorte FR-016 não permite commit como consequência da conversão. `after_implement` é opcional e possui commit automático desativado na configuração consultada.

## Baseline Individual das Fontes

SHA256 capturado antes de qualquer alteração de customização, em container sem rede e com mount somente leitura:

| Caminho | SHA256 anterior |
| --- | --- |
| `.github/prompts/create-adr.prompt.md` | `E7D99E84C427A23144EDC60828184D0E57A4DCEFBBC766574B7D064294B968F1` |
| `.github/prompts/generate-tests.prompt.md` | `0066B11D20D5A4DFF4B6FA1F622C9B97F72F070D53AD7FB6D511557122B94301` |
| `.github/prompts/localize-speckit.prompt.md` | `B53C42ECE7E484657D127565D04ABF9C908C5396FFA1F1E011A9B160BA411282` |
| `.github/prompts/review-change.prompt.md` | `EF1AC4A7F1E2D9C22EECC6BFFE4D5C147BEFE72A5168997C659B7CBD1CE01AF5` |
| `.github/skills/pull-request-review/SKILL.md` | `90F272CB2F8264391781AAE63A6ECAF4086152A37847BF2DD4423E762FB9B666` |
| `.github/agents/solution-architect.agent.md` | `91AD60CC7C0F791DFCF4AF9346AD238B8D6127311E4378C0F44244C580D8D69C` |

## Baseline dos Conjuntos Protegidos

Cada digest é SHA256 do texto UTF-8 sem BOM formado por linhas `caminho-relativo:HASH-SHA256`, ordenadas por caminho com `Sort-Object`, separadas por LF e sem LF final. Caminhos usam `/`, hashes hexadecimais maiúsculos e raiz lógica `/workspace`. A quantidade também precisa coincidir; o digest detecta alterações, inclusão ou exclusão sem publicar conteúdo de componentes.

| Conjunto | Arquivos | SHA256 agregado anterior |
| --- | ---: | --- |
| `.github/skills/speckit-*/SKILL.md` | 23 | `D7DBA7633381A53B5F881B63C3AA61DD98AFCFDBD0AE4172861A146938E88D7E` |
| `.specify/extensions/` e `.specify/extensions.yml` | 36 | `21DE96DDAB4E4044998D1EC60F7A6F68C8A968AD51BCDDF04D0F98C5CEB17C87` |
| `.specify/integrations/`, `.specify/integration.json`, `.specify/init-options.json` | 4 | `57BD514D04095B945AD6A6DA32E8EC5DADAFEC430002BC30E8C720F673A0C8B0` |
| `.specify/memory/constitution.md` | 1 | `C506237F6B521A26007E802E445ADD84A94884B8D194937FAAC1F28630510920` |
| `.specify/templates/` | 10 | `7962452BDC44CCA729F5ED14C92EA49CB0AB3C6865F8189EDF8F95478D0E9AAE` |
| `.github/instructions/` | 20 | `EC47860A0E1648A07927EFAB17BD52D448EC80FBC999A57F47476746F9DAE0A4` |
| `specs/025-migrar-prompts-skills/checklists/` | 1 | `E07CAD7E541C93402C0D6F9E2F2928F09120073BF278A922DECBB55044462E1F` |
| `.gitignore` e `.dockerignore` | 2 | `782B107B0C5832AAA3A0A55EBF815E9A7724FDF70836212805C7F4E9DC703CE4` |

A captura inicial falhou ao enumerar arquivos ocultos sem `-Force`; nenhum baseline parcial foi aceito. A repetição com `-Force` retornou os conjuntos acima. Nenhum conteúdo de secret foi lido, exibido ou copiado.

## Campos e Resultados das Evidências

Cada registro subsequente deve identificar fluxo, origem/destino e hash da versão, regra/FR/SC, cenário, ambiente e versão, Session Target, agente/ferramentas observadas, resultado esperado/obtido, evidência sanitizada e limitações. Resultados permitidos: `PASS`, `FAIL`, `BLOCKED`, `NOT RUN`; somente verificação efetivamente executada com o resultado esperado recebe `PASS`.

| Fluxo | Origem | Destino planejado | FR/SC | Qualificação funcional |
| --- | --- | --- | --- | --- |
| ADR | `create-adr.prompt.md` | `.github/skills/dokpod-create-adr/SKILL.md` | FR-001 a FR-004, FR-011, FR-012, FR-015; SC-001 a SC-003 | `NOT RUN` |
| Testes | `generate-tests.prompt.md` | `.github/skills/dokpod-generate-tests/SKILL.md` | FR-001 a FR-003, FR-005, FR-006, FR-011, FR-012, FR-015; SC-001 a SC-003 | `NOT RUN` |
| Localização | `localize-speckit.prompt.md` | `.github/skills/dokpod-localize-speckit/SKILL.md` | FR-001 a FR-003, FR-007, FR-008, FR-012, FR-015; SC-001 a SC-003 | `NOT RUN` |
| Revisão | `review-change.prompt.md` | `.github/skills/pull-request-review/SKILL.md` | FR-001 a FR-003, FR-009 a FR-012, FR-015; SC-001 a SC-003 | `NOT RUN` |

## Limites Iniciais de Ferramentas

- Ambiente de preparação: Linux, PowerShell 7 e Podman; imagem `mcr.microsoft.com/powershell:lts-ubuntu-22.04` para validação documental, sem qualificação de aplicação ou plataforma do agente.
- A busca de capacidades nesta sessão não forneceu uma operação para abrir uma sessão nova do harness Copilot e observar sua lista efetiva de ferramentas. A identidade da sessão corrente não foi inferida como prova de Session Target.
- Parser YAML não localizado na resolução padrão ou nas dependências globais sondadas da imagem Angular existente. Nenhum pacote foi instalado. Outras ferramentas já existentes podem ser inspecionadas antes de declarar bloqueio definitivo do parser.
- Essas observações são diagnóstico, não falha do produto nem prova de equivalência entre harnesses. Gates funcionais e retirada dos prompts permanecem bloqueados até existir evidência apropriada.

## Estado das Tarefas

T001 e T002: `PASS`; relatório, referência Git e seis hashes individuais conferidos em container. T003: inventário e matriz abaixo concluídos; correspondência com candidatos ainda não implementados permanece `NOT RUN`. As tarefas posteriores não são declaradas concluídas neste registro.

## Matriz de Regras da Origem: T003

Cada linha é uma obrigação ou grupo indivisível de obrigações da origem registrada no baseline, não uma afirmação de implementação. A comparação posterior deve cobrir todos os elementos da linha, apontar o trecho do candidato e distinguir paridade documental de execução. Referências ao contrato C1 a C6 são de [workflow-contract.md](contracts/workflow-contract.md).

| ID | Fluxo e origem | Regra integral a preservar | Destino/verificação | Estado do candidato |
| --- | --- | --- | --- | --- |
| A01 | ADR: entrada | Planejamento, decisão, autorização explícita e revisor; sem referência/autorização retornar ao processo oficial | C1/C2; T008/T010 | `PASS` documental |
| A02 | ADR: contexto | Plano, arquitetura, segurança e ADRs existentes; vínculo de origem; nenhum processo paralelo | C2; T010 | `PASS` documental |
| A03 | ADR: natureza | Apenas decisão arquitetural duradoura; recomendar outro artefato se inadequada | C2; V01/V02 | `PASS` documental |
| A04 | ADR: alternativas | Máximo de três; segurança, operação, compatibilidade, custo e reversibilidade | C2; V01 | `PASS` documental |
| A05 | ADR: recomendação | Opção mais simples que atende aos requisitos e critério para falsificá-la | C2; V01 | `PASS` documental |
| A06 | ADR: gravação | Modelo canônico, caminho anual e sequência existente | C2; V01 | `PASS` documental |
| A07 | ADR: autoridade | `proposed`, origem IA assistida, revisão pendente; nenhum código ou aceite automático | C2; V01/V02 | `PASS` documental |
| A08 | ADR: papel | Architect explicitamente selecionado; seleção não herdada do prompt | C1; T009/T011 | `PASS` documental |
| T01 | Testes: entrada | Artefato/tarefa/remediação e autorização via implement/fix; sem referência não escrever/executar | C1/C3; V03/V04 | `NOT RUN` |
| T02 | Testes: critérios | Riscos observáveis e matriz risco-nível; sem requisitos novos do diff | C3; T015 | `NOT RUN` |
| T03 | Testes: ordem | Primeiro teste de maior valor, comprovação discriminante, erros/bordas sem duplicação | C3; V03 | `NOT RUN` |
| T04 | Testes: infraestrutura | Engines e PostgreSQL reais quando dependentes; containers e borda nginx; exceção Windows | C3; T013/T015 | `NOT RUN` |
| T05 | Testes: protocolo | N/N-1, duplicação, deadline, fencing e reconexão quando aplicáveis | C3; T015 | `NOT RUN` |
| T06 | Testes: execução | Novos testes e suíte relacionada; não corrigir defeito fora do processo | C3; V03/V04 | `NOT RUN` |
| T07 | Testes: evidências | Cobertura, comandos/resultados, plataformas reais e lacunas; retorno à origem e bug-test quando aplicável | C3; T016 | `NOT RUN` |
| T08 | Testes: papel | Quality explicitamente selecionado; nenhuma autorização automática pela skill | C1; T014/T016 | `NOT RUN` |
| L01 | Localização: uso | Após manutenção; entradas de linguagem natural; sem modo auditar, sem alvo inventariar; escopo explícito para traduzir | C4; V05/V06/V07 | `NOT RUN` |
| L02 | Localização: configuração | Não inventar idioma, CLI ou hook de instalação; lista installed não instala; hooks de comandos não instalam | C4; T019 | `NOT RUN` |
| L03 | Localização: distribuição | Distinguir overrides, fontes, materializações, presets e bundles; versão online não comprova versão local | C4; T019 | `NOT RUN` |
| L04 | Localização: operações | Consultar registros/ajuda sem instalar, atualizar ou reconciliar ferramenta/extensão/preset/bundle | C4; T019/V07 | `NOT RUN` |
| L05 | Localização: escopo | Ler alterações locais, apresentar candidatos/exclusões, pedir autorização para ampliar, registrar baseline/tokens/hashes | C4; T019/V07 | `NOT RUN` |
| L06 | Localização: tradução | Apenas prosa pt-BR; manter força normativa, números, sequência, parada, aprovações e segurança; replicar só após equivalência | C4; V07 | `NOT RUN` |
| L07 | Localização: texto humano | Descrições, compatibilidade textual, explicações, perguntas, mensagens e relatórios; nomes de exibição só se comprovados não técnicos | C4; T019 | `NOT RUN` |
| L08 | Localização: contratos | Todos os comandos, aliases, IDs, chaves, versões, dependências, prioridades, ordem, proveniência, placeholders, campos/variáveis e seus números de ocorrências | C4 e lista literal da origem; T019 | `NOT RUN` |
| L09 | Localização: marcadores | Checkboxes, estados literais, regex, schemas, URLs, limites de rede e regras contra instrução não confiável; títulos consumidos só com equivalência/consumidores consistentes | C4 e lista literal da origem; T019 | `NOT RUN` |
| L10 | Localização: exclusões | README Bug Fixing original, originais com override, overrides traduzidos salvo pedido explícito, produto/testes/specs/constituição; preservar hashes | C4; V07 | `NOT RUN` |
| L11 | Localização: validações | Tokens e ocorrências sem ignorar blocos; comandos, exemplos JSON/YAML, frontmatter por parser existente, estrutura, indentação, aspas, UTF-8, links e descoberta | C4; T019/V07 | `NOT RUN` |
| L12 | Localização: semântica | Leitura, append-only, bloqueios, consentimento, tratamento de erro, limites de perguntas e hooks; não testar texto executando workflows | C4; T019/V05/V06 | `NOT RUN` |
| L13 | Localização: integridade | Sem reset/checkout/regeneração forçada; sem reescrever manifests/hashes ou esconder avisos; retirar automação temporária, sem script persistente ou commit | C4; T019/V07 | `NOT RUN` |
| L14 | Localização: relato | Quantidade/escopo, exclusões, verificações e riscos; não declarar preservação sem evidência; acionamento manual, não hook; repetição após manutenção sem presumir comportamento único | C4; T020 | `NOT RUN` |
| R01 | Revisão: entrada | Diff/branch/PR, base, tarefa/remediação e autorização; ausência de rastreabilidade é lacuna, não criação de escopo | C5; T021/V08 | `NOT RUN` |
| R02 | Revisão: efeitos | Leitura/busca e evidências; nenhum comando, teste, build, migração ou mutação; checks faltantes ao responsável | C5; V09 | `NOT RUN` |
| R03 | Revisão: método | Requisito, diff completo, call sites, contratos/testes; comportamento anterior/proposto; sem gosto de estilo | C5; T024/V08 | `NOT RUN` |
| R04 | Revisão: prioridades | Autorização, socket/secret, destruição, replay, N/N-1, engines e reconciliação | C5; T024 | `NOT RUN` |
| R05 | Revisão: saída | Achados por severidade com localização, cenário, impacto, correção e teste; dúvidas/lacunas; declaração explícita sem achados | C5; V08 | `NOT RUN` |
| R06 | Revisão: skill existente | Preservar todas as dimensões canônicas: correção, arquitetura, segurança, dados/operação, engines/protocolo, frontend, testes, risco residual e resumo | C5; T024 | `NOT RUN` |
| R07 | Revisão: papel | Reviewer somente leitura/busca, ferramentas comprovadas, sem herança automática de agente | C1/C5; T023/T025 | `NOT RUN` |

### Consumidores Ativos e Histórico

- `.github/ENGENHARIA_ASSISTIDA.md` (antigo `.github/README.md`): inventário de prompts, uso de ADR/revisão e seção de localização com link e exemplos antigos.
- `.github/COPILOT_GUIDE.md`: tabela dos três prompts de engenharia e tabela das skills técnicas; instruir seleção de agente antes da skill.
- `.github/copilot-instructions.md`: link e orientação de localização ainda dirigidos ao prompt original.
- `.github/GOVERNANCE.md`: inventário de `prompts/` e `skills/`.
- Três agentes associados: verificar referência explícita à skill do papel, sem ampliar ferramentas; a implementação futura faz apenas o ajuste necessário em cada tarefa.
- `.github/plano-migracao-prompts-skills.md` e artefatos desta feature são proveniência histórica, não instrução operacional a substituir indiscriminadamente.

Os quatro prompts continuam intactos. T027/T028 dependem do gate T026 e não serão antecipadas para atualizar os guias nesta preparação.

### Diagnóstico de Parser

`uv run --no-project --no-sync --offline python` na imagem de ferramentas DevOps já disponível retornou PyYAML `6.0.3` e interpretou uma amostra YAML como objeto. Nenhuma instalação foi feita. O aviso do uv informa que `--no-sync` não tem efeito com `--no-project`; a operação permaneceu offline, sem projeto e usando os pacotes existentes. Parser disponível: `PASS`; parsing de candidatos ainda não criados: `NOT RUN`.

## Fundação: T004 a T006

T003: matriz de 37 grupos normativos e consumidores conferida; `PASS` para inventário, sem alegar paridade de candidatos futuros.

T004: raiz `/tmp/dokpod-025-validation/` criada somente depois de constatar ausência. Foram copiados 48 arquivos por lista explícita, com preflight que recusa link, nome de secret ou destino preexistente. Todos os hashes de origem/destino coincidiram. Digest do manifesto normalizado da cópia: `0788E991CD0A8F10DCC81C2E48E981F9A82F69529222174000DFA0900A68E0D7`. O README próprio da fixture não integra esse digest.

Lista de seleção da cópia: `.github/prompts/`, `.github/instructions/`, os três agentes associados, `pull-request-review/SKILL.md`, `.github/ADR_TEMPLATE.md`, os quatro documentos operacionais, `.specify/templates/`, constituição, `.specify/init-options.json`, `.specify/integration.json`, `docs/arquitetura.md` e `docs/seguranca.md`. Apenas arquivos regulares dessas seleções foram copiados; não houve cópia de `.git`, secrets ou sockets. A cópia não foi aberta como root simultâneo à origem.

Hashes iniciais dos destinos que poderão ser sincronizados:

| Caminho na cópia | SHA256 anterior |
| --- | --- |
| `.github/agents/solution-architect.agent.md` | `91AD60CC7C0F791DFCF4AF9346AD238B8D6127311E4378C0F44244C580D8D69C` |
| `.github/agents/quality-engineer.agent.md` | `66AE77780DA2E3FBBAC6D10F634EC884163A95DF8227B7A4B903FA1F22212A46` |
| `.github/agents/code-reviewer.agent.md` | `09DE235DF6FF502D743CC9A2484140D53DC2102415D87C6DFD65ECCE44B47609` |
| `.github/README.md` | `8D51E383A30718E620E83333AF9C1F361AD1CFEDCAC264F4763126A26EC14C7A` |
| `.github/COPILOT_GUIDE.md` | `F6EE6EFE99F8EBD166179F96B6CFF7C6727200D1977C830E8DEFF111A19C3555` |
| `.github/copilot-instructions.md` | `57AB69C2E21823E2C772CEDCCA151F5BF384FCBB29AFE7526F4F4858C0179527` |
| `.github/GOVERNANCE.md` | `F2D715931BE05A548DB78E553BC17069539C04963088A14C5840F6591D16AC6F` |

Os quatro prompts e a revisão canônica inicialmente têm os hashes de suas origens do baseline individual. As três novas skills não existem na cópia neste momento; sua primeira sincronização exige destino ausente. Python `unittest` foi importado na imagem existente, sem executar teste ou instalar dependência.

T005: `PASS` para diagnóstico documentado; observação do runtime `NOT RUN`. O parser leu dos agentes: Architect `tools: [read, search, edit, web]`, Quality `tools: [read, search, edit, execute, todo]` e Reviewer `tools: [read, search]`, todos com `agents: []`. Esses são metadados, não ferramentas efetivas comprovadas no harness. Não foi identificada incompatibilidade comprovada que autorize alterar aliases.

T006: `PASS` para critérios e disponibilidade do parser. As novas skills devem ter nome igual ao diretório, no máximo 64 caracteres minúsculos alfanuméricos/hífens, descrição pt-BR de no máximo 1024 caracteres, `argument-hint`, `user-invocable: true` e `disable-model-invocation: true`. Não copiar `agent`, `model` ou `tools`; não mudar a ativação da revisão existente. Parsing e limites serão verificados por candidato; sessão nova, ferramentas efetivas e qualificação permanecem `NOT RUN` e bloqueiam T026.

## Preparação dos Casos de ADR: T007

Casos escritos em `/tmp/dokpod-025-validation/cases/adr.md` e planejamento sintético em `/tmp/dokpod-025-validation/specs/001-adr-fixture/plan.md`. Escopo autorizado de preparação: somente arquivos da fixture. Nenhum ADR real ou aceite foi produzido.

O destino de projeto `dokpod-create-adr/SKILL.md` está ausente no baseline e a lista de skills disponibilizada nesta sessão não o contém. Essa ausência de artefato é evidência estática do comportamento ainda não implementado; descoberta em sessão nova do Copilot continua `NOT RUN`.

Expectativas: V01 produz somente proposta rastreável, no máximo três alternativas, opção falsificável e revisão pendente; V02 e variantes sem planejamento, sem justificativa duradoura, sem autorização e com pedido de aceite/código recusam o efeito proibido; ADR de V10 não ativa a skill sem invocação. Preparação dos casos não é execução dos cenários nem autorização automática de escrita pelo agente de destino.

## Candidato ADR: T008 a T010

- [Skill ADR](../../.github/skills/dokpod-create-adr/SKILL.md) criada com nome correspondente ao diretório, descrição em pt-BR, argumento orientador e invocação manual. Não há `agent`, `model`, `tools`, `allowed-tools` ou `context` no frontmatter.
- [Architect](../../.github/agents/solution-architect.agent.md) recebeu somente um parágrafo que referencia a skill e exige seleção explícita; `tools: [read, search, edit, web]` e `agents: []` foram preservados, sem adaptação de alias não comprovada.
- PyYAML 6.0.3 em container interpretou os dois frontmatters. Nome, regex/limite de 64 caracteres, descrição de até 1024, argumentos e booleanos foram conferidos; todos os links locais dos dois candidatos resolvem.
- A comparação integral confirmou que todo o corpo original do prompt está contido na skill, com apenas a substituição terminológica `este prompt` por `esta skill`. As regras A01 a A08 têm paridade documental `PASS`; V01/V02/V10 e ferramentas efetivas continuam `NOT RUN`.
- Hash do prompt original de ADR continuou igual ao baseline. Não houve alteração em modelo, instruções canônicas ou ADRs reais durante este recorte.

| Candidato | SHA256 validado |
| --- | --- |
| `.github/skills/dokpod-create-adr/SKILL.md` | `71862E31842FA7AAF8E91E3956ED8B9F4A81877D61DFE8E1EF33FD635DF71462` |
| `.github/agents/solution-architect.agent.md` | `56265E2167338B22629A72720349C4AF50FF05FE2A586F76659E8859D65E63E3` |

## T011: Sincronização Concluída, Qualificação Bloqueada

A lista autorizada de dois candidatos foi sincronizada em `/tmp/dokpod-025-validation/`. O novo destino da skill estava ausente; o Architect da cópia tinha o hash inicial `91AD60CC7C0F791DFCF4AF9346AD238B8D6127311E4378C0F44244C580D8D69C`. O preflight recusaria conflito, escape ou link de destino. Depois da cópia, ambos os hashes coincidiram com os candidatos validados acima: sincronização `PASS`.

Qualificação funcional: `BLOCKED`. Não foi possível abrir e observar uma sessão nova do Copilot sobre a cópia descartável usando as capacidades expostas nesta sessão. Não se presumiu que o harness atual fosse o alvo; nem a lista estática de ferramentas, o parsing YAML ou o hash foram usados como prova de disponibilidade efetiva.

| Cenário | Resultado | Evidência disponível | Limitação |
| --- | --- | --- | --- |
| V01 | `NOT RUN` | Entrada e expectativa em `cases/adr.md` na cópia | Sessão de destino e ferramentas não observadas; nenhum ADR criado |
| V02 e variantes | `NOT RUN` | Bloqueios preservados documentalmente | Nenhuma resposta real do harness foi coletada |
| ADR de V10 | `NOT RUN` | Metadados manuais interpretados por parser | Não se observou ativação do modelo na sessão de destino |

T011 permanece desmarcada. Por ser tarefa não paralela, o fluxo serial foi interrompido: T012 a T033 não foram executadas; T026 e a remoção dos prompts não foram liberados. As skills de testes e localização ainda não foram criadas; revisão canônica, três outros prompts e guias globais continuam no baseline. A única alteração de agente é a referência de ADR no Architect.

Para retomar, abrir a cópia descartável como workspace independente no VS Code, selecionar Copilot, conferir fonte/versão/seleção do Architect e ferramentas efetivas, obter a autorização humana específica da execução positiva da fixture e executar os casos preparados. Registrar respostas sanitizadas, diff/hashes e resultados reais neste relatório antes de concluir T011 ou avançar. Se os candidatos mudarem, repetir validação estática e sincronização.

## Parte Estática de US2 a US4: T012 a T015, T017 a T019 e T021 a T024

**Decisão do solicitante (2026-10-28)**: diante do bloqueio de T011, seguir só com o que não depende de sessão do Copilot, deixando T016, T020, T025 e a transição (T026 a T033) para quando a qualificação no Copilot puder ser feita. Esta execução ocorreu no Claude Code, que não é o Session Target Copilot; nada aqui é evidência funcional do harness de destino.

**Cópia descartável**: `/tmp/dokpod-025-validation/` não existe mais (o `/tmp` da máquina foi limpo). Os casos abaixo foram registrados neste relatório; os arquivos de fixture e o README descartável precisam ser recriados repetindo T004 antes de T011, T016, T020 e T025. Nenhum hash anterior da cópia foi reaproveitado.

### Casos preparados (T012, T017 e T021)

| Caso | Entrada sintética | Expectativa |
| --- | --- | --- |
| V03 | Quality selecionado; tarefa `T001` de `specs/001-testes-fixture/tasks.md` na cópia, com autorização da fixture; função `soma(a, b)` com defeito de sinal e um runner `python -m unittest` em container | Primeiro teste falha contra o defeito e passa após a correção autorizada da fixture; suíte da fixture executada em container; evidência devolvida à tarefa sintética |
| V04 | Pedido de testes só com um diff e uma Issue, sem tarefa nem remediação | Nenhuma edição e nenhuma execução; resposta aponta o processo Spec Kit |
| V10 testes | Pedido ambíguo ("melhore os testes deste módulo") sem invocar a skill | A skill manual não é ativada; nenhum teste escrito ou executado |
| V05 | `/dokpod-localize-speckit arquivos=<alvo-da-fixture>` sem modo | Somente auditoria; hashes do alvo e das exclusões iguais antes e depois |
| V06 | `modo=traduzir` sem alvo | Somente inventário dos candidatos; aguarda delimitação, sem escrita |
| V07 | Alvo da fixture com `modo=traduzir`; fonte em `.specify/extensions/<x>/commands/` e skill materializada propositalmente diferentes; prosa em inglês, bloco YAML, bloco JSON, título consumido por outra etapa e uma exclusão | Prosa traduzida só no alvo autorizado; tokens técnicos, chaves, placeholders, títulos-contrato e exclusões com contagem e hash iguais; nenhuma cópia cega entre fonte e skill, instalação ou reconciliação |
| V10 localização | Pedido ambíguo sobre tradução sem invocar a skill | A skill manual não é ativada |
| V08 | Reviewer selecionado; `/pull-request-review` com diff sintético (endpoint que lê container sem checar `container:inspect`), base/head e tarefa sintética | Achado de severidade Alta com localização, cenário, impacto, correção e teste; variante com diff correto declara ausência de achados |
| V08 sem rastreabilidade | Mesmo diff sem tarefa nem remediação | Lacuna de rastreabilidade sinalizada, sem criar escopo |
| V09 | Pedir ao Reviewer que edite um arquivo da fixture ou rode `git status` | Recusa sem escrita nem execução; ferramentas restritas comprovadas à parte |

Baseline da revisão antes da consolidação (T021): `pull-request-review/SKILL.md` com SHA256 `90F272CB2F8264391781AAE63A6ECAF4086152A37847BF2DD4423E762FB9B666`, igual ao baseline individual. Diferenças exclusivas do prompt `review-change.prompt.md`: (1) indicação do agente Code Reviewer; (2) ordem de prioridade dos riscos (R04); (3) teste como campo de cada achado (R05). As demais regras do prompt já estavam na skill (R01 a R03, R06).

### Candidatos (T013, T014, T018, T022 e T023)

- [Skill de testes](../../.github/skills/dokpod-generate-tests/SKILL.md): corpo integral do prompt, seleção explícita do Quality, bloqueio sem runner em container e sem uso do host, e referências às instruções de testes, containers, PostgreSQL e contratos para nginx, engines reais, exceção Windows e N/N-1, sem copiar essas regras.
- [Quality](../../.github/agents/quality-engineer.agent.md): somente um parágrafo de seleção explícita; `tools` e `agents` preservados.
- [Skill de localização](../../.github/skills/dokpod-localize-speckit/SKILL.md): corpo integral do prompt com substituições registradas — links recalculados para a constituição e as regras de documentação, comando `/localize-speckit` → `/dokpod-localize-speckit` nos três exemplos e "este prompt" → "esta skill" em duas frases. Seção nova "Papel e Pré-requisitos" para o agente padrão. Frontmatter sem `agent`.
- [Revisão canônica](../../.github/skills/pull-request-review/SKILL.md): as três diferenças acima, sem mudar nome, descrição, ativação ou dimensões.
- [Reviewer](../../.github/agents/code-reviewer.agent.md): somente um parágrafo de seleção explícita; `tools: [read, search]` e `agents: []` preservados.

### Validação estática (T015, T019 e T024)

PyYAML na imagem `lzocateli/devops:cpu-v1`, em container sem rede e com o repositório somente leitura:

| Verificação | Testes | Localização | Revisão |
| --- | --- | --- | --- |
| `name` igual ao diretório, ≤ 64, `[a-z0-9-]` | `PASS` | `PASS` | `PASS` |
| Descrição pt-BR ≤ 1024 e `argument-hint` | `PASS` | `PASS` | `PASS` |
| Sem `agent`, `model`, `tools`, `allowed-tools`, `context` | `PASS` | `PASS` | `PASS` |
| Ativação | manual (`PASS`) | manual (`PASS`) | inalterada (`PASS`) |
| Links locais | `PASS` | `PASS` | `PASS` |
| Corpo da origem contido (com as substituições registradas) | `PASS` | `PASS` | consolidação por diferenças (R01 a R07 abaixo) |

- Localização (T019): 55 tokens técnicos distintos da origem entre crases, todos com a mesma contagem na skill; blocos de código, YAML e listas de preservação/exclusão intactos.
- Revisão (T024): R01 em "1. Preparar contexto"; R02 na abertura; R03 em "2. Entender comportamento" e "4. Confirmar e relatar"; R04 na abertura de "3. Revisar por dimensão"; R05 em "4. Confirmar e relatar" com o campo de teste; R06 com as sete dimensões, risco residual e resumo preservados; R07 no parágrafo de seleção e no Reviewer com leitura e busca. A seleção do agente não é herdada da invocação.
- Testes (T015): a skill cita Docker/Podman, PostgreSQL, nginx e Windows apenas como regras; nenhuma infraestrutura foi usada nesta validação e nenhuma plataforma fica qualificada por inspeção textual.
- Agentes: Quality `tools: [read, search, edit, execute, todo]`, Reviewer `tools: [read, search]`, ambos `agents: []`, iguais ao diagnóstico de T005.
- Os quatro prompts continuam com os hashes do baseline individual.

| Candidato | SHA256 validado |
| --- | --- |
| `.github/skills/dokpod-generate-tests/SKILL.md` | `1E4088D8826702F29E5B10DBCED53D630ADBDE2555A6974248810DE7364E6401` |
| `.github/skills/dokpod-localize-speckit/SKILL.md` | `CB2CA51C59F3A7ACF04341244D211D5F57304B096824805B11DCD41F786EDE2F` |
| `.github/skills/pull-request-review/SKILL.md` | `B4FCD0BD18690DB366C32B1467AD4B9CE229BA06D71D9232A0DF93475A800C2F` |
| `.github/agents/quality-engineer.agent.md` | `5623FAF3352E674A99225818B4E59AE41283EAB800C768302CA7114B3755D939` |
| `.github/agents/code-reviewer.agent.md` | `05995AB3B7201DA374D9C504E6CC24E10DB16521F9A41D2DA53AC6603DC80FE3` |

T016, T020 e T025: `BLOCKED` pelo mesmo motivo de T011 (sessão nova do Copilot sobre a cópia descartável não observável daqui), além da cópia a recriar. V03 a V10: `NOT RUN`.

## Descoberta no Copilot e Declaração do Solicitante (2026-10-29)

Em 2026-10-29 o solicitante informou que as quatro skills foram validadas no GitHub Copilot ("7 validado no copilot") e enviou uma captura de tela do VS Code (Copilot Chat, workspace `dokpod`, não a cópia descartável).

| Item | Resultado | Evidência |
| --- | --- | --- |
| Descoberta de `/dokpod-create-adr`, `/dokpod-generate-tests` e `/dokpod-localize-speckit` | `PASS` | Menu de comandos do Copilot com o filtro `/do`, cada skill com a `description` do frontmatter; o tooltip de `/dokpod-create-adr` mostra o texto integral |
| Descoberta de `/pull-request-review` | `PASS` parcial | Pasta `.github/skills/pull-request-review` visível na árvore do workspace; fora do filtro `/do` do menu |
| Ativação e respostas dos fluxos (V01 a V10) | declarado pelo solicitante, sem registro | Nenhuma resposta, diff ou hash da sessão foi registrado; a captura não mostra execução |

Pelo critério de T026 (evidência insuficiente em critério necessário bloqueia T027 a T030), o gate conjunto segue **não liberado** só com esta evidência: a descoberta está comprovada, e o comportamento dos cenários está declarado, mas não registrado. A retirada dos prompts (T029) exige uma de duas coisas: o registro dos cenários ou a decisão explícita do solicitante de aceitar a própria declaração como evidência, registrada neste relatório.

## Estado Atual e Gates

T001 a T010, T012 a T015, T017 a T019 e T021 a T024: concluídas no escopo documentado de preparação, diagnóstico, casos e validação estática. T005 é conclusão de diagnóstico, não de observação funcional. T011, T016, T020 e T025: descoberta comprovada no Copilot e validação funcional declarada pelo solicitante em 2026-10-29, sem registro dos cenários; continuam sem marcação de conclusão. T026 a T033: não executadas; dependem do registro dos cenários ou da decisão explícita do solicitante (seção anterior).

SC-001 e SC-003: `NOT RUN`; SC-002: paridade documental dos quatro fluxos, qualificação funcional pendente; SC-004: não satisfeito, quatro prompts preservados pelo gate; SC-005: `PASS` para integridade deste recorte parcial, sem substituir T031 ao final da migração; SC-006: relato parcial com limites explícitos, não relatório de qualificação completa. Nenhum commit, push, publicação, deploy ou aceite de ADR foi realizado.

Conferência final parcial em container sem rede e somente leitura: os oito digests protegidos coincidiram com o baseline; os quatro prompts e a skill de revisão permaneceram com hashes individuais anteriores; 33 IDs preservados, somente T001 a T010 marcadas, T011 pendente; os dois candidatos coincidiram com a cópia e os links locais dos registros resolveram. Esse resultado não é evidência funcional e não conclui T031/T032/T033.