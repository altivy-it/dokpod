# Contrato de Uso dos Fluxos Auxiliares

**Feature**: [spec.md](../spec.md) | **Data**: 2026-10-27
**Origem**: IA assistida | **Estado**: interface proposta, não implementada.
**Revisor humano**: Solicitante; identificação nominal e revisão pendentes.

Este contrato descreve interfaces de chat para mantenedores. Não adiciona endpoint, comando de sistema, API pública ou contrato ao produto. Exemplos de invocação são especificações de aceitação para a implementação futura.

## C1: descoberta e representação

| Origem | Interface canônica proposta | Papel selecionado |
| --- | --- | --- |
| `create-adr.prompt.md` | `/dokpod-create-adr` | Dokpod Solution Architect |
| `generate-tests.prompt.md` | `/dokpod-generate-tests` | Dokpod Quality Engineer |
| `localize-speckit.prompt.md` | `/dokpod-localize-speckit` | Agente padrão do harness |
| `review-change.prompt.md` | `/pull-request-review` | Dokpod Code Reviewer |

As três skills novas terão `name` igual ao diretório, no máximo 64 caracteres minúsculos alfanuméricos/hífens, `description` específica em pt-BR com no máximo 1024 caracteres, `argument-hint`, `user-invocable: true` e `disable-model-invocation: true`. Não terão `agent`, `model` ou `tools` copiados do prompt. A revisão mantém sua configuração atual de ativação, sem alterar padrões implícitos.

Selecionar um agente não é efeito da invocação da skill. O guia deve instruir a seleção antes do comando. Se não for possível comprovar as ferramentas necessárias e as restrições do papel no destino, informar bloqueio e não prosseguir com execução que dependa delas. Nenhum campo da skill concede autorização Spec Kit ou pré-aprova shell.

## C2: ADR

**Entrada**: referência ao planejamento Spec Kit, decisão duradoura, autorização explícita e identificação do revisor humano. Informações podem ser expressas em linguagem natural após `/dokpod-create-adr`.

**Precondições**: decisão justificada pelo planejamento e agente Architect disponível com capacidades compatíveis. Não interpretar tarefa, Issue, branch ou invocação como aceite de ADR.

**Procedimento preservado**:

1. Conferir planejamento, arquitetura, segurança e ADRs existentes; vincular o artefato de origem.
2. Confirmar que a decisão é arquitetural e duradoura; caso contrário, recomendar o artefato correto.
3. Comparar no máximo três alternativas por segurança, operação, compatibilidade, custo e reversibilidade.
4. Recomendar a opção mais simples que atende aos requisitos e uma forma de falsificá-la.
5. Se justificado e autorizado, seguir o modelo canônico, o caminho anual e a sequência de ADRs vigentes.

**Saída**: proposta `proposed`, origem IA assistida, referência de origem e revisão humana pendente. Se faltarem autorização ou justificativa, informar a ausência e não criar arquivo. Não implementar código nem marcar `accepted`.

## C3: testes

**Entrada**: artefato Spec Kit e ID de tarefa autorizada via `/speckit-implement`, ou remediação autorizada via `/speckit-bug-fix`, além do comportamento e recorte solicitados.

**Procedimento preservado**:

1. Derivar critérios e riscos observáveis do escopo autorizado, sem criar requisito novo a partir do diff.
2. Montar matriz curta entre risco e nível de teste; escolher primeiro o teste de maior valor e demonstrar que discrimina o comportamento.
3. Cobrir erros e bordas sem duplicar testes equivalentes; não corrigir defeitos por fora de Bug Fixing.
4. Usar Docker/Podman e PostgreSQL reais quando o comportamento depender deles; preservar execução em containers, exceção Windows e acesso HTTP pela borda nginx.
5. Incluir N/N-1, duplicação, deadline, fencing e reconexão quando aplicáveis ao protocolo.
6. Executar testes novos e suíte relacionada no escopo permitido; devolver evidências ao artefato de origem e a `/speckit-bug-test` quando aplicável.

**Saída**: cobertura, matriz risco-teste, comandos e resultados, plataformas verificadas e lacunas. Sem referência/autorização, não editar ou executar testes. Infraestrutura indisponível gera `BLOCKED` ou `NOT RUN`, sem equivalência presumida entre engines ou plataformas.

## C4: localização segura

**Entrada**: `extensão`, `arquivos`, `modo=auditar` ou `modo=traduzir` e exclusões adicionais como linguagem natural, não opções de CLI. Sem modo, somente auditoria. Sem alvo, inventariar candidatos e aguardar delimitação, mesmo que o pedido mencione tradução.

**Limites preservados**:

