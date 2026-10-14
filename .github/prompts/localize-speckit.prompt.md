---
name: "localize-speckit"
description: "[dokpod] Auditar ou traduzir skills, comandos e textos de extensões do Spec Kit para pt-BR, preservando contratos operacionais após instalação ou atualização."
argument-hint: "Extensão ou arquivos alvo; modo=auditar ou modo=traduzir; exclusões adicionais"
agent: "agent"
---

# Localização Segura do Spec Kit

Use este procedimento após instalar ou atualizar uma extensão, preset ou integração do Spec Kit.
Siga a [constituição](../../.specify/memory/constitution.md) e as [regras de documentação](../instructions/documentation.instructions.md).

## Uso

No chat do Copilot:

```text
/localize-speckit extensão=assess modo=auditar
/localize-speckit extensão=assess modo=traduzir
/localize-speckit arquivos=.github/skills/speckit-*/SKILL.md modo=traduzir
```

Os parâmetros acima são instruções em linguagem natural para este prompt, não opções do CLI.
Sem modo explícito, somente audite. Sem alvo explícito, inventarie os arquivos do Spec Kit no Dokpod e apresente os candidatos; não traduza indiscriminadamente.

## Configuração Nativa e Limites

- Não há opção geral de idioma identificada nas referências oficiais consultadas para traduzir automaticamente qualquer extensão. Não invente `locale`, `language`, `--lang` ou um evento `after_install`.
- `installed` em `.specify/extensions.yml` registra extensões instaladas; editar a lista não instala seus componentes. Hooks antes/depois de comandos não equivalem a hooks de instalação.
- Overrides em `.specify/templates/overrides/` personalizam templates, mas não traduzem automaticamente skills já materializadas em `.github/skills/`.
- Presets podem fornecer versões localizadas de comandos centrais e de extensões. Em integrações compatíveis, a instalação/reconciliação materializa os comandos ou skills; o agente não resolve essa pilha a cada invocação. Isso distribui traduções previamente preparadas, não traduz extensões desconhecidas.
- Bundles podem declarar um conjunto de extensões, presets e workflows para instalação conjunta. Um bundle não acrescenta tradução automática; precisa incluir um preset localizado que cubra os comandos desejados.
- A documentação online pode descrever capacidades posteriores à versão local. Consulte `.specify/init-options.json`, `.specify/integration.json` e, se o CLI estiver disponível, `specify version` e o `--help` do subcomando antes de recomendar opções.
- Não instale extensões, presets, bundles, ferramentas ou dependências como parte da tradução. Uma solução de distribuição por preset/bundle exige escopo e aprovação próprios.

## Procedimento

1. Leia a versão atual dos arquivos, inclusive alterações locais do desenvolvedor. Restrinja o trabalho ao Dokpod e ao alvo solicitado.
2. Identifique a origem dos comandos em `.specify/extensions/<extensão>/commands/`, as skills materializadas em `.github/skills/` e os textos pertinentes de `extension.yml` ou `workflow.yml`. Não suponha que fonte e skill sejam idênticas.
3. Informe os arquivos candidatos e as exclusões. No modo de auditoria, apresente evidências sem editar. No modo de tradução, prossiga dentro do escopo explícito; peça autorização antes de ampliá-lo.
4. Registre uma referência anterior de conteúdo, tokens técnicos e hashes dos arquivos protegidos. Preserve trabalho local; não use reset, checkout, instalação forçada ou regeneração indiscriminada.
5. Traduza apenas o texto humano para pt-BR, mantendo sua força normativa, limites numéricos, sequência, condições de parada, aprovações e semântica de segurança.
6. Se fonte e skill forem equivalentes, confirme a equivalência antes de replicar a tradução. Preserve os aliases específicos de cada representação; não copie uma sobre a outra cegamente.
7. Execute as verificações abaixo. Se algum contrato mudar, corrija a tradução antes de prosseguir. Não execute o fluxo de uma skill apenas para testar seu texto: isso pode implementar código, instalar componentes ou produzir efeitos externos.
8. Informe o resultado e os limites da validação. Remova qualquer automação temporária criada para a tarefa; não faça commit nem mantenha scripts sem solicitação explícita.

## O que Traduzir

- Descrições, compatibilidade em linguagem natural, explicações, comentários documentais, perguntas e mensagens ao usuário.
- Exemplos de texto e descrições de tarefas, preservando IDs, marcadores e caminhos contidos neles.
- Textos de relatórios gerados; atualize também referências internas às seções traduzidas. Mantenha reconhecimento dos títulos antigos quando relatórios existentes forem consumidos por etapas posteriores.
- Em YAML, somente campos textuais pertinentes, como `description`, `prompt` e `message`. Um nome de exibição só pode mudar se for confirmado como texto humano, não identidade ou chave de registro.

