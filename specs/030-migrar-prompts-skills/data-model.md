# Modelo de Artefatos da Migração

**Feature**: [spec.md](spec.md) | **Data**: 2026-10-27
**Origem**: IA assistida | **Estado**: desenho proposto, revisão humana pendente.
**Revisor humano**: Solicitante; identificação nominal a confirmar.

Este modelo descreve informação documental e relações de rastreabilidade. Não introduz classes de aplicação, armazenamento, schema, migrations ou um novo registro de aprovação. Os requisitos continuam na spec, as decisões no plano e a execução será registrada nas tarefas oficiais.

## Fluxo Auxiliar

| Campo | Descrição | Validação |
| --- | --- | --- |
| Identificador | Nome canônico do procedimento | Um identificador por fluxo, sem colisão no contexto de descoberta |
| Origem | Um dos quatro prompts existentes | Registrar referência anterior antes da remoção |
| Destino | Skill nova ou skill canônica existente | Nome e diretório coincidem; revisão não ganha segunda implementação |
| Entradas | Contexto de usuário e informações obrigatórias | Determinam autorização, escopo e condição de parada |
| Política de invocação | Manual para as três novas skills; política vigente para revisão | Não alterar ativação automática silenciosamente |
| Papel de execução | Especialista selecionado ou agente padrão para localização | Nenhum papel é herdado implicitamente do prompt |
| Regras de paridade | Entradas, passos, limites, exclusões e saída | Cada regra original deve ter correspondência no destino |
| Referências | Modelos, instruções e artefato de origem | Caminhos relativos resolvidos a partir do novo arquivo |

Cardinalidade: quatro origens correspondem a quatro fluxos canônicos, sendo três destinos novos e um existente. Cada fluxo possui várias regras e várias evidências de validação.

## Vínculo de Execução

| Campo | Descrição | Validação |
| --- | --- | --- |
| Artefato Spec Kit | Planejamento, tarefa ou assessment/remediação correspondente | Autoridade vem do processo oficial, não da presença da skill |
| Decisão ou tarefa | Recorte específico solicitado | Não pode ampliar requisitos ou corrigir produto por fora do fluxo |
| Autorização | Referência à decisão humana que permite a operação | Ausência bloqueia escrita nos fluxos de ADR e testes |
| Revisor | Humano responsável pela revisão | Identificação pendente não é convertida em aceite automático |
| Alvo e modo | Dados de localização quando aplicáveis | Sem modo, auditoria; sem alvo, apenas inventário |
| Base e head | Contexto da revisão | Falta de rastreabilidade é reportada, sem criar escopo |

O vínculo muda conforme o fluxo. Não se exige autorização de implementação de produto para uma auditoria textual; isso não permite usar localização para alterar requisitos, tarefas, constituição ou produto. A revisão permanece possível para sinalizar rastreabilidade ausente, sempre sem escrita ou execução de comandos.

## Evidência de Validação

| Campo | Descrição | Validação |
| --- | --- | --- |
| Fluxo e requisito | Identificador e FR/SC cobertos | Toda regra exclusiva e todo fluxo devem possuir cobertura |
| Cenário | Entrada, precondições e resultado esperado | Positivo ou negativo; fixture isolada quando houver escrita |
| Ambiente | Versão do editor, Session Target, agente e ferramentas observadas | Identificar Copilot; Local não comprova o alvo |
| Tipo de verificação | Documental, estrutural, invocação ou efeito | Um tipo não substitui outro |
| Resultado | `PASS`, `FAIL`, `BLOCKED` ou `NOT RUN` | Somente observação executada com resultado esperado recebe `PASS` |
| Integridade | Referências anteriores e posteriores, hashes e diff de escopo | Nenhuma alteração fora do conjunto autorizado |
| Limitação | Ferramenta ausente, parser ausente ou plataforma não executada | Não omitir nem converter em sucesso |

Evidências devem conter somente dados sintéticos e saídas sanitizadas, sem prompts integrais sensíveis, tokens, secrets ou conteúdo de arquivos externos de credenciais. O formato de relatório é definido no [contrato](contracts/workflow-contract.md).

## Estado da Migração

| Estado lógico | Entrada | Próxima transição permitida |
| --- | --- | --- |
| Origem preservada | Quatro prompts disponíveis; spec/plano em revisão | Tarefas analisadas e autorizadas permitem conversão |
| Destinos preparados | Skills criadas/consolidadas, fontes ainda preservadas | Paridade e estrutura válidas permitem qualificação funcional |
| Paridade verificada | Regras, links e arquivos protegidos conferidos | Sessão nova no Copilot verifica invocação e efeitos |
| Qualificação demonstrada | Evidência funcional dos quatro fluxos e restrições | Revisão do gate permite atualizar consumidores e retirar fontes |
| Fontes retiradas | Quatro prompts excluídos; referências ativas atualizadas | Checagem final confirma ausência de links quebrados e desvios |
| Bloqueada | Falha, evidência ausente ou efeito proibido em qualquer gate | Reparar somente o recorte autorizado e repetir o gate afetado |

Os estados acima são o desenho das transições, não status atuais atribuídos aos artefatos. Se houver bloqueio antes da retirada, manter as fontes; se houver regressão depois, recuperar somente o diff da migração mediante revisão, sem sobrescrever trabalho do usuário. Checkboxes de tarefas futuras registram execução validada, nunca autorização ou aceite humano.