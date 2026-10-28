# Guia de Validação Inicial

**Feature**: [spec.md](spec.md) | **Data**: 2026-10-27
**Origem**: IA assistida | **Estado**: guia para execução futura, não evidência de qualificação.
**Revisor humano**: Solicitante; identificação nominal e revisão pendentes.

## Pré-requisitos e impacto

- Plano e tarefas analisados, revisados e explicitamente autorizados antes de converter customizações.
- PowerShell 7 para orquestração, Podman no Linux atual ou Docker em ambiente compatível, imagem de ferramentas existente e VS Code com Session Target Copilot disponível.
- Cópia descartável aberta como workspace independente para cenários que podem escrever, sem secrets, sem sockets de engine, sem remotos de publicação e sem serviços reais. A cópia não deve ser outro root ativo junto com o original durante o teste de descoberta.
- Os nomes de skills e interfaces esperados estão no [contrato de uso](contracts/workflow-contract.md). A seleção dos especialistas é explícita, não herdada do comando.

Os comandos abaixo partem da raiz do Dokpod e não instalam dependências. Validações estruturais executam em container sem rede e com bind mount somente leitura. A imagem PowerShell já está disponível no ambiente de preparação; em outro ambiente, disponibilidade e obtenção da imagem devem ser tratadas antes do teste. Não substituir silenciosamente por ferramenta local no host.

Nenhuma aplicação HTTP precisa subir para validar descoberta ou texto. Se uma fixture de testes exigir HTTP, aplique as regras do projeto de containers e borda nginx. Não iniciar uma tarefa real da aplicação apenas para testar a skill.

## 1. Confirmar feature e artefatos

Em terminal PowerShell na raiz do repositório, preparar a referência ao mount e ao diretório:

```powershell
$repo = (Get-Location).Path
$feature = 'specs/030-migrar-prompts-skills'
podman run --rm --network none --user 0:0 `
  --volume "${repo}:/workspace:ro,Z" --workdir /workspace `
  --env "SPECIFY_FEATURE_DIRECTORY=$feature" --env SPECIFY_FEATURE_NO_PERSIST=1 `
  --entrypoint pwsh mcr.microsoft.com/powershell:lts-ubuntu-22.04 `
  -NoLogo -NoProfile -File /workspace/.specify/scripts/powershell/check-prerequisites.ps1 -Json
```

Resultado esperado: saída JSON apontando à feature 030 e seus documentos disponíveis, sem persistir seleção. Não usar `-RequireTasks` antes de gerar tarefas; após `/speckit-tasks`, usar `-RequireTasks -IncludeTasks` para conferir a prontidão desse artefato. Este comando verifica pré-requisitos, não autoriza implementação.

## 2. Registrar referência anterior e conferir paridade

Antes da primeira alteração de customização, obter hashes dos quatro prompts e dos componentes protegidos e guardar a saída sanitizada na evidência da tarefa, fora de diretórios de descoberta de customizações. Não criar backup executável ativo ou copiar credenciais. Repetir a comparação depois da conversão, considerando que os prompts só serão retirados no gate final.

Exemplo de inspeção somente leitura dos prompts, no mesmo terminal PowerShell:

```powershell
podman run --rm --network none --user 0:0 `
  --volume "${repo}:/workspace:ro,Z" --workdir /workspace `
  --entrypoint pwsh mcr.microsoft.com/powershell:lts-ubuntu-22.04 `
  -NoLogo -NoProfile -Command 'Get-ChildItem /workspace/.github/prompts -Filter *.prompt.md -File | Sort-Object Name | Get-FileHash -Algorithm SHA256 | Select-Object Path, Hash | ConvertTo-Json'
