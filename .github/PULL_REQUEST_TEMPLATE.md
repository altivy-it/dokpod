# Pull request

## Objetivo

<!-- Problema resolvido e resultado observável. -->

## Escopo

<!-- Mudanças e itens deliberadamente excluídos. -->

## Spec Kit e decisões

- Feature/artefatos Spec Kit ou assessment oficial Bug Fixing:
- IDs das tarefas/remediações:
- Autorização humana explícita (referência, responsável e limites):
- Issue de intake/rastreabilidade (não autoriza execução):
- ADR:

- [ ] O diff corresponde somente às tarefas/remediações autorizadas
- [ ] Dependências e bloqueios de análise foram tratados
- [ ] Evidências foram registradas nos artefatos oficiais; checkboxes não representam aprovação humana
- [ ] Não foi criado fluxo paralelo em plano, prompt, agente ou Issue

## Áreas

- [ ] Frontend Angular
- [ ] API .NET
- [ ] BFF/Keycloak
- [ ] Agente .NET
- [ ] Contrato OpenAPI/gRPC
- [ ] PostgreSQL/migration
- [ ] Segurança
- [ ] Container/operação
- [ ] Documentação

## Evidências

- [ ] `CI / Result`
- [ ] `Gitleaks / Full History`
- [ ] Formatação e análise estática
- [ ] Build de produção
- [ ] Testes unitários
- [ ] Testes de integração com engine/PostgreSQL real
- [ ] Testes de contrato e compatibilidade N/N-1
- [ ] Playwright desktop/mobile
- [ ] Build e smoke test das imagens afetadas
- [ ] Publicação e smoke test do agente Windows self-contained

Comandos e resultados:

## Segurança e operação

- [ ] Autenticação e autorização foram avaliadas
- [ ] Recursos/scopes Keycloak e comportamento fail-closed foram avaliados
- [ ] Tokens permanecem fora do browser e não chegam ao agente
- [ ] Idempotência, timeout e reconexão foram avaliados
- [ ] Journal-before-ack, fencing, replay e recuperação após falha parcial foram avaliados
- [ ] Matriz Docker/Podman x Linux/Windows declara versões e combinações não suportadas ou não verificadas
- [ ] Nenhum socket foi exposto nem proxy genérico adicionado
- [ ] Nenhum secret ou dado sensível foi incluído
- [ ] Nenhuma connection string, senha de teste ou credencial foi incluída em
  código, testes, documentação, scripts ou workflows
- [ ] Secrets necessários ao CI foram criados no Environment do GitHub, e não
  commitados no repositório
- [ ] Dependências possuem licença e versão verificadas
- [ ] Testes de carga seguem os critérios do Dokpod quando o caminho crítico foi alterado
- [ ] Rollout, rollback e observabilidade estão documentados

## Riscos e limitações

<!-- Riscos residuais e validações não executadas. -->

Registre gates não executados como `NOT RUN`, com motivo; não marque aprovação
sem evidência. Reviews são read-only. Este PR não autoriza commit, push, merge,
publicação ou deploy por um agente.
