# Guia do GitHub Copilot no Dokpod

Spec Kit é o único processo autorizado para desenvolver e manter a aplicação.
As customizações em `.github` apoiam seus artefatos e tarefas, sem criar fluxos
paralelos. Documentação e saídas ficam em pt-BR; identificadores das ferramentas
permanecem literais.

## Comece aqui

1. Leia [README](../README.md), [arquitetura](../docs/arquitetura.md),
[segurança](../docs/seguranca.md) e [constituição](../.specify/memory/constitution.md).
2. Para retomar iniciativa antiga, consulte [migração e cobertura](../specs/README.md).
Os seis conjuntos estão em `draft`; sua existência não autoriza execução.
3. Para nova mudança, execute o fluxo de feature abaixo. Para defeito,
use Bug Fixing oficial. Issue é entrada, não spec nem autorização.
4. Confira artefatos, análise, tarefa/escopo e autorização humana antes de delegar
ou implementar. Não faça commit, push, publicação ou deploy sem pedido explícito.

## Modelo mental

| Recurso | Função | Uso |
| --- | --- | --- |
| instrução | regra permanente ou específica por caminho | automática |
| agente | papel especializado e limite de autoridade | seletor de agentes |
| prompt | tarefa curta e focada | comando `/` |
| skill Spec Kit | processo oficial e artefatos | comando `/` ou descoberta automática |
| skill técnica | revisão ou gate de uma tarefa existente | comando `/` ou descoberta automática |
| documento/template | fonte humana e formato canônico | referência durante o trabalho |

Princípios duradouros ficam na constituição; contexto em README/arquitetura/segurança;
decisões duradouras em ADRs aceitos; requisitos, desenho e execução em
`specs/<feature>/{spec,plan,tasks}.md`. `.github` detalha procedimentos. Conflitos
exigem decisão humana, não precedência inventada pelo agente.

## Fluxo de feature

| Etapa | Comando | Saída e checkpoint |
| --- | --- | --- |
| Requisitos | `/speckit-specify` | `spec.md`; humano confirma escopo e critérios |
| Ambiguidades | `/speckit-clarify` | respostas registradas na spec, quando necessário |
| Desenho | `/speckit-plan` | `plan.md` e artefatos pertinentes; humano revisa decisões |
| Trabalho | `/speckit-tasks` | `tasks.md` com dependências e evidências esperadas |
| Consistência | `/speckit-analyze` | análise read-only; resolver conflitos antes de executar |
| Execução | `/speckit-implement` | somente tarefas explicitamente autorizadas e validadas |
| Diferença restante | `/speckit-converge` | tarefas residuais; nova revisão/autorização |

`/speckit-checklist` auxilia revisão de requisitos; não substitui testes ou aprovação.
`/speckit-taskstoissues` pode publicar tarefas no GitHub somente quando solicitado;
Issues mantêm referência ao artefato e não se tornam requisitos paralelos.
`/speckit-constitution` cria a constituição inicial ou aplica emenda aprovada,
não implementa a aplicação.

### Seleção explícita de feature

Nesta versão, o diretório vem de `SPECIFY_FEATURE_DIRECTORY` ou de
`.specify/feature.json`, não do nome da branch Git. Em `chore/adotar-spec-kit` ou
com várias features, não escolha automaticamente o último diretório. Informe o
caminho explicitamente no processo que executa os scripts oficiais:

```powershell
$env:SPECIFY_FEATURE_DIRECTORY = 'specs/001-identidade-ambientes-auditoria'
$env:SPECIFY_FEATURE = '001-identidade-ambientes-auditoria'
```

Para scripts em container, passe também
`--env SPECIFY_FEATURE_DIRECTORY=specs/001-identidade-ambientes-auditoria` no
`docker run`. `SPECIFY_FEATURE` identifica o nome, mas não resolve o diretório
sozinho. Validações read-only usam também `--env SPECIFY_FEATURE_NO_PERSIST=1`
para não escrever `.specify/feature.json`. Seleção não aprova draft nem tarefa. Confirme
o `FEATURE_DIR` retornado por `check-prerequisites.ps1 -Json -RequireTasks`.
Em multi-root, comandos e Git sempre partem da raiz do Dokpod.

### Checkpoints humanos