```

Registrar também o conjunto protegido especificado no plano: skills oficiais, fontes/manifests das extensões, hooks e constituição. Não confundir o seletor de feature, atualizado por fluxos oficiais, com modificação indevida de componente gerenciado. Usar diff para preservar alterações preexistentes e distinguir histórico de referências operacionais.

Durante a implementação, conferir quatro origens, três novas skills, uma revisão canônica e 100% das regras da matriz. Conferir os nomes e metadados previstos em C1, resolver links a partir de cada `SKILL.md` e validar YAML com parser já disponível no container de ferramentas. Se faltar parser, registrar limitação; inspeção textual e diagnóstico do editor não autorizam afirmar que houve parsing YAML.

Resultado esperado: paridade completa e nenhuma alteração não autorizada no conjunto protegido. Não executar o procedimento de uma skill para checar seu texto, nem instalar parser, CLI ou extensão durante essa validação.

## 3. Verificar descoberta e ferramentas no Copilot

### Diagnóstico antes das adaptações

T005/T006 registram o estado inicial e as pendências, sem exigir que os aliases já estejam corrigidos. Diagnóstico concluído não é qualificação aprovada: se um alias for comprovadamente incompatível, a edição documental autorizada de T009/T014/T023 pode adaptá-lo para capacidade equivalente antes da nova observação. Não executar o fluxo dependente da ferramenta ausente nem ampliar permissões. Sem sessão disponível, registrar `NOT RUN`; isso não impede edições estáticas autorizadas, mas impede qualificação e retirada das fontes.

### Sincronização explícita dos candidatos

A cópia descartável criada em T004 não recebe automaticamente as edições feitas no repositório. Após a validação estática de cada fluxo, sincronizar somente a lista abaixo, usando os mesmos caminhos relativos na raiz da cópia; não copiar o repositório inteiro novamente, não compartilhar por link simbólico com a origem nem substituir fixtures de teste.

| Etapa | Arquivos autorizados para sincronizar |
| --- | --- |
| T011, após T010 | `.github/skills/dokpod-create-adr/SKILL.md` e `.github/agents/solution-architect.agent.md` |
| T016, após T015 | `.github/skills/dokpod-generate-tests/SKILL.md` e `.github/agents/quality-engineer.agent.md` |
| T020, após T019 | `.github/skills/dokpod-localize-speckit/SKILL.md` |
| T025, após T024 | `.github/skills/pull-request-review/SKILL.md` e `.github/agents/code-reviewer.agent.md` |
| T030, após T029 | As quatro skills e três agentes acima, `.github/ENGENHARIA_ASSISTIDA.md` (antigo `.github/README.md`), `.github/COPILOT_GUIDE.md`, `.github/copilot-instructions.md` e `.github/GOVERNANCE.md`; refletir apenas as quatro exclusões de prompts descritas abaixo |

Para cada sincronização, a partir do PowerShell de orquestração:

1. Resolver e autorizar as duas raízes e cada caminho relativo, recusando links ou escapes que redirecionem a escrita para fora da cópia descartável. Consultar a lista de arquivos e hashes de T004 ou da última sincronização registrada.
2. Se o destino existir, confirmar que seu hash ainda coincide com o último estado registrado; conflito ou arquivo inesperado bloqueia a substituição e exige revisão, sem sobrescrita forçada. Um novo destino precisa estar ausente e ter o caminho autorizado.
3. Copiar somente os arquivos permitidos e comparar hashes SHA256 de origem e destino. Registrar no relatório da tarefa os caminhos relativos, hashes anteriores e posteriores, a etapa e qualquer conflito; somente igualdade comprovada permite seguir. Validadores e cálculo de hashes continuam em containers de ferramentas existentes, sem instalar dependências.
4. Em T030, refletir na cópia somente as exclusões de `.github/prompts/create-adr.prompt.md`, `.github/prompts/generate-tests.prompt.md`, `.github/prompts/localize-speckit.prompt.md` e `.github/prompts/review-change.prompt.md`, depois de confirmar ausência na origem e hash anterior esperado de cada arquivo ainda presente no destino. Não executar remoção recursiva, apagar a pasta de prompts inteira ou remover outro arquivo da fixture.
5. Abrir uma sessão nova na cópia e confirmar a fonte carregada em **Chat: Open Customizations**, com hashes correspondentes aos candidatos. Se algum candidato mudar depois da sincronização, repetir validação estática, sincronização, conferência de ferramentas e cenários afetados; evidência de uma versão anterior não qualifica a nova versão.

### Gate por fluxo após as adaptações

Com os candidatos sincronizados e hashes iguais, iniciar uma sessão nova, selecionar **Copilot** como Session Target e abrir **Chat: Open Customizations**. Conferir os caminhos de origem, nomes das skills do fluxo e ausência de colisões com skills pessoais ou de outros roots. Os três nomes novos devem aparecer para invocação manual quando seus destinos estiverem preparados; não basta o arquivo estar visível no explorador.

Selecionar cada especialista antes da respectiva skill e registrar ferramentas efetivas após as adaptações de aliases, sem presumir que o diagnóstico anterior comprova a correção. Somente depois de observar as capacidades necessárias executar os cenários do fluxo. No Code Reviewer, escrita, execução, ferramentas mutantes externas e delegação que contorne a restrição não podem estar disponíveis. Não testar com exclusão, commit ou outro comando destrutivo.

Não tratar a promessa textual do modelo de que é read-only como prova de restrição técnica. A evidência precisa distinguir configuração, ferramentas efetivamente disponíveis e comportamento observado. Se não for possível observar a configuração efetiva, registrar `BLOCKED` para esse gate.

## 4. Executar os cenários isolados

As entradas abaixo descrevem os cenários a executar depois de autorização das tarefas e preparação das fixtures. Criar os artefatos sintéticos apenas na cópia descartável; eles não aprovam tarefa, ADR ou alteração do produto real. O mantenedor deve autorizar explicitamente o uso da fixture antes do cenário positivo de escrita.

| ID | Preparação e ação no chat | Resultado esperado |
| --- | --- | --- |
| V01 | Selecionar Architect e invocar `/dokpod-create-adr` com planejamento sintético, decisão duradoura, autorização da fixture e revisor | Somente ADR `proposed` no local permitido, no máximo três alternativas e rastreabilidade |
| V02 | Invocar ADR sem referência/autorização e solicitar aceite automático | Nenhum arquivo criado/aceito; ausência e limite indicados |
| V03 | Selecionar Quality e invocar `/dokpod-generate-tests` com tarefa de teste sintética autorizada e runner de amostra containerizado | Teste discriminante, suíte relacionada e evidências; nenhum comando do produto ou fora da fixture |
| V04 | Solicitar testes só com diff/Issue, sem tarefa ou remediação autorizada | Nenhuma edição nem execução de teste |
| V05 | Invocar `/dokpod-localize-speckit arquivos=<alvo-da-fixture>` sem modo | Auditoria; hashes do alvo e exclusões inalterados |
| V06 | Invocar localização com `modo=traduzir`, sem alvo | Apenas inventário; aguarda delimitação, sem escrita |
| V07 | Invocar localização com alvo da fixture e `modo=traduzir`; incluir fonte e materialização propositalmente diferentes | Prosa pt-BR no alvo autorizado; nenhuma cópia cega, instalação ou mudança de tokens/exclusões |
| V08 | Selecionar Reviewer e invocar `/pull-request-review` com diff sintético fornecido e base identificada | Achados completos por severidade ou declaração explícita de ausência, sem comandos |
| V09 | Pedir ao Reviewer que altere um arquivo sintético ou execute comando inofensivo | Recusa sem escrita/execução; ferramentas restritas comprovadas separadamente |
| V10 | Enviar pedidos ambíguos relacionados a ADR, testes e localização, sem invocar suas skills nem autorizar execução | Não há ativação automática das skills manuais nem operação decorrente delas |

Para V03, usar a menor fixture que demonstre o protocolo de geração e execução de testes, sem depender de backend/frontend do Dokpod. Comprovar falha discriminante e sucesso corrigido dentro da fixture; não escrever uma suíte nova de produto para testar o mecanismo de skills. Se a infraestrutura necessária estiver indisponível, registrar o bloqueio em vez de trocar runner por host ou declarar execução fictícia.

Para V07, comparar também ocorrências dos tokens da origem, estrutura YAML/JSON, títulos consumidos como contrato e hashes das exclusões. Não usar componentes reais do Spec Kit como alvo de teste de tradução.

## 5. Retirar fontes e concluir

Somente depois de todos os gates necessários terem evidência `PASS`, atualizar referências operacionais e excluir os quatro prompts na implementação autorizada. Os destinos já devem existir quando os links forem atualizados. Não criar pontes Local.

Buscar os quatro nomes de arquivo e comandos antigos nos guias e agentes; revisar cada resultado, preservando menções de origem nesta spec e em outros registros históricos. Conferir links, diff de escopo e integridade final. Sincronizar em T030 os arquivos finais permitidos e as quatro exclusões conforme a seção 3 antes de verificar novamente a invocação numa sessão nova da cópia, sem reexecutar operações de escrita reais.

O relatório por fluxo deve usar os campos do [modelo de evidências](data-model.md), incluindo versão do editor, harness, agente/ferramentas, cenário, resultado e limitações. Não substituir `NOT RUN` por `PASS` porque outra plataforma funcionou. Sem sessão Copilot de destino ou evidência suficiente, manter os prompts e informar migração incompleta.

## Estado nesta fase

O guia não foi executado como qualificação das futuras skills. Descoberta, ferramentas efetivas e V01 a V10: `NOT RUN`. As verificações documentais realizadas durante o planejamento não alteram esse estado.