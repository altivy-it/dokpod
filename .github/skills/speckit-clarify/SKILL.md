---
name: "speckit-clarify"
description: "Identificar lacunas na spec atual com até 5 perguntas de esclarecimento focadas e registrar as respostas na especificação."
compatibility: "Requer a estrutura de projeto do spec-kit com o diretório .specify/"
metadata:
  author: "github-spec-kit"
  source: "templates/commands/clarify.md"
---


## Entrada do Usuário

```text
$ARGUMENTS
```

Você **DEVE** considerar a entrada do usuário antes de prosseguir (se não estiver vazia).

## Verificações Antes da Execução

**Verifique os hooks de extensões (antes do esclarecimento)**:
- Verifique se `.specify/extensions.yml` existe na raiz do projeto.
- Se existir, leia-o e procure entradas na chave `hooks.before_clarify`.
- Se o YAML não puder ser interpretado ou for inválido, não ignore silenciosamente: informe que `.specify/extensions.yml` não pôde ser lido (inclua o erro do parser) e que nenhum hook foi verificado, inclusive hooks obrigatórios (`optional: false`); depois, continue normalmente
- Exclua os hooks cujo `enabled` seja explicitamente `false`. Considere habilitados por padrão aqueles sem o campo `enabled`.
- Para cada hook restante, **não** tente interpretar ou avaliar expressões `condition`:
  - Se não houver `condition`, ou se ela for nula/vazia, considere o hook executável
  - Se houver `condition` não vazia, ignore o hook e deixe a avaliação da condição para a implementação de HookExecutor
- Ao construir invocações a partir dos nomes dos comandos de hooks, substitua pontos (`.`) por hífens (`-`). Por exemplo, `speckit.git.commit` → `/speckit-git-commit`.
- Para cada hook executável, apresente o seguinte conforme seu campo `optional`:
  - **Hook opcional** (`optional: true`):
    ```
    ## Hooks de Extensões

    **Hook Prévio Opcional**: {extension}
    Comando: `/{command}`
    Descrição: {description}

    Solicitação: {prompt}
    Para executar: `/{command}`
    ```
  - **Hook obrigatório** (`optional: false`):
    ```
    ## Hooks de Extensões

    **Hook Prévio Automático**: {extension}
    Executando: `/{command}`
    EXECUTE_COMMAND: {command}

    Aguarde o resultado do hook antes de prosseguir para o Roteiro.
    ```
    Após apresentar o bloco, você DEVE invocar o hook e aguardar sua conclusão. Execute-o como executaria o comando nesta sessão (a invocação pode diferir do identificador literal `{command}`; por exemplo, um agente em modo skills usa `/skill:speckit-...` ou `$speckit-...`). Apresentar apenas o bloco não executa o hook.
- Se não houver hooks registrados ou `.specify/extensions.yml` não existir, prossiga sem anunciar essa ausência

## Roteiro

Objetivo: Detectar e reduzir ambiguidades ou decisões ausentes na especificação ativa e registrar os esclarecimentos diretamente na spec.

Nota: Este fluxo deve executar e terminar ANTES de `/speckit-plan`. Se o usuário declarar explicitamente que o está pulando (por exemplo, investigação exploratória), pode prosseguir, mas avise sobre o maior risco de retrabalho posterior.

Etapas de execução:

1. Execute `.specify/scripts/powershell/check-prerequisites.ps1 -Json -PathsOnly` na raiz **uma vez** (modo combinado `--json --paths-only` / `-Json -PathsOnly`). Interprete os campos mínimos do JSON:
   - `FEATURE_DIR`
   - `FEATURE_SPEC`
   - Opcionalmente, registre `IMPL_PLAN` e `TASKS` para fluxos encadeados futuros.
   - Se a interpretação do JSON falhar, interrompa e oriente a repetir `/speckit-specify` ou verificar o ambiente da branch da feature.
   - Para aspas simples em argumentos como "I'm Groot", use escape: por exemplo, 'I'\''m Groot' (ou aspas duplas, se possível: "I'm Groot").

