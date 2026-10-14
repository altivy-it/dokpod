# Dokpod - Instruções globais

Toda documentação produzida ou atualizada, inclusive artefatos do Spec Kit e
relatórios de Bug Fixing, deve estar em português brasileiro (pt-BR). Preserve
comandos, identificadores, caminhos e marcadores literais exigidos pelas ferramentas.

## Contexto obrigatório

- O Dokpod é um plano de controle self-hosted para containers Docker e Podman.
- Backend, BFF, API e agente usam C#/.NET 10; o frontend usa Angular 22.
- Backend, BFF e frontend sempre executam em containers.
- O agente Linux sempre executa em container; o agente Windows é um Worker Service self-contained instalado como Windows Service.
- Runners de testes e ferramentas de build executam em containers; comandos Docker no host apenas os orquestram. Preserve a exceção do agente Windows: instalação e smoke test do Windows Service ocorrem em host Windows sem runtime .NET.
- Sempre que um teste, preview ou validação exigir a aplicação em execução, suba seus serviços em containers e acesse HTTP do navegador e do runner exclusivamente pela borda de um proxy reverso nginx também em container. Isso inclui Playwright com respostas simuladas e E2E com serviços reais: não use `ng serve`, BFF ou API diretamente como URL do navegador/teste. Testes unitários sem aplicação HTTP em execução continuam em containers e não exigem nginx ocioso. Para E2E real, siga `deploy/e2e/README.md` e use `https://localhost:7443/dokpod/`; para testes simulados, configure primeiro uma borda nginx containerizada.
- Keycloak é a autoridade externa obrigatória de identidade e autorização dos usuários.
- Leia o [README](../README.md), a [arquitetura](../docs/arquitetura.md) e a [segurança](../docs/seguranca.md) antes de alterar contratos, agentes, engines, identidade ou deployment.
- O engine local é a fonte de verdade dos containers; o inventário PostgreSQL é uma projeção reconstruível.
- O projeto é inspirado funcionalmente no Portainer, mas não copia seu código, texto, marca ou interface.

## Estrutura do monorepo

```text
backend/apps/Dokpod.ControlPlane.Api/ # host REST, gRPC e SignalR
backend/apps/Dokpod.Bff/              # OIDC confidencial e sessão do browser
backend/apps/Dokpod.Agent/            # agente Linux container/Windows Service
backend/libs/              # domínio e aplicações/infraestruturas separadas
backend/tests/             # testes .NET
frontend/web/              # aplicação Angular
frontend/libs/             # bibliotecas compartilhadas
frontend/tests/            # Playwright
contracts/openapi/         # contrato público HTTP
contracts/agent/           # protocolo agente-servidor
deploy/                    # imagens, pacote Windows, Keycloak e operação
docs/                      # arquitetura, ADRs, planos e runbooks
specs/                     # requisitos, desenho e tarefas Spec Kit
.specify/                  # constituição, templates e ferramentas oficiais
tools/scripts/             # automação global de infraestrutura e manutenção
```

Não crie dependências circulares. Hosts compõem; bibliotecas implementam regras reutilizáveis; contratos não dependem de aplicações.

## Fluxo de trabalho

1. Comece pelo comportamento, teste relacionado e módulo proprietário.
2. Declare uma hipótese local e uma validação capaz de refutá-la.
3. Faça a menor alteração coerente e valide o recorte imediatamente.
4. Valide primeiro o recorte alterado; depois execute verificações amplas proporcionais ao risco.
5. Atualize contrato, compatibilidade, migração, documentação, segurança e observabilidade quando o comportamento exigir.
6. Preserve alterações existentes e não reformate arquivos fora do escopo.

Esse fluxo técnico só pode ser executado dentro de uma tarefa Spec Kit autorizada
ou da remediação autorizada de Bug Fixing; não cria processo independente.

## Spec Kit, ADRs e planos

