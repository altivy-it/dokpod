# Plano de Implementação: Responsáveis e colaboração no GitHub

**Branch**: `032-colaboracao-responsaveis-github` | **Data**: 2026-11-06 | **Spec**: [spec.md](spec.md)

**Entrada**: Especificação em `specs/032-colaboracao-responsaveis-github/spec.md`

## Resumo

Distribuir os donos no CODEOWNERS, registrar os responsáveis na governança e corrigir as referências ao repositório antigo. A mudança é somente documental e de configuração de revisão.

## Contexto Técnico

**Linguagem/Versão**: N/A; Markdown, YAML e CODEOWNERS

**Dependências Principais**: nenhuma nova

**Armazenamento**: N/A

**Testes**: resolução local de donos por última correspondência, busca textual, API do GitHub para permissões e erros de CODEOWNERS

**Plataforma-alvo**: GitHub (`altivy-it/dokpod`)

**Tipo de Projeto**: monorepo; governança do repositório

**Metas de Desempenho**: N/A

**Restrições**: não editar arquivos gerenciados pelo Groundwork, nem código de aplicação

**Escala/Escopo**: três contas e as áreas de primeiro nível do monorepo

## Verificação da Constituição

- **Documentação em pt-BR**: atendida.
- **Spec Kit como processo único**: atendida por esta feature.
- **Sem commit, push, publicação ou deploy sem solicitação explícita**: commit e PR dependem de pedido próprio.
- **Sem código de aplicação ou runner local**: não há build nem testes de aplicação; as validações leem arquivos e a API.

## Estrutura do Projeto

### Documentação (desta feature)

```text
specs/032-colaboracao-responsaveis-github/
├── plan.md
├── spec.md
├── tasks.md
└── checklists/requirements.md
```

### Arquivos alterados (raiz do repositório)

```text
.github/CODEOWNERS
.github/CONTRIBUTING.md
.github/GOVERNANCE.md
.github/ISSUE_TEMPLATE/config.yml
docs/github-projeto-gestao.md
specs/README.md
```

**Decisão de Estrutura**: a responsabilidade por área fica em `.github/GOVERNANCE.md`, já indicada como fonte de governança; os demais arquivos apontam para ela.

## Pesquisa e Decisões

- **Dono único por linha ou listas completas**: listas completas, porque a última correspondência substitui as anteriores.
- **Equipes ou contas diretas**: contas diretas; a organização não tem equipes e criar equipes amplia o escopo.
- **Permissão de `eusener`**: permanece `admin`; reduzir acesso exige decisão própria.