2. **SE EXISTIR**: Carregue `.specify/memory/constitution.md` para consultar princípios e restrições de governança.

3. Carregue a spec atual. Verifique ambiguidades e cobertura usando a taxonomia abaixo. Marque cada categoria como Clear / Partial / Missing (clara/parcial/ausente). Produza um mapa interno para priorizar; não apresente o mapa bruto, salvo se não houver perguntas.

   Escopo e comportamento funcional:
   - Objetivos principais do usuário e critérios de sucesso.
   - Declarações explícitas do que está fora do escopo.
   - Diferenciação de roles e personas.

   Domínio e modelo de dados:
   - Entidades, atributos e relacionamentos.
   - Regras de identidade e unicidade.
   - Ciclo de vida e transições de estado.
   - Premissas de volume e escala dos dados.

   Interação e fluxo de UX:
   - Jornadas e sequências críticas.
   - Estados de erro, vazio e carregamento.
   - Notas de acessibilidade ou localização.

   Atributos não funcionais de qualidade:
   - Desempenho (latência e metas de vazão).
   - Escalabilidade (horizontal/vertical e limites).
   - Confiabilidade e disponibilidade (tempo ativo e expectativas de recuperação).
   - Observabilidade (logs, métricas e rastreamento).
   - Segurança e privacidade (authN/Z, proteção de dados e premissas de ameaças).
   - Conformidade e restrições regulatórias, se houver.

   Integração e dependências externas:
   - Serviços/APIs externas e modos de falha.
   - Formatos de importação/exportação.
   - Premissas de protocolo e versionamento.

   Casos de borda e tratamento de falhas:
   - Cenários negativos.
   - Limitação de taxa e throttling.
   - Resolução de conflitos, como edições concorrentes.

   Restrições e compromissos:
   - Restrições técnicas (linguagem, armazenamento e hospedagem).
   - Compromissos explícitos ou alternativas rejeitadas.

   Terminologia e consistência:
   - Termos canônicos do glossário.
   - Sinônimos evitados e termos obsoletos.

   Indicadores de conclusão:
   - Testabilidade dos critérios de aceitação.
   - Indicadores mensuráveis de definição de concluído.

   Outros itens e placeholders:
   - Marcadores TODO e decisões não resolvidas.
   - Adjetivos ambíguos ("robusto", "intuitivo") sem quantificação.

   Para cada categoria Partial ou Missing, considere uma pergunta, exceto quando:
   - O esclarecimento não mudar materialmente a implementação ou a validação.
   - O item tratar especificamente de método de implementação, comparação de stacks ou decomposição de tarefas (registre internamente).

4. Gere internamente uma fila priorizada de perguntas (máximo de 5). NÃO apresente todas de uma vez. Aplique estas restrições:
    - No máximo 5 perguntas durante toda a sessão.
    - Cada pergunta deve admitir UMA destas respostas:
       - Escolha curta entre 2 a 5 opções distintas e mutuamente exclusivas; OU
       - Uma palavra/frase curta (limite explicitamente: "Responda em <=5 palavras").
    - Inclua somente perguntas com impacto material em arquitetura, modelagem, decomposição de tarefas, testes, UX, prontidão operacional ou conformidade.
    - Equilibre a cobertura: priorize categorias não resolvidas de maior impacto; evite duas perguntas pequenas enquanto uma área crítica, como segurança, continuar indefinida.
    - Exclua perguntas já respondidas, preferências triviais de estilo e detalhes de execução do plano, salvo se bloquearem a correção.
    - Prefira esclarecimentos que reduzam retrabalho ou evitem testes de aceitação desalinhados.
    - Se houver mais de 5 categorias indefinidas, selecione as 5 principais por (Impacto * Incerteza).