- Revisar requisitos, matriz Docker/Podman/Linux/Windows e limites antes do desenho.
- Revisar plan, decisões/ADRs propostos e tarefas após a análise de consistência.
- Autorizar explicitamente tarefas ou remediação; status `Ready` não basta.
- Marcar `[x]` somente após execução com evidência. Checkbox não aceita ADR.
- Decidir GO/NO-GO separadamente; `NOT RUN` não é PASS e migração não muda NO-GO.

## Defeitos com Bug Fixing

| Comando | Resultado |
| --- | --- |
| `/speckit-bug-assess` | `.specify/bugs/<slug>/assessment.md`; leitura e remediação proposta |
| `/speckit-bug-fix` | remediação aprovada e `fix.md` |
| `/speckit-bug-test` | evidências em `test.md` |

Informe o slug/diretório explicitamente. Revise o assessment e autorize a correção
antes de fix; não transforme relato incerto em alteração automática. Se a análise
revelar feature ou alteração arquitetural fora do defeito, use o fluxo de
feature e obtenha autorização para o novo escopo.

## Como pedir trabalho

Inclua contexto, resultado observável, escopo, limites, plataformas e validação esperada.

```text
Contexto: operador precisa reiniciar um container em um ambiente remoto.
Resultado: comando idempotente com estado visível e auditoria.
Escopo: contrato, API, agente Docker Linux e interface.
Limites: não implementar terminal nem remover volumes.
Plataformas: Docker Engine em Linux; Podman retorna unsupported.
Validação: repetição do mesmo command ID produz um único efeito e a UI converge após resposta perdida.
```

Nunca inclua tokens, certificados privados, variáveis de ambiente ou logs integrais de containers no pedido.

## Agentes

| Situação | Agente |
| --- | --- |
| feature entre várias camadas | `Dokpod Delivery Lead` |
| arquitetura, ADR ou plano | `Dokpod Solution Architect` |
| API, BFF, domínio, aplicação ou PostgreSQL | `Dokpod .NET Engineer` |
| Docker/Podman, gRPC, mTLS, journal ou reconciliação | `Dokpod Engine & Protocol Engineer` |
| Angular, estado, acessibilidade ou UX operacional | `Dokpod Angular Engineer` |
| estratégia e implementação de testes | `Dokpod Quality Engineer` |
| threat model ou revisão de segurança | `Dokpod Security Reviewer` |
| revisão de diff ou pull request | `Dokpod Code Reviewer` |

Os reviewers são read-only. O Solution Architect apoia plan/tarefa e edita apenas
ADRs propostos; não cria planos legados. O Delivery Lead coordena tarefas Spec Kit
autorizadas ou remediação Bug Fixing e pode delegar recortes com referência aos
artefatos. Especialistas não iniciam workflows independentes nem presumem aprovação.

## Prompts

| Comando | Quando usar |
| --- | --- |
| `/create-adr` | registrar decisão duradoura proposta vinculada a plan/tarefa |
| `/generate-tests` | produzir evidência para tarefa/remediação autorizada |
| `/review-change` | revisar diff, branch ou pull request sem editar |

Exemplo:

```text
/speckit-bug-assess stop-replay

Após timeout de STOP, a reconexão executa o mesmo comando novamente.
Ambiente: Docker Linux.
Esperado: o journal reconhece o command ID e reconcilia o estado sem repetir o efeito.
Validação: teste com resposta perdida e reconexão.
```

## Skills

| Comando | Quando usar |
| --- | --- |
| `/pull-request-review` | revisão multidimensional de PR |
| `/release-readiness` | decisão GO/NO-GO antes de publicar ou promover |

Capabilities são especificadas, planejadas e executadas pelo Spec Kit. O plan/tarefa
exige matriz de engine/plataforma, journal antes de ACK, fencing, dedup,
expiração, reconciliação e N/N-1; `unsupported` é resultado válido,
nunca falsa equivalência Docker/Podman. O especialista implementa só o recorte autorizado.

`/release-readiness` apenas avalia evidências. `NOT RUN` não é sucesso, e a skill não publica nem faz deploy.

## Exemplos por fluxo

### Capability de engine

```text
/speckit-specify

Adicionar PAUSE e UNPAUSE para Docker Linux.
Podman e Windows permanecem fora do escopo nesta entrega.
O comando deve ser idempotente, expirar e reconciliar após resposta perdida.
```

### Revisão de segurança

```text
Use o Dokpod Security Reviewer para revisar o bootstrap de agente.
Trace token de uso único, chave pública, emissão do certificado, revogação e primeira conexão.
Não edite arquivos.
```

### Prontidão de release

```text
/release-readiness

Versão candidata: 0.2.0.
Base: 0.1.0.
Alvos: agente Linux em Docker e Worker Service Windows self-contained.
Não publique; produza matriz de gates e recomendação GO/NO-GO.
```

## Frontend Angular e PO UI

PO UI (`@po-ui/ng-components`) é a única biblioteca de componentes aprovada para o frontend. Antes de desenhar uma tela, consulte `https://po-ui.io/llms.txt` (índice), `https://po-ui.io/llms-full.txt` (documentação completa) ou, preferencialmente, o servidor MCP `po-ui` configurado em `.vscode/mcp.json` (ferramentas `list_components`, `search_docs`, `get_component_docs`, `get_guide`, `get_component_examples`). Regras completas em `.github/instructions/frontend-po-ui.instructions.md`.

Se nenhum componente do portfólio atender a necessidade, não invente substituto: registre a lacuna e pergunte antes de prosseguir.

Toda tela ou componente novo passa por um loop de screenshot, análise e ajuste (`frontend-visual-verification.instructions.md`) antes de ser considerado pronto, com no máximo 3 iterações antes de perguntar.

## Validações esperadas

- .NET: build e testes containerizados do projeto ou solução afetada;
- adapters: integração com engine real, nunca apenas mock;
- frontend: comandos `ng` a partir de `frontend/web`;
- contratos: geração e testes de consumidor/provedor, incluindo N/N-1;
- containers: build, inspeção e smoke test;
- Windows: instalação e execução em host sem runtime .NET;
- desempenho: perfis e evidências de `PERFORMANCE_TESTING_CRITERIA.md`;
- segurança: autorização horizontal, agente falso/revogado, replay e vazamento de secrets.

Execute primeiro a validação mais estreita que pode refutar a mudança. Amplie apenas após o recorte passar.

## Descoberta no VS Code

Selecione um agente no topo do Chat ou digite `/` para listar prompts e skills. Se um arquivo novo não aparecer, execute `Developer: Reload Window` e abra um novo chat na raiz do Dokpod.

Se uma regra não for aplicada, confira o `applyTo`, a descrição de descoberta e a raiz do workspace antes de duplicar instruções.

## Instalação e manutenção

Instalação oficial: Spec Kit `1.0.13` (MIT), integração Copilot em skills mode,
scripts PowerShell e extensão Bug Fixing `1.0.0`. Configuração em
`.specify/init-options.json` e `.specify/integration.json`; versões da extensão
em seus manifests. Templates pt-BR ficam em `.specify/templates/overrides/` e
devem ser resolvidos pelos scripts oficiais. Skills/comandos bug têm orientação
pt-BR aditiva.

O comando abaixo documenta a instalação realizada, não deve ser reexecutado
automaticamente: `--force` pode sobrescrever customizações. Atualização exige
revisão da versão, licença, diff dos assets e testes dos resolvers/pré-requisitos.

```powershell
docker run --rm --entrypoint sh --volume "${PWD}:/workspace" --workdir /workspace lzocateli/devops@sha256:2a3e7f65b59071e415182a9829e3d22a4d24ef2f6fc0afe55bcc96ac73ee0ef3 -c 'uvx --from git+https://github.com/github/spec-kit.git@v1.0.13 specify init --here --integration copilot --integration-options="--skills" --script ps --force --non-interactive --extension bug'
```

Execute a partir da raiz do Dokpod, somente com autorização para reinstalar.
Builds, testes e ferramentas permanecem containerizados. Scripts oficiais ficam
em `.specify/` com contrato upstream, não em `tools/scripts/`; essa exceção não
permite automações próprias nesses diretórios. Não altere hashes de manifests
manualmente para encobrir customizações locais.

## Migração dos planos

Os cinco legados preservam integralmente conteúdo/status históricos e ficam
congelados. [O índice](../specs/README.md) mapeia 36 etapas em seis conjuntos e
45 tarefas não concluídas. Divergências de evidência/aprovação permanecem explícitas.
Não herde aprovação de legado nem reimplemente entregas existentes:
revisão humana, avaliação de diferença e teste do residual vêm antes de execução.
O template antigo `PLAN_TEMPLATE.md` é somente histórico. ADRs existentes,
gates HIGH e decisão de release permanecem inalterados.