- Não inventar configuração de idioma (`locale`, `language`, `--lang`) ou evento `after_install`; configuração de registro não instala componentes e hooks de comandos não são hooks de instalação.
- Distinguir overrides de templates, presets, bundles, comandos fonte e skills materializadas; a versão online não garante suporte pela versão local.
- Consultar registros e ajuda da versão instalada quando necessário, sem instalar ou atualizar CLI/extensão/preset/bundle como parte da tradução.
- Ler o conteúdo atual e preservar alterações locais. Apresentar candidatos/exclusões e solicitar autorização antes de ampliar o alvo.
- Registrar referência anterior, contratos técnicos e hashes dos arquivos protegidos; não usar reset, checkout, instalação forçada ou regeneração indiscriminada.
- Traduzir somente texto humano, mantendo força normativa, números, sequência, condições de parada, aprovações e semântica de segurança.
- Comprovar equivalência entre fonte e materialização antes de replicar tradução; preservar os aliases próprios de cada representação.
- Não executar workflows de skills para validar o texto, instalar componentes ou produzir efeitos externos.
- Não reescrever hashes, registries ou manifests nem ocultar avisos de arquivos modificados; registrar a divergência para revisão.

**Paridade de contratos técnicos**: preservar nomes, diretórios, comandos, aliases, opções, ferramentas, expressões executáveis, IDs, chaves, versões, dependências, prioridades e ordem de etapas. Preservar proveniência/descoberta (`name`, `author`, `source`, `schema_version`), variáveis, campos de saída, checkboxes, estados literais, regex, schemas, URLs, limites de rede e regras contra instruções não confiáveis. Isso inclui `EXECUTE_COMMAND:`, `$ARGUMENTS`, `{command}`, `{extension}`, `{prompt}`, `{description}`, `{CORE_TEMPLATE}`, `BUG_SLUG`, `BUG_DIR`, `FEATURE_DIR`, `SPEC_FILE`, `TEMPLATE_CONTENT` e `AVAILABLE_DOCS`, além dos demais marcadores técnicos da origem. Títulos consumidos como contrato só mudam com comprovação e atualização consistente dos consumidores.

**Exclusões padrão**: README original da extensão Bug Fixing, originais que já possuem overrides pt-BR, overrides já traduzidos salvo solicitação explícita, produto, testes da aplicação, specs de features e constituição. A tradução complementar existente não autoriza substituir o README original.

**Verificações**: comparar tokens técnicos e suas ocorrências, explicitando substituições autorizadas de prosa nos exemplos; não ignorar blocos inteiros. Conferir comandos e exemplos JSON/YAML, frontmatter/estrutura por parser existente quando disponível, indentação, aspas, delimitadores, UTF-8, nomes e links. Comparar semântica de leitura, append-only, bloqueios, consentimento, tratamento de erro, limites de perguntas e hooks. Comprovar por hashes a integridade das exclusões; falha ou ausência de ferramenta impede alegar verificação completa.

**Saída**: quantidade e escopo, exclusões, verificações realizadas, divergências e riscos. A declaração de preservação dos contratos só é permitida com evidência. Auditoria não é tradução automática e a skill não é hook de instalação.

## C5: revisão canônica

**Entrada**: diff, branch ou PR, base/head e tarefa/remediação Spec Kit associada. Se faltar rastreabilidade, reportá-la, sem criar requisito ou iniciar implementação.

**Procedimento preservado**: ler requisito, diff completo, call sites, contratos e testes; comparar comportamento anterior e proposto; priorizar bypass de autorização, socket/secret, destruição, replay, N/N-1, diferenças de engine e reconciliação. Manter as dimensões adicionais da skill canônica, incluindo dados/operação e frontend. Verificar evidências existentes sem executar checks. Não relatar gosto estilístico coberto por ferramentas.

**Ferramentas**: somente capacidades de leitura e busca, comprovadas na sessão; não usar terminal, execução, escrita, migrações ou operações mutantes, inclusive por integração externa. Diff/contexto indisponível deve ser fornecido pelo mantenedor ou por ferramenta de leitura permitida; não adicionar shell para obtê-lo.

**Saída**: achados por severidade com localização, cenário, impacto, correção e teste; depois dúvidas, lacunas, risco residual e resumo. Se não houver achados, declarar explicitamente. Indicar checks ao responsável autorizado, sem executá-los.

## C6: evidência e retirada das fontes

O relatório de implementação deverá relacionar, por fluxo: origem e referência anterior, regra de paridade, destino, FR/SC, cenário, ambiente/versão/Session Target, agente/ferramentas observadas, resultado esperado/obtido, evidência sanitizada e limitações. Os resultados seguem o [modelo de evidências](../data-model.md).

Para cada fluxo, a matriz de paridade enumera todas as regras da origem; as listas acima orientam a revisão, mas não dispensam comparar integralmente a versão anterior. No caso de localização, preservar inclusive cada placeholder e estado literal não enumerado neste resumo.

A retirada conjunta dos quatro prompts exige os quatro fluxos qualificados, ausência de diferenças normativas não resolvidas, links válidos e integridade dos arquivos protegidos. `FAIL`, `BLOCKED` ou `NOT RUN` num gate necessário impede declarar conclusão e excluir as fontes. Após retirar, procurar referências operacionais aos comandos/arquivos antigos e revisar o diff; referências históricas permanecem identificadas como histórico.