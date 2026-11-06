# Especificação da Feature: Responsáveis e colaboração no GitHub

**Branch da Feature**: `032-colaboracao-responsaveis-github`

**Criada em**: 2026-11-06

**Status**: Draft

**Entrada**: Pedido do mantenedor: "etapa 3, ok para seguir", referente à Etapa 3 do plano de coordenação `docs/plan/gestao-dokpod-identidades-github.md` do repositório Altivy.

## Cenários de Usuário e Testes *(obrigatório)*

### História de Usuário 1 - Revisores corretos por área (Prioridade: P1)

Quem abre um PR recebe a solicitação de revisão dos responsáveis pela área alterada: `lunadev00` e `lzocateli` em backend; `eusener` e `lzocateli` em frontend e demais áreas.

**Por que esta prioridade**: hoje toda revisão recai sobre uma única pessoa, o que contraria a distribuição de responsabilidades decidida.

**Teste Independente**: resolver o dono de um caminho de cada área pela regra de última correspondência do CODEOWNERS e conferir o resultado, além de confirmar a permissão `write` ou superior de cada dono.

**Cenários de Aceitação**:

1. **Dado** um PR que altera `backend/**`, **Quando** os donos forem resolvidos, **Então** o resultado é `lzocateli` e `lunadev00`.
2. **Dado** um PR que altera `frontend/**`, `deploy/**`, `docs/**`, `tools/**` ou `.github/**`, **Quando** os donos forem resolvidos, **Então** o resultado é `lzocateli` e `eusener`.
3. **Dado** um PR que altera `contracts/**`, **Quando** os donos forem resolvidos, **Então** o resultado inclui as três contas, porque contratos são compartilhados entre backend e frontend.

---

### História de Usuário 2 - Responsabilidades documentadas (Prioridade: P2)

Quem contribui encontra, em um único lugar, quem responde por cada área e qual é o limite do CODEOWNERS.

**Por que esta prioridade**: sem registro, a distribuição fica implícita no arquivo de donos.

**Teste Independente**: ler a seção de responsáveis em `.github/GOVERNANCE.md` e seguir os links a partir de `.github/CONTRIBUTING.md` e `docs/github-projeto-gestao.md`.

**Cenários de Aceitação**:

1. **Dado** um contribuidor, **Quando** consultar a governança, **Então** encontra a responsabilidade por área e a preservação de `lzocateli` como mantenedor.
2. **Dado** uma revisão, **Quando** a regra for consultada, **Então** fica claro que autor e revisor precisam ser pessoas independentes.

---

### História de Usuário 3 - Referências ao repositório atual (Prioridade: P3)

Quem usa os links de contato e o processo de entrega chega ao repositório canônico `altivy-it/dokpod`.

**Por que esta prioridade**: o relato privado de vulnerabilidade não deve depender de um endereço antigo.

**Teste Independente**: buscar `lzocateli/Dokpod` nos arquivos ativos e não encontrar ocorrências.

**Cenários de Aceitação**:

1. **Dado** o formulário de novas Issues, **Quando** o contato for aberto, **Então** o link aponta para `altivy-it/dokpod`.

### Casos de Borda

- O arquivo CODEOWNERS é avaliado pela última linha correspondente: cada área precisa listar todos os seus donos, sem herdar de `*`.
- A lista de donos não garante revisão de todos os listados; basta a aprovação de um deles quando a revisão de Code Owner estiver habilitada.
- Dono sem permissão de escrita é ignorado pelo GitHub.

## Requisitos *(obrigatório)*

### Requisitos Funcionais

- **FR-001**: O padrão e as áreas sem backend DEVEM ter `lzocateli` e `eusener` como donos.
- **FR-002**: `backend/` DEVE ter `lzocateli` e `lunadev00` como donos.
- **FR-003**: `contracts/` DEVE ter `lzocateli`, `eusener` e `lunadev00` como donos.
- **FR-004**: A governança DEVE registrar a responsabilidade por área e preservar `lzocateli` como mantenedor durante a transição.
- **FR-005**: A governança DEVE exigir independência entre autor e revisor.
- **FR-006**: Os arquivos ativos DEVEM referenciar `altivy-it/dokpod` no lugar de `lzocateli/Dokpod`.
- **FR-007**: Esta feature NÃO DEVE alterar `repository-policy.json`, workflows `gw-*`, código de aplicação ou histórico Git.

### Entidades Principais

- **Área**: conjunto de caminhos do monorepo com responsáveis definidos.
- **Responsável**: conta GitHub com permissão de escrita ou superior no repositório.

## Critérios de Sucesso *(obrigatório)*

### Resultados Mensuráveis

- **SC-001**: 100% dos caminhos de primeiro nível versionados resolvem para os donos definidos em FR-001 a FR-003.
- **SC-002**: As três contas têm permissão `write` ou `admin`, confirmada pela API do GitHub.
- **SC-003**: Zero ocorrências de `lzocateli/Dokpod` em arquivos ativos, fora de `specs/` e `CHANGELOG.md`.
- **SC-004**: Zero erros na validação de CODEOWNERS do GitHub depois da publicação da branch.

## Premissas

- A divisão foi decidida pelo mantenedor no plano de coordenação em 2026-10-27 e o login `lunadev00` em 2026-11-06.
- `contracts/` como superfície compartilhada é uma proposta desta spec, sujeita à revisão do PR.
- A reescrita do histórico de identidades está fora desta feature e tem autorização e janela próprias.
- A criação do Project `Dokpod Delivery` e a habilitação da revisão de Code Owner continuam fora do escopo; a segunda é gerenciada pelo catálogo Altivy.Groundwork.