5. Ciclo sequencial de perguntas (interativo):
    - Apresente EXATAMENTE UMA pergunta por vez.
    - **Qualidade da redação (para perguntas de escolha ou resposta curta):**
       - Comece com `**Question:**`, seguido de uma pergunta completa terminada em `?`. O texto anterior ao `?` deve fazer sentido sozinho.
       - NUNCA use rótulo de tema, título de seção ou ID de requisito como pergunta. Por exemplo, `Acceptance device/runtime matrix (FR-023)` é INVÁLIDO: é um rótulo, não uma pergunta.
       - Após `?`, só é permitido um ID opcional de requisito/pergunta entre parênteses. Formato exato: `**Question:** <interrogative>?` ou `**Question:** <interrogative>? (FR-023)`. Nunca coloque o ID antes de `?` nem use apenas o ID ou rótulo como solicitação.
       - Logo após a pergunta, explique em uma frase simples por que ela importa para aceitação/entrega, antes da recomendação/opções.
       - Use linguagem cotidiana; defina jargões na mesma frase. Um leitor sem conhecimento do Spec Kit deve conseguir responder só com a linha da pergunta. Concisão é válida; rótulos enigmáticos não.
    - Para múltipla escolha:
       - **Analise todas as opções** e determine a **mais adequada** com base em:
          - Boas práticas para o tipo de projeto.
          - Padrões de implementações semelhantes.
          - Redução de riscos (segurança, desempenho, manutenção).
          - Alinhamento com objetivos ou restrições explícitos da spec.
       - Destaque a **opção recomendada** no início com justificativa clara (1 a 2 frases).
       - Use o formato: `**Recommended:** Option [X] - <reasoning>`.
       - Apresente todas as opções em tabela Markdown:

      | Opção | Descrição |
       |--------|-------------|
      | A | <Descrição da opção A> |
      | B | <Descrição da opção B> |
      | C | <Descrição da opção C> (adicione D/E se necessário, até 5) |
      | Short | Forneça outra resposta curta (<=5 palavras), somente se uma alternativa livre for adequada |

      - Após a tabela, oriente: `Responda com a letra da opção (como "A"), aceite dizendo "sim" ou "recomendado", ou forneça sua própria resposta curta.`
    - Para resposta curta, sem opções discretas úteis:
       - Forneça uma **resposta sugerida** fundamentada nas boas práticas e no contexto.
       - Formato: `**Suggested:** <your proposed answer> - <brief reasoning>`.
      - Depois, oriente: `Formato: Resposta curta (<=5 palavras). Aceite dizendo "sim" ou "sugerido", ou forneça sua própria resposta.`
    - Após a resposta:
      - Se o usuário aceitar com "sim", "recomendado" ou "sugerido" (ou os equivalentes "yes", "recommended", "suggested"), use a recomendação/sugestão anterior.
       - Caso contrário, confira se corresponde a uma opção ou respeita <=5 palavras.
       - Se ambígua, peça uma desambiguação rápida (continua sendo a mesma pergunta; não avance).
       - Quando satisfatória, registre em memória de trabalho, sem escrever ainda em disco, e avance na fila.
    - Pare quando:
       - Todas as ambiguidades críticas estiverem resolvidas e o restante se tornar desnecessário; OU
      - O usuário indicar conclusão ("concluído", "está bom", "sem mais perguntas", ou os equivalentes "done", "good", "no more"); OU
       - Atingir 5 perguntas.
    - Nunca revele antecipadamente as perguntas futuras.
    - Se não houver perguntas válidas no início, informe imediatamente a ausência de ambiguidades críticas.

