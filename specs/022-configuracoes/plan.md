# Plano de Implementação: Configurações

**Branch**: `022-configuracoes` | **Data**: 2026-10-16 | **Spec**: [spec.md](spec.md)

**Estado**: `approved` - plano derivado da spec aprovada em 2026-10-16.

## Resumo

Tela Governança → Configurações com três vistas: Políticas (justificativa por estágio e registros permitidos, pelas APIs das specs 016 e 020), Instalação (versões, nível do banco, saúde das dependências e distribuição dos agentes) e Retenções e limites (valores efetivos, somente leitura). Acesso só para quem tem `environment:manage` em algum host. Sem mudança no agente nem no protocolo.

## Contexto Técnico

**Linguagem/Versão**: .NET 10, OpenAPI 3.1, Angular 22.

**Armazenamento**: nenhum novo; lê o histórico de migrations do EF.

**Testes**: aplicação/API (acesso, parâmetros com origem, canário de segredo, `canEdit` da política de justificativa), arquitetura (versão do produto igual no backend e no console), Vitest, E2E simulada e stack real.

**Restrições**: nenhum segredo na resposta; retenções e limites somente leitura; telas de Alertas e Registros sem regressão.

## Verificação da Constituição

| Princípio | Situação |
| --- | --- |
| I. Documentação em pt-BR | OK. |
| II. Engine local como fonte | OK: versões dos agentes vêm dos agentes conectados. |
| III. Identidade externa e menor privilégio | OK: acesso decidido no Keycloak (`environment:manage` em algum host); edição das políticas mantém as regras das specs 016 e 020. |
| IV. Operações duráveis | N/A (somente leitura, exceto as políticas existentes). |
| V. Modularidade e compatibilidade | OK: endpoints novos; nenhuma mudança de contrato do agente. |
| VI. Qualidade verificável | OK: testes por camada, canário, E2E simulada e stack real. |