- ADRs ficam em `docs/adr/AAAA-NNNN-titulo.md`, começam como `proposed` e seguem `.github/ADR_TEMPLATE.md`.
- Spec Kit é o único processo autorizado para desenvolver e manter a aplicação: features, correções, manutenção e iniciativas de produto exigem seus artefatos e tarefas.
- Features usam `specs/<feature>/spec.md`, `plan.md` e `tasks.md`. Use `/speckit-specify`, `/speckit-clarify` quando necessário, `/speckit-plan`, `/speckit-tasks`, `/speckit-analyze`, autorização humana explícita, `/speckit-implement` e `/speckit-converge`.
- Defeitos usam exclusivamente a extensão oficial Bug Fixing: `/speckit-bug-assess`, `/speckit-bug-fix` e `/speckit-bug-test`. Não crie fluxo independente de diagnóstico/correção.
- `.specify/memory/constitution.md` registra princípios duradouros; `.github/` contém procedimentos operacionais; specs contêm requisitos de cada mudança; ADRs registram decisões duradouras sem duplicar esses artefatos.
- README, arquitetura, segurança e ADRs aceitos restringem features. Divergências exigem decisão humana, não arbitragem automática.
- `docs/plan` está congelado para novos planos, atualizações e execução. Os cinco planos foram convertidos em seis conjuntos Spec Kit em rascunho, aguardando revisão humana em `specs/README.md`. Não retome essas iniciativas pela aprovação histórica; arquive/substitua originais somente após revisão.
- `.github/PLAN_TEMPLATE.md` está obsoleto. Não crie requisitos, planos ou fluxos concorrentes em prompts, agentes, skills ou Issues; estes só apoiam tarefas Spec Kit existentes.
- Use `/speckit-constitution` para adoção inicial e emendas aprovadas, registrando apenas princípios existentes ou explicitamente acordados.
- Antes de implementar, confirme feature/diretório, tarefas ou remediação, escopo revisado e autorização explícita. Issue `Ready`, branch, checkbox ou artefato existente não autoriza execução. Selecione `SPECIFY_FEATURE_DIRECTORY` explicitamente quando houver múltiplas features; `SPECIFY_FEATURE` sozinho e o nome da branch não resolvem o diretório nesta versão. Para validação read-only, use `SPECIFY_FEATURE_NO_PERSIST=1`.
- Checkboxes em `tasks.md` registram execução validada, não aprovação humana. Tarefas novas de `/speckit-converge` exigem revisão e autorização antes de executar.
- Registre `Origem` como humano ou IA assistida e identifique o revisor humano.
- IA não marca ADR como `accepted`, artefato como aprovado nem altera status histórico sem decisão ou evidência humana explícita e referenciada.

### Extensões e localização

