# Bug Assessment: Falhas de CI e falsos positivos de secrets no PR 6

- **Slug**: pr6-ci-gitleaks
- **Created**: 2026-10-06
- **Source**: pasted text
- **Verdict**: valid
- **Severity**: high

## Report (verbatim or summarized)

Pedido humano: resolver as falhas do backend, frontend e Gitleaks do PR 6.
Evidencia consultada na sessao: CI 37557301037 e Gitleaks 37557301020.
O job Images e as aprovacoes obrigatorias do PR nao fazem parte desta remediacao.

## Symptom

Backend falha com NETSDK1004 antes de executar migrations. Frontend gera o
cliente API, mas Git nao reconhece o checkout no container. Full History
reporta 37 ocorrencias da regra dokpod-connection-string-credential.
Os builds locais isolados passaram, mas nao cobrem esses passos da CI.

## Reproduction

1. Executar o workflow CI num checkout limpo: Restore local tools nao restaura
   os projetos da solucao; Apply database migrations falha ao carregar assets.
2. Executar git rev-parse no container Angular com o checkout montado: Git
   rejeita a propriedade do repositorio. Repetir com safe.directory=/workspace
   especifico resolve a verificacao local.
3. Enviar git log --all --full-history -p ao stdin do Gitleaks 8.30.1 com a
   configuracao versionada e redaction total. O scan local registra 74
   ocorrencias porque inclui os historicos equivalentes nas branches de backup;
   o primeiro conjunto de 37 corresponde aos tipos reportados na CI.
4. Mapear apenas metadados e expressoes com os literais ocultados: os achados
   sao leituras de ambiente, helpers de secrets, referencias a variaveis ou
   configuracoes, dois valores vazios e um placeholder entre sinais de menor
   e maior. Nenhuma credencial real foi identificada nesses achados.

## Suspected Code Paths

- .github/workflows/ci.yml: backend.steps - migrations antes de restore.
- .github/workflows/ci.yml: frontend.steps - git diff sem safe.directory.
- .gitleaks.toml: dokpod-connection-string-credential - regex aceita sufixos de
  identificadores e qualquer expressao a direita da atribuicao como secret.
- .github/workflows/gitleaks.yml: Full History - usa essa regra em todo o diff.

## Root Cause Hypothesis

Confianca alta para a ordem de restore e o reconhecimento do checkout Git.
Confianca alta para os falsos positivos: a regra de connection string nao
delimita a chave e nao distingue um valor literal de uma expressao de runtime.
O scanner precisa continuar detectando passwords literais e as regras padrao,
sem ignorar arquivos, commits ou classes inteiras de secrets.

## Proposed Remediation

**Preferred**: adicionar restore explicito da solucao antes das migrations;
executar a verificacao Git do frontend com safe.directory limitado ao workspace;
delimitar as chaves da regra de connection string e capturar o valor separado,
com exclusoes locais a essa regra para expressoes runtime estritamente
reconhecidas, valores vazios e placeholders documentais. Manter as regras
padrao, Basic/Bearer/Docker, full-history, redaction e exit code de bloqueio.
Adicionar teste containerizado para credenciais sinteticas positivas e para
referencias runtime negativas; executa-lo antes do scan na CI.

**Files likely to change**:

- .github/workflows/ci.yml
- .github/workflows/gitleaks.yml
- .gitleaks.toml
- tools/scripts/test-gitleaks.ps1
- .specify/bugs/pr6-ci-gitleaks/fix.md
- .specify/bugs/pr6-ci-gitleaks/test.md

**Tests to add or update**:

- Checkout backend limpo: restore, metadata EF e migrations em PostgreSQL
  efemero isolado quando viavel, seguidos dos testes backend.
- Gerar cliente API e executar a verificacao Git dentro do container Angular;
  executar testes e build de producao.
- Teste Gitleaks positivo com valores gerados apenas em memoria e negativos
  com referencias runtime. Nao persistir tokens nem credenciais de teste.
- Repetir o scan de todo o historico com redaction e exit code normal de bloqueio.
- Validar syntax dos scripts, ajuda sem efeitos colaterais e git diff --check.

## Risks & Considerations

- A seguranca do scanner nao pode ser reduzida com allowlist global de paths,
  commits, expressoes vagas ou valores reais.
- As expressoes aceitas devem corresponder integralmente a referencias runtime;
  concatenacao com literal sensivel deve continuar sendo detectada.
- Nao ler arquivos externos de secrets. Valores dos achados foram suprimidos.
- Nao reescrever historico, revogar credenciais, alterar protecoes, publicar
  imagens, fazer deploy ou aceitar findings sem autorizacao humana especifica.
- Images continua bloqueado por vulnerabilidades criticas e fora deste pedido.
- Os checks remotos so refletirao a correcao apos publicacao e nova execucao.

## Open Questions

- Aprovacao humana desta remediacao e do escopo listado antes da implementacao.
- Nenhuma reescrita Git e proposta: os achados classificados sao falsos positivos.