## O que Preservar

- Nomes de arquivo, diretórios, comandos, aliases, opções do CLI, nomes de ferramentas e expressões executáveis.
- IDs de extensão, preset, workflow, integração e tarefas; chaves YAML/JSON; tipos, versões, dependências, prioridades, condições e ordem das etapas.
- Metadados técnicos de proveniência e descoberta, como `name` da skill, `author`, `source`, `schema_version` e referências a arquivos.
- Marcadores estruturais e placeholders, incluindo `EXECUTE_COMMAND:`, `[NEEDS CLARIFICATION]`, `[NEEDS CLARIFICATION: ...]`, `__SPECKIT_COMMAND_*__`, `$ARGUMENTS`, `{command}`, `{extension}`, `{prompt}`, `{description}`, `{{ inputs.spec }}` e `{CORE_TEMPLATE}`.
- Nomes de variáveis e campos de saída, como `BUG_SLUG`, `BUG_DIR`, `FEATURE_DIR`, `SPEC_FILE`, `TEMPLATE_CONTENT` e `AVAILABLE_DOCS`.
- Checkboxes e marcadores como `- [ ]`, `[x]`, `[X]`, `[P]`, `[US1]`, `T001` e `CHK001`; estados literais como `verified`, `partial`, `failed`, `applied`, `not-applied`, `converged` e `tasks_appended`.
- Regex, schemas, URLs, limites de rede, listas de hosts e regras contra instruções não confiáveis. Não amplie permissões, libere hosts bloqueados ou solicite secrets durante a tradução.
- Títulos ou labels usados como contrato de leitura por outra etapa, salvo comprovação de que são apenas apresentação e atualização consistente dos consumidores.

## Exclusões Padrão

- Não altere `.specify/extensions/bug/README.md`; existe uma tradução em `.specify/extensions/bug/README.pt-BR.md`.
- Não altere templates originais que já possuam overrides pt-BR nem os próprios overrides já traduzidos, salvo solicitação explícita.
- Não altere código da aplicação, testes, specs de features ou a constituição para acomodar a tradução.
- Não reescreva hashes, registries ou manifests de instalação para ocultar modificações locais. Não remova avisos de arquivos modificados; registre-os para revisão.

## Validação Obrigatória

- Compare comandos, IDs, aliases, placeholders, marcadores e literais técnicos antes/depois, incluindo suas ocorrências. Para prosa traduzida dentro de exemplos, explicite as substituições permitidas; não ignore todo o bloco de código.
- Confira comandos executáveis e blocos JSON/YAML de exemplo; não confunda prosa com contratos de máquina.
- Valide frontmatter e YAML com parser existente, quando disponível, sem instalar dependências. Compare a estrutura, permitindo somente campos textuais autorizados. Sem parser, use diagnósticos do editor e comparação estrutural e declare essa limitação.
- Preserve indentação YAML, aspas, delimitadores de frontmatter, blocos Markdown, links e encoding UTF-8. Confira nomes de skills e campos de descoberta.
- Revise a equivalência semântica: somente leitura, append-only, bloqueios, consentimento, tratamento de erros, limites de perguntas e comportamento dos hooks continuam iguais.
- Verifique por hashes que os arquivos excluídos permaneceram intactos. Não declare sucesso quando uma checagem falhar ou não puder executar.

## Resultado Esperado

Informe quantidade e escopo dos arquivos traduzidos, exclusões preservadas, verificações realizadas e riscos residuais. A frase "Comandos, IDs, placeholders, metadados técnicos e estrutura operacional foram preservados" só pode ser usada quando sustentada pelas verificações.

A constituição orienta o Copilot, mas não configura o CLI como tradutor. Este prompt é acionado manualmente após instalações/atualizações; não é um hook automático. Atualizações ou reconciliações podem preservar arquivos modificados, avisar sobre eles ou substituí-los conforme a versão e a operação: revise o diff e repita a auditoria, sem presumir um comportamento único.

## Referências Oficiais

- [Personalização](https://github.github.io/spec-kit/guides/customization.html)
- [Extensões e hooks](https://github.github.io/spec-kit/reference/extensions.html)
- [Presets e resolução de arquivos](https://github.github.io/spec-kit/reference/presets.html)
- [Bundles](https://github.github.io/spec-kit/reference/bundles.html)
- [Comandos centrais](https://github.github.io/spec-kit/reference/core.html)