- Use as mesmas configurações Spec Kit do Inventory360Api: `auto_execute_hooks: true`, todos os 18 hooks Git com `enabled: true` e `git-config.yml` igual ao da referência. Os commits por evento continuam desativados nessa configuração.
- A invocação de um fluxo Spec Kit autoriza os hooks nele configurados; não autoriza push, publicação, deploy ou implementação de produto fora das tarefas aprovadas.
- Após instalar, atualizar ou reconciliar componentes, use [localize-speckit](prompts/localize-speckit.prompt.md) para auditar ou traduzir textos humanos em pt-BR sem alterar comandos, IDs, placeholders ou contratos de leitura. Sem modo explícito, somente audite; não invente configuração de idioma ou hooks de instalação.
- Consulte [o guia de extensões](README.md#spec-kit-e-extensões) para versões e validação. Preserve constituição, specs, relatórios, overrides e personalizações locais.

## Commits

- Todo commit criado ou sugerido segue [Conventional Commits](instructions/conventional-commits.instructions.md) e usa os tipos e escopos aprovados pelo projeto.
- Escreva a descrição em português brasileiro, no imperativo, iniciando com minúscula e sem ponto final.
- Mudanças incompatíveis usam `!` no cabeçalho e o rodapé `BREAKING CHANGE:` com impacto e migração.
- Fora dos hooks autorizados pela invocação do fluxo Spec Kit, não faça commit sem solicitação explícita. Push, publicação e deploy sempre exigem solicitação explícita.

## Scripts e automação

- Para qualquer comando Angular do frontend, use o alias `ng` definido no profile do PowerShell, executando-o a partir de `frontend/web`. O alias inicia o container padronizado do projeto; não invoque `node`, `npm`, `npx` ou uma instalação local do Angular CLI diretamente no host.
- O profile também define o alias `npx`, que executa a ferramenta na mesma imagem containerizada de `node` e `npm`; use-o no PowerShell interativo quando for necessário executar um pacote diretamente. Em scripts, tarefas e CI que não carregam o profile, use explicitamente `npm exec -- <comando>` dentro do container apropriado; não invoque `npx` instalado no host.
- Antes de enviar `ng`, confirme que o diretório atual é `frontend/web`; se o terminal estiver na raiz do monorepo, envie primeiro `Set-Location $env:USERPROFILE\projetos\dokpod\frontend\web`.
- Envie comandos Angular como texto literal, sem caracteres de controle como `^U` ou `Ctrl+U` antes de `ng`. Se `^U` aparecer no prompt, descarte essa entrada e reenvie o comando limpo.
- Após `ng test` ou `ng build`, aguarde a conclusão e o resumo final do Angular ou Vitest antes de concluir que não houve saída.
- Exemplos: `Set-Location frontend/web; ng test --watch=false` e `Set-Location frontend/web; ng build --configuration production`.
- Toda automação global de infraestrutura, administração, manutenção e validação pertence a `tools/scripts/`.
- Use PowerShell 7 para orquestração de CLIs, containers e sistema; use Python para parsing estruturado, APIs, lógica reutilizável ou testável.
- Ferramentas Python usam exclusivamente `uv`, compartilham o único `tools/pyproject.toml` e mantêm `tools/uv.lock` versionado.
- Não crie `requirements.txt`, ambientes virtuais manuais, outro `pyproject.toml` para automação ou scripts globais fora de `tools/scripts/`.
- Scripts de build, entrypoint, health check, instalação ou runtime permanecem no módulo proprietário.
- Todo script criado ou refatorado oferece `--help` sem efeitos colaterais e segue `.github/instructions/script-authoring.instructions.md` e `.github/SCRIPTING.md`.
- Scripts oficiais do Spec Kit/Bug Fixing permanecem em `.specify/scripts/` e `.specify/extensions/bug/scripts/`, preservando contrato e ajuda upstream. Esta exceção não autoriza scripts próprios nesses diretórios.

## Regras invariáveis

- Preserve conteúdo do usuário; conflitos nunca podem causar sobrescrita silenciosa.
- Normalize e autorize paths antes de qualquer acesso ao filesystem.
- Nunca exponha sockets Docker/Podman pela rede nem implemente proxy genérico da API do engine.
- Autorize antes de revelar ambiente, container ou metadado.
- No desenvolvimento local em Windows, armazene todo secret exclusivamente em `$env:APPDATA\Microsoft\UserSecrets\Dokpod\.env`. Isso inclui credenciais administrativas do Keycloak, PostgreSQL, client secrets, connection strings, certificados e qualquer outro valor sensível.
- Nunca crie arquivos `.env`, `.env.*`, `secrets.json`, cópias ou templates com valores reais dentro do repositório. Não use `dotnet user-secrets`; scripts, Compose, coleções HTTP, testes e documentação devem referenciar o arquivo externo ou variáveis de ambiente injetadas por processo seguro.
- Não leia, exiba, registre, copie ou sobrescreva o conteúdo do arquivo externo durante validações. Antes de mover ou gravar secrets, falhe fechado se o destino já existir.
- Keycloak mantém usuários, credenciais, MFA, sessões, roles, recursos, scopes e políticas; o Dokpod não os reimplementa.
- O BFF mantém tokens fora do browser; a API atua como PEP e falha fechada ao aplicar decisões do Keycloak.
- Agentes usam identidade individual, mTLS, rotação e revogação.
- Operações mutáveis usam ID idempotente, expiração, auditoria e reconciliação.
- Não presuma equivalência entre Docker e Podman; anuncie e valide capabilities.
- Não registre tokens, certificados privados, variáveis de ambiente, secrets ou logs integrais de containers.
- Exclusão de container não remove volume implicitamente.
- Dependências externas exigem licença permissiva, gratuita, versão fixada e manutenção verificada.
- Nunca sugira, instale ou adicione biblioteca paga, proprietária, source-available, com licença comercial obrigatória, copyleft ou outra restrição incompatível com a distribuição AGPL-3.0-only do Dokpod. Licença ausente, ambígua ou não verificada bloqueia a dependência.
- O padrão arquitetural Mediator é permitido quando houver necessidade comprovada de desacoplar dispatch, handlers e pipelines. Nesses casos, use `Nuuvify.CommonPack.Mediator` com versão centralizada e licença da versão exata verificada; nunca use MediatR.
- Não adicione broker, cache distribuído, microsserviço ou novo datastore sem ADR e evidência operacional.
- APIs públicas usam OpenAPI, `application/problem+json` e versionamento explícito.
- Mudanças de protocolo mantêm compatibilidade N/N-1 ou documentam migração e rollout.
- Mudanças de schema seguem expand-contract e devem ser testadas com PostgreSQL real.
- Tabelas com potencial de crescimento elevado ou retenção temporal devem usar Table Partitioning/Declarative Partitioning por faixa de data, preferencialmente partições mensais; a solução deve definir criação antecipada, rollover, partição de segurança para datas fora da janela, índices por partição, retenção e testes de roteamento antes da produção, independentemente do banco de dados adotado.

## Qualidade mínima

- Código novo possui testes nos níveis adequados ao risco.
- Rode formatador, análise estática, build e testes do projeto alterado.
- Adapters são testados contra engines reais, não apenas mocks.
- Para fluxos do usuário, atualize ou adicione Playwright.
- Para PostgreSQL e filesystem, use testes de integração reais, não mocks como evidência principal.
- Imagens possuem build e smoke test; o pacote Windows possui instalação e smoke test em host sem runtime .NET.
- Superfícies privilegiadas recebem threat model e revisão de segurança.
- Reviews priorizam integridade de dados, segurança, regressões, concorrência e lacunas de teste.

## Instruções especializadas

As regras detalhadas em `.github/instructions/` são carregadas pelo caminho alterado ou pela descrição. Não replique essas regras em código ou documentação local.