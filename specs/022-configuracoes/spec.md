# Especificação da Feature: Configurações

**Branch da feature**: `022-configuracoes`

**Criada em**: 2026-10-16

**Estado**: `implemented` - aprovada pelo solicitante em 2026-10-16 ("Continue a implementação"); implementada e integrada em 2026-10-16 (PR #27); evidências em [tasks.md](tasks.md).

**Entrada**: Pedido do solicitante em 2026-10-16: "pode seguir", após a spec 021. Último item "Em breve" do menu: Governança → Configurações ("Preferências da instalação Dokpod").

**Depende de**: [016](../016-governanca-alertas/spec.md) (política de justificativa por estágio), [017](../017-auditoria/spec.md) (trilha), [020](../020-registros/spec.md) (política de registros permitidos), [008](../008-visao-geral-hosts-eventos/spec.md) (situação e versão dos agentes).

**Design**: não há board de Configurações em `docs/design/app`. O mockup define o item "Configurações" na seção Governança. A tela segue as vistas em abas de Alertas e Registros.

## Esclarecimentos

### Sessão 2026-10-16 (recomendações adotadas, sujeitas à aprovação)

- P: O que entra em Configurações? → R: Três vistas: **Políticas** (o que já é editável no console, reunido), **Instalação** (versões e saúde) e **Retenções e limites** (valores efetivos, somente leitura).
- P: As políticas saem de Alertas e de Registros? → R: Não. Configurações reúne a política de justificativa por estágio (spec 016) e a de registros permitidos (spec 020) em um só lugar, com as mesmas regras de edição e auditoria; as vistas atuais continuam funcionando, sem regressão.
- P: Quem vê Configurações? → R: Quem tem `environment:manage` em algum host (administradores). Versões, saúde e limites ajudam a operar, mas também descrevem a instalação; usuários só de leitura não precisam deles. O item de menu só aparece para quem pode ver.
- P: Retenções e limites podem ser editados pelo console? → R: Não nesta entrega. São configurações de implantação, com validação no início da API e efeito em processos de manutenção; a tela mostra os valores efetivos e de onde vêm, para conferência e auditoria de conformidade.
- P: Preferências do usuário (tema, fuso), gestão de usuários e grupos, notificações externas? → R: Fora desta entrega. Usuários e grupos continuam no Keycloak; o console já segue o tema do sistema.

## Cenários do Usuário e Testes (obrigatório)

### Jornada do Usuário 1 - Políticas em um só lugar (Prioridade: P1)

Um administrador abre Governança → Configurações e vê e edita, em um só lugar, as políticas que hoje ficam em telas diferentes.

**Valor desta prioridade**: as políticas que mais afetam a operação (quando exigir justificativa, de onde baixar imagens) ficam espalhadas; reuni-las facilita revisão e conferência.

**Teste independente**: abrir a vista Políticas, conferir que mostra os mesmos valores de Alertas → Regras e de Registros → Política, alterar uma de cada e ver a mudança refletida nas telas originais e na auditoria.

**Cenários de aceite**:

1. **Dado** um administrador, **Quando** abre a vista Políticas, **Então** vê, por estágio, se a justificativa é obrigatória e quais registros são permitidos (ou "qualquer registro"), com quem alterou por último e quando.
2. **Dado** permissão de administração em todos os hosts do estágio, **Quando** altera uma política, **Então** a mudança vale imediatamente, fica na auditoria e aparece igual nas telas de Alertas e de Registros.
3. **Dado** um usuário sem a permissão em algum host do estágio, **Quando** vê a política, **Então** ela aparece somente para leitura.

---

### Jornada do Usuário 2 - Saber o que está instalado e se está saudável (Prioridade: P1)

O administrador confere versões e a saúde dos componentes antes de uma atualização ou ao investigar um problema.

**Valor desta prioridade**: hoje não há no console como saber a versão do plano de controle, o nível do banco ou quantos agentes precisam de atualização.

**Teste independente**: abrir a vista Instalação e conferir versões, nível do banco, saúde de cada dependência e a distribuição de versões dos agentes contra o que está implantado.

**Cenários de aceite**:

1. **Dado** a vista Instalação, **Quando** aberta, **Então** mostra a versão do plano de controle, do BFF e do console, a versão do protocolo dos agentes e o nível de atualização do banco.
2. **Dado** as dependências, **Quando** exibidas, **Então** mostram a situação de cada uma (banco, provedor de identidade, canal em tempo real) com a hora da última verificação.
3. **Dado** os agentes, **Quando** exibidos, **Então** a tela mostra quantos hosts há por versão de agente e de engine e quantos precisam de atualização, com link para a tela Hosts filtrada.

---

### Jornada do Usuário 3 - Conferir retenções e limites (Prioridade: P2)

O administrador ou um auditor confere por quanto tempo cada dado fica guardado e quais limites operacionais estão valendo.

**Valor desta prioridade**: conformidade e investigação dependem de saber, por exemplo, por quanto tempo comandos e métricas ficam guardados e quanto dura uma sessão de terminal.

**Teste independente**: abrir a vista Retenções e limites e conferir os valores contra a configuração da implantação, inclusive um valor alterado na configuração.

**Cenários de aceite**:

1. **Dado** a vista, **Quando** aberta, **Então** mostra as retenções (comandos, métricas, eventos, alertas, auditoria) e os limites (lote, exportação da auditoria, sessões e duração do terminal, justificativa do terminal) com o valor efetivo e se vem do padrão ou da configuração da instalação.
2. **Dado** um valor alterado na configuração da implantação, **Quando** a API reinicia, **Então** a vista mostra o novo valor como "configurado na instalação".

---

## Casos de Borda

- Dependência indisponível (por exemplo, o provedor de identidade): a vista Instalação mostra a falha com a hora da verificação, sem quebrar as demais vistas.
- Hosts sem agente conectado: contam como "versão desconhecida" na distribuição.
- Administrador de um só estágio: vê Configurações; as políticas dos outros estágios aparecem somente para leitura.
- Valores sem limite configurado (por exemplo, sem lista de registros): exibidos como "qualquer registro" ou "sem limite", nunca em branco.

## Requisitos (obrigatório)

### Requisitos Funcionais

- **FR-001**: A tela Configurações DEVE substituir a página "Em breve" e só aparecer no menu para quem tem `environment:manage` em algum host; a API DEVE recusar os demais.
- **FR-002**: A vista Políticas DEVE reunir a política de justificativa por estágio e a de registros permitidos, com as regras de leitura, edição e auditoria das specs 016 e 020, sem alterar as telas originais.
- **FR-003**: A vista Instalação DEVE mostrar versões do plano de controle, BFF e console, versão do protocolo, nível de atualização do banco e a situação das dependências com a hora da verificação.
- **FR-004**: A vista Instalação DEVE mostrar a distribuição de versões de agentes e engines dos hosts legíveis, com os que precisam de atualização e link para Hosts.
- **FR-005**: A vista Retenções e limites DEVE mostrar, somente para leitura, os valores efetivos e se vêm do padrão ou da configuração da instalação; nenhum segredo (senhas, chaves, cadeias de conexão) DEVE aparecer.

## Entidades Principais

- **Política operacional**: tipo (justificativa, registros), estágio, valor, autor e horário da última alteração.
- **Componente instalado**: nome, versão, situação, hora da verificação.
- **Parâmetro operacional**: nome, valor efetivo, unidade, origem (padrão ou instalação).

## Critérios de Sucesso (obrigatório)

### Resultados Mensuráveis

- **SC-001**: As políticas exibidas em Configurações coincidem com as das telas de Alertas e Registros em 100% dos estágios, e uma alteração feita em uma aparece na outra.
- **SC-002**: As versões e o nível do banco exibidos coincidem com os da implantação em 100% dos componentes da stack de teste.
- **SC-003**: Os valores de retenção e limite coincidem com a configuração efetiva, inclusive após alterar um deles e reiniciar a API.
- **SC-004**: Nenhum segredo da configuração aparece na resposta da API ou na tela (varredura por canário).
- **SC-005**: A tela não tem violações sérias ou críticas de acessibilidade e é operável só por teclado.

## Premissas

- As políticas reutilizam as APIs e as regras de autorização das specs 016 e 020, sem configuração nova.
- Versões vêm dos próprios componentes (API, BFF e console publicados) e dos agentes conectados (spec 008).
- Fora do escopo: editar retenções e limites pelo console, preferências por usuário, gestão de usuários e grupos, notificações externas, backup e restauração.
