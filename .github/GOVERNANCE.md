# Governança do repositório

Esta pasta concentra regras de contribuição, segurança, decisões e instruções contextuais para desenvolvimento assistido.

Spec Kit é o único processo de features, manutenção e defeitos. GitHub Issues e
o Project `Dokpod Delivery` são intake e rastreabilidade, não autoridade para
implementar. A configuração administrativa versionada está em
[docs/github-projeto-gestao.md](../docs/github-projeto-gestao.md); instruções
legadas de execução por plano não autorizam trabalho fora do Spec Kit.

## Processo autorizado

- Features e manutenção seguem `/speckit-specify`, `/speckit-clarify`,
    `/speckit-plan`, `/speckit-tasks`, `/speckit-analyze` e execução explicitamente
    autorizada por `/speckit-implement`; `/speckit-converge` identifica pendências.
- Requisitos, desenho e tarefas vivem em `specs/<feature>/spec.md`, `plan.md`
    e `tasks.md`; a constituição Spec Kit registra princípios duradouros, não
    duplica procedimentos operacionais de `.github`.
- Defeitos seguem a extensão oficial Spec Kit Bug Fixing:
    `/speckit-bug-assess`, `/speckit-bug-fix` com remediação autorizada e
    `/speckit-bug-test`. Ausência da extensão ou dos artefatos bloqueia a correção;
    não crie diagnóstico ou workflow concorrente.
- Exija referência à tarefa/remediação, autorização humana, dependências e
    limites. Abrir Issue, selecionar agente ou marcar checkbox não autoriza execução.
- Agentes especializados e prompts auxiliares apoiam tarefas autorizadas;
    reviewers são read-only e inspecionam evidências, sem comandos mutantes.
- ADRs auxiliares são justificados por `/speckit-plan`, começam como `proposed`
    e nunca recebem aprovação automática. Planos legados não são executáveis.
- Checkboxes de tarefas registram implementação e validação, não aprovação.
    Commit, push, merge, publicação e deploy exigem autorização separada.

## Conteúdo

- `copilot-instructions.md`: contexto e regras invariáveis do Dokpod;
- `instructions/`: regras especializadas por caminho;
- `agents/`: especialistas selecionáveis e agentes de revisão read-only;
- `prompts/`: auxiliares de ADR, testes e revisão subordinados ao Spec Kit;
- `skills/`: processos oficiais Spec Kit e gates auxiliares de revisão/release;
- `COPILOT_GUIDE.md`: guia de escolha e uso das customizações;
- `SCRIPTING.md`: localização, linguagem e contrato das automações do repositório;
- `PERFORMANCE_TESTING_CRITERIA.md`: perfis, thresholds e evidências de capacidade;
- `ADR_TEMPLATE.md`: modelo para decisões arquiteturais;
- `PLAN_TEMPLATE.md`: aviso de obsolescência, sem autoridade operacional;
- `COMMIT_CONVENTIONS.md`: tipos e escopos de commit;
- `CONTRIBUTING.md`: fluxo de contribuição e Definition of Done;
- `CODEOWNERS`: ownership padrão e superfícies de revisão;
- `dependabot.yml`: atualizações semanais de Actions, NuGet e Docker;
- `SECRET-SCANNING.md`: política, instalação e tratamento de achados do Gitleaks;
- `SECURITY.md`: política de divulgação responsável;
- `PULL_REQUEST_TEMPLATE.md`: evidências exigidas em revisão;
- `ISSUE_TEMPLATE/`: intake e vínculo com tarefas/remediações Spec Kit.
- `workflows/`: checks obrigatórios de CI e detecção de secrets.

O CI backend e a detecção de secrets existem desde o baseline executável do
repositório. Novos gates são adicionados com os manifests correspondentes e após
validação do comando local equivalente. Nenhum gate existe apenas de forma
decorativa.

## Configuração administrativa

- exija `CI / Result` e `Gitleaks / Full History` no ruleset de `main`;
- habilite revisão de Code Owner quando houver revisor independente;
- habilite Dependency Graph, Dependabot Alerts e security updates;
- habilite Private Vulnerability Reporting, secret scanning e push protection;
- crie o Environment `ci` e mantenha nele os secrets de integração do workflow;
- nunca aceite secrets em commits, inclusive em workflows, testes, documentação,
  exemplos, scripts ou connection strings;
- mantenha `GITHUB_TOKEN` somente leitura por padrão e actions fixadas por SHA;
- proteja tags `v*` antes da primeira release.

Configurações do GitHub não são reconstruídas pelos arquivos versionados. Registre
responsável, data e evidência sanitizada para cada controle habilitado.

## Hierarquia de autoridade

1. Visão, documentação técnica, constituição Spec Kit e ADRs aceitos restringem as mudanças;
2. artefatos Spec Kit definem requisitos, desenho, tarefas/remediações e sua autorização;
3. `copilot-instructions.md` e instruções específicas definem procedimentos operacionais;
4. agente, prompt ou skill auxiliar apoia o processo oficial sem criar escopo;
5. convenções observadas no módulo proprietário orientam a implementação.

Em conflito entre requisitos, contexto e decisões, interrompa e peça decisão
humana. Documentos legados não substituem artefatos Spec Kit. Skills e agentes
não podem relaxar as regras invariáveis nem presumir autorização.
