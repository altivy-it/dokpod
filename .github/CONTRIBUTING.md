# Contribuindo com o Dokpod

## Antes de começar

1. Leia o [README](../README.md) e os documentos da área afetada.
2. Procure os artefatos Spec Kit, tarefa/remediação, Issue e ADR relacionados.
3. Confirme autorização humana explícita e limites da tarefa/remediação antes de executar; Issues são intake, não autorização.
4. Nunca inclua secrets em nenhuma parte do código ou configuração, inclusive
   workflows, testes, documentação, exemplos, scripts e connection strings.
   Tokens, chaves, certificados privados, dados reais de infraestrutura e logs
   sensíveis são sempre proibidos.
5. Para persistência e migrations, siga as [convenções de EF Core](EFCORE.md);
   secrets locais permanecem exclusivamente no arquivo externo
   `$env:APPDATA\Microsoft\UserSecrets\Dokpod\.env`, sem leitura por automações, e secrets de CI
   permanecem nos Environments do GitHub.
6. Instale o hook obrigatório de detecção de secrets em cada clone:

   ```powershell
   ./tools/scripts/install-gitleaks-hook.ps1
   git hook run pre-commit
   ```

   Consulte a [política de detecção de secrets](SECRET-SCANNING.md).
   O hook usa `.gitleaks.toml`; não o substitua por uma execução sem a
   configuração versionada.

## Licenciamento de contribuições

O código da Community Edition será publicado sob [`AGPL-3.0-only`](../LICENSE), conforme a [política de licenciamento](../docs/licenciamento.md). O Dokpod pretende manter a possibilidade de oferecer uma edição Business sob licença comercial.

Até que um Contributor License Agreement ou mecanismo equivalente seja aprovado juridicamente e publicado, contribuições externas de código não serão incorporadas. Issues, relatos de falha e discussões de projeto continuam bem-vindos, mas o envio de uma contribuição não implica sua aceitação nem concede automaticamente os direitos necessários ao duplo licenciamento.

## Fluxo

1. Para features e manutenção, use o processo Spec Kit de especificação, esclarecimento, planejamento, tarefas e análise. Requisitos, desenho e execução pertencem a `specs/<feature>/spec.md`, `plan.md` e `tasks.md`; não crie planos em `docs/plan`.
2. Para defeitos, use a extensão oficial Bug Fixing: `/speckit-bug-assess`, remediação explicitamente autorizada via `/speckit-bug-fix` e verificação via `/speckit-bug-test`. Sem artefatos ou extensão, pare; não substitua por fluxo local.
3. Execute somente tarefas explicitamente autorizadas via `/speckit-implement`, respeitando dependências e bloqueios de `/speckit-analyze`. A criação de branch ou a Issue não autoriza execução.
4. Atualize testes, contratos, documentação e telemetria necessários à tarefa; valide o menor recorte primeiro e registre evidências no artefato e PR.
5. Use `/speckit-converge` para pendências, sem ampliar escopo por conta própria. ADR auxiliar exige justificativa de `/speckit-plan` e permanece `proposed` até decisão humana.
6. Use os [Conventional Commits](COMMIT_CONVENTIONS.md) somente quando houver solicitação de commit. Commit, push, merge, publicação e deploy exigem autorização separada.

## Scripts e automação

Automações globais de infraestrutura, administração, manutenção e validação ficam em `tools/scripts/`. Escolha PowerShell 7 para orquestração de CLIs e sistema; escolha Python para parsing, APIs ou lógica reutilizável e execute-o exclusivamente com `uv` pelo projeto compartilhado `tools/pyproject.toml`.

Scripts de build, entrypoint, health check, instalação ou runtime permanecem no módulo proprietário. Consulte [Scripts e automação](SCRIPTING.md) antes de criar ou mover um script.

Scripts oficiais do Spec Kit e Bug Fixing preservam localização, interface e
contrato upstream; não os mova nem imponha `--help` para adequação local.

## Comandos disponíveis

Execute os comandos na raiz do repositório. O backend não depende de SDK .NET
instalado no host; build e testes usam a imagem de toolchain fixada.

```powershell
docker run --rm --volume "${PWD}:/workspace" --workdir /workspace `
   lzocateli/dotnet-sdk:10.0.400-noble `
   dotnet build Dokpod.slnx --configuration Release --verbosity minimal

docker run --rm --volume "${PWD}:/workspace" --workdir /workspace `
   lzocateli/dotnet-sdk:10.0.400-noble `
   dotnet test Dokpod.slnx --configuration Release --verbosity minimal
```

O teste de integração Docker monta o socket do engine e concede privilégios
equivalentes aos do daemon. Execute-o somente em clone e host confiáveis:

```powershell
docker run --rm --group-add 0 `
   --volume "${PWD}:/workspace" `
   --volume /var/run/docker.sock:/var/run/docker.sock `
   --workdir /workspace/backend `
   lzocateli/dotnet-sdk:10.0.400-noble `
   dotnet test tests/Dokpod.Agent.Docker.IntegrationTests/Dokpod.Agent.Docker.IntegrationTests.csproj `
      --configuration Release --verbosity minimal
```

As tarefas equivalentes estão em `.vscode/tasks.json`. Comandos de frontend,
Compose e publicação do agente Windows serão documentados quando os respectivos
manifests e gates existirem. Não substitua as toolchains containerizadas por SDKs
globais no fluxo oficial.

## Definition of Done

- tarefa/remediação Spec Kit e autorização explícita rastreáveis, com evidências no artefato de origem;
- critérios de aceite demonstrados;
- código formatado, analisado e compilado;
- testes reais proporcionais ao engine, sistema operacional e risco;
- contratos e clientes gerados sincronizados;
- segurança, acessibilidade, concorrência e desempenho considerados;
- logs e métricas não expõem conteúdo sensível;
- hook Gitleaks instalado e `Gitleaks / Full History` aprovado;
- `CI / Result` aprovado;
- rollout, rollback e compatibilidade documentados;
- nenhum achado crítico ou alto aberto sem aceitação formal.

## Pull requests

Mantenha PRs coesos e pequenos o bastante para revisão responsável. Mudanças incompatíveis exigem issue, ADR quando aplicável, estratégia de migração e período de compatibilidade. Não misture refactor amplo com mudança funcional.
