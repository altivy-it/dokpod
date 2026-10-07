# Plano de Implementação: [FEATURE]

**Branch**: `[###-feature-name]` | **Data**: [DATE] | **Spec**: [link]

**Entrada**: Especificação em `/specs/[###-feature-name]/spec.md`

**Idioma**: Escreva este plano integralmente em português brasileiro (pt-BR). Preserve comandos, identificadores, nomes de tecnologias e caminhos literais.

## Resumo

[Sintetize a necessidade principal da spec e a abordagem técnica proposta.]

## Contexto Técnico

<!-- Substitua esta seção pelos detalhes técnicos reais do projeto. -->

**Linguagem/Versão**: [ex.: .NET 10, Angular 22 ou NEEDS CLARIFICATION]

**Dependências Principais**: [Dependências necessárias e já aprovadas]

**Armazenamento**: [PostgreSQL ou não aplicável]

**Testes**: [Ferramentas e níveis de teste usados pelo projeto]

**Plataforma Alvo**: [ex.: containers Linux ou agente Windows self-contained]

**Tipo de Projeto**: [ex.: aplicação web, biblioteca, API, agente]

**Objetivos de Desempenho**: [Metas mensuráveis ou NEEDS CLARIFICATION]

**Restrições**: [Limites de segurança, compatibilidade, operação e recursos]

**Escopo**: [Módulos, jornadas e componentes afetados]

## Verificação da Constituição

**GATE**: Deve passar antes da pesquisa da Fase 0 e ser reavaliado após o desenho da Fase 1.

[Registre as regras da constituição aplicáveis, evidências, riscos e exceções que exigem decisão humana.]

## Estrutura do Projeto

### Documentação desta Feature

```text
specs/[###-feature]/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
└── tasks.md
```

`tasks.md` é gerado por `/speckit-tasks`, não por `/speckit-plan`.

### Código-fonte

```text
[Registre somente diretórios reais do repositório afetados pela feature.]
```

**Decisão de Estrutura**: [Explique como a feature se encaixa nos módulos existentes e referencie os diretórios reais.]

## Registro de Complexidade

> Preencha somente quando houver violação justificada da constituição.

| Violação | Justificativa | Alternativa mais simples rejeitada e motivo |
| -------- | ------------- | ------------------------------------------ |
| [Regra violada] | [Necessidade concreta] | [Por que não atende] |