6. Integre CADA resposta aceita de modo incremental:
    - Mantenha em memória a representação da spec, carregada uma vez, e o conteúdo bruto.
    - Para a primeira resposta integrada:
       - Garanta a seção `## Clarifications`; se ausente, crie-a após a seção contextual/visão geral de maior nível, conforme o template.
       - Nela, crie `### Session YYYY-MM-DD` para hoje, se ainda não existir.
    - Após a aceitação, acrescente imediatamente: `- Q: <question> → A: <final answer>`.
    - Aplique então o esclarecimento às seções adequadas:
       - Ambiguidade funcional → atualize ou adicione item nos requisitos funcionais.
       - Interação/distinção de atores → atualize histórias ou atores com role, restrição ou cenário esclarecido.
       - Dados/entidades → atualize o modelo, adicionando campos, tipos e relacionamentos, mantendo ordem e registrando restrições concisamente.
       - Restrição não funcional → adicione/altere resultados mensuráveis nos critérios de sucesso; converta adjetivos vagos em métricas/metas explícitas.
       - Caso de borda/fluxo negativo → adicione item em casos de borda/tratamento de erros, ou crie a subseção prevista pelo template.
       - Conflito de terminologia → normalize o termo na spec; mantenha o original somente se necessário, acrescentando `(formerly referred to as "X")` uma vez.
    - Se uma resposta invalidar uma afirmação anterior, substitua-a em vez de duplicar; não deixe contradições obsoletas.
    - Salve APÓS cada integração para reduzir perda de contexto (sobrescrita atômica).
    - Não reordene seções alheias; preserve a hierarquia dos títulos.
    - Mantenha cada esclarecimento mínimo e testável, sem desvio narrativo.

7. Valide após CADA escrita e ao final:
   - Exatamente um item por resposta aceita na sessão de esclarecimentos, sem duplicatas.
   - Total de perguntas aceitas ≤ 5.
   - Nenhum placeholder vago que a nova resposta deveria resolver.
   - Nenhuma contradição anterior restante; confira a remoção de alternativas invalidadas.
   - Markdown válido; novos títulos permitidos somente: `## Clarifications`, `### Session YYYY-MM-DD`.
   - Mesmo termo canônico em todas as seções atualizadas.

8. Escreva a spec atualizada em `FEATURE_SPEC`.

9. **Revalide a checklist de qualidade**, se existir:
   - Verifique `FEATURE_DIR/checklists/requirements.md`.
   - Se NÃO existir, ignore esta etapa sem anúncio.
   - Se existir:
     1. Leia o arquivo.
     2. Identifique linhas de checkbox GitHub com `- [ ]`, `- [x]` ou `- [X]` fora de blocos de código (sem diferenciar caixa, tolerando espaços iniciais em itens aninhados). Ignore títulos, notas, itens sem checkbox e metadados.
     3. Registre o estado e texto de cada item em uma lista de referência anterior.
     4. Reavalie contra a spec **atualizada**, salva na etapa 7.
     5. Atualize somente quando o estado mudar:
        - Passou e estava desmarcado: altere `[ ]` para `[x]`.
        - Falhou e estava marcado: altere `[x]`/`[X]` para `[ ]`.
        - Estado inalterado: mantenha o marcador e sua caixa, evitando diferenças cosméticas.
     6. Salve a checklist. **Altere somente a parte `[ ]`/`[x]` dos itens com mudança de estado.** Preserve todo o restante: títulos, metadados, notas, ordem e espaços.
     7. Compare referência anterior e estado atual para o relatório:
        - **Novas aprovações**: de desmarcado para marcado.
        - **Regressões**: de marcado para desmarcado.
        - **Ainda desmarcados**: itens que continuam desmarcados.
     8. Registre as contagens antes/depois como marcados/total (por exemplo, "12/16 → 15/16 itens aprovados").

Regras de comportamento:

- Sem ambiguidades relevantes, ou se todas as perguntas forem de baixo impacto, responda: "Nenhuma ambiguidade crítica exige esclarecimento formal" e sugira prosseguir.
- Se faltar spec, oriente executar `/speckit-specify` primeiro; não crie uma aqui.
- Nunca exceda 5 perguntas; novas tentativas da mesma pergunta não contam como novas.
- Evite perguntas especulativas sobre stack, salvo se a ausência bloquear clareza funcional.
- Respeite sinais de encerramento antecipado ("pare", "concluído", "prossiga", ou os equivalentes "stop", "done", "proceed").
- Sem perguntas por cobertura completa, apresente resumo conciso com todas as categorias Clear e sugira avançar.
- Ao atingir a cota com categorias críticas indefinidas, sinalize-as em Deferred com justificativa.

Contexto para priorização: $ARGUMENTS

## Hooks Obrigatórios Após a Execução

**Você DEVE concluir esta seção antes de informar a conclusão ao usuário.**

Verifique se `.specify/extensions.yml` existe na raiz do projeto.
- Se não existir ou não houver hooks registrados na chave `hooks.after_clarify`, prossiga para o Relatório de Conclusão.
- Se existir, leia-o e procure entradas na chave `hooks.after_clarify`.
- Se o YAML não puder ser interpretado ou for inválido, não ignore silenciosamente: informe que `.specify/extensions.yml` não pôde ser lido (inclua o erro do parser) e que nenhum hook foi verificado, inclusive hooks obrigatórios (`optional: false`); depois, prossiga para o Relatório de Conclusão.
- Exclua os hooks cujo `enabled` seja explicitamente `false`. Considere habilitados por padrão aqueles sem o campo `enabled`.
- Para cada hook restante, **não** tente interpretar ou avaliar expressões `condition`:
  - Se não houver `condition`, ou se ela for nula/vazia, considere o hook executável
  - Se houver `condition` não vazia, ignore o hook e deixe a avaliação da condição para a implementação de HookExecutor
- Ao construir invocações a partir dos nomes dos comandos de hooks, substitua pontos (`.`) por hífens (`-`). Por exemplo, `speckit.git.commit` → `/speckit-git-commit`.
- Para cada hook executável, apresente o seguinte conforme seu campo `optional`:
  - **Hook obrigatório** (`optional: false`) — **Você DEVE apresentar `EXECUTE_COMMAND:` para cada hook obrigatório**:
    ```
    ## Hooks de Extensões

    **Hook Automático**: {extension}
    Executando: `/{command}`
    EXECUTE_COMMAND: {command}
    ```
    Após apresentar o bloco, você DEVE invocar o hook e aguardar sua conclusão. Execute-o como executaria o comando nesta sessão (a invocação pode diferir do identificador literal `{command}`; por exemplo, um agente em modo skills usa `/skill:speckit-...` ou `$speckit-...`). Apresentar apenas o bloco não executa o hook.
  - **Hook opcional** (`optional: true`):
    ```
    ## Hooks de Extensões

    **Hook Opcional**: {extension}
    Comando: `/{command}`
    Descrição: {description}

    Solicitação: {prompt}
    Para executar: `/{command}`
    ```

## Relatório de Conclusão

Informe a conclusão após encerrar as perguntas, inclusive antecipadamente:
- Quantidade de perguntas feitas e respondidas.
- Caminho da spec atualizada.
- Nomes das seções alteradas.
- Estado da checklist, se `FEATURE_DIR/checklists/requirements.md` foi revalidado: contagens antes/depois (como "Checklist de qualidade: 12/16 → 15/16 itens aprovados") e itens com mudança, incluindo novas aprovações e regressões. Liste os ainda desmarcados como pontos de atenção.
- Tabela de cobertura por categoria: Resolved (era Partial/Missing e foi resolvida), Deferred (excede a cota ou trata de método de implementação, comparação de stack ou decomposição), Clear (já suficiente), Outstanding (ainda Partial/Missing, mas de baixo impacto).
- Se restar Outstanding ou Deferred, recomende prosseguir para `/speckit-plan` ou repetir `/speckit-clarify` após o plano.
- Próximo comando sugerido.

## Critérios de Conclusão

- [ ] Ambiguidades identificadas e esclarecimentos integrados à spec.
- [ ] Checklist de qualidade revalidada contra a spec atualizada, se `FEATURE_DIR/checklists/requirements.md` existir.
- [ ] Hooks de extensões acionados ou ignorados conforme as regras de Hooks Obrigatórios Após a Execução acima.
- [ ] Conclusão informada com perguntas respondidas, seções alteradas, estado da checklist e cobertura.
