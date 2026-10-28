# Especificação da Feature: Modelos de SSO prontos

**Branch da feature**: `025-029-roteiro-recursos`

**Criada em**: 2026-10-27

**Estado**: `draft` - aguardando revisão humana.

**Entrada**: Pedido do solicitante em 2026-10-27: "Itens de referência que ainda não estão no dokpod, inclua todos no roadmap", item 5 de 5: modelos prontos de SSO para Microsoft Entra ID, Google e GitHub.

**Depende de**: [docs/configuracao-keycloak.md](../../docs/configuracao-keycloak.md) (realm, clients e scopes), [ADR 2026-0002](../../docs/adr/2026-0002-distribuicao-e-identidade.md) e ADR 2026-0004 (plataforma global de identidade), tema de login do Keycloak (`deploy/keycloak/themes`).

## Esclarecimentos

### Sessão 2026-10-27 (recomendações adotadas, sujeitas à aprovação)

- P: Onde o SSO é configurado? → R: No Keycloak, que continua a autoridade de identidade (constituição, princípio III). O Dokpod entrega modelos prontos de provedor de identidade, mapeamento de grupos e botões no tema de login; não guarda credenciais nem políticas próprias.
- P: Quais provedores? → R: Microsoft Entra ID, Google Workspace e GitHub (organização), os mais pedidos por empresas que já usam essas contas.
- P: Quem entra pelo provedor ganha acesso? → R: Não. Um usuário novo entra sem permissão nenhuma, até que um grupo mapeado (por exemplo, um grupo do Entra ID ou um time do GitHub) o coloque em um grupo do Dokpod. Isso preserva o menor privilégio.
- P: Como aplicar o modelo? → R: Pelo script de configuração do Keycloak, com parâmetros (provedor, identificadores do aplicativo, domínio permitido, mapeamento de grupos) e segredo vindo de arquivo ou provider seguro, nunca de argumento de linha de comando.

## Cenários do Usuário e Testes (obrigatório)

### Jornada do Usuário 1 - Entrar com a conta Microsoft da empresa (Prioridade: P1)

O administrador aplica o modelo do Entra ID; os usuários passam a ver "Entrar com Microsoft" na tela de login e entram com a conta corporativa.

**Valor desta prioridade**: elimina senhas separadas e herda MFA e desligamento de usuários da empresa.

**Teste independente**: aplicar o modelo contra um tenant de teste, entrar com um usuário do grupo mapeado e com um usuário fora do grupo, e conferir acesso e auditoria.

**Cenários de aceite**:

1. **Dado** o modelo aplicado, **Quando** o usuário abre a tela de login, **Então** vê o botão do provedor no tema do Dokpod.
2. **Dado** um usuário em grupo do Entra ID mapeado para um grupo do Dokpod, **Quando** entra, **Então** recebe as permissões desse grupo.
3. **Dado** um usuário sem grupo mapeado, **Quando** entra, **Então** a sessão é criada sem nenhum ambiente visível e a tela orienta a pedir acesso.

---

### Jornada do Usuário 2 - Google e GitHub (Prioridade: P2)

O administrador aplica os modelos do Google Workspace (restrito ao domínio da empresa) e do GitHub (restrito à organização).

**Valor desta prioridade**: cobre empresas que não usam Microsoft e times de engenharia que vivem no GitHub.

**Teste independente**: aplicar cada modelo e repetir os cenários da jornada 1, incluindo uma conta de fora do domínio ou da organização.

**Cenários de aceite**:

1. **Dado** o modelo do Google com domínio permitido, **Quando** uma conta de outro domínio tenta entrar, **Então** o login é recusado.
2. **Dado** o modelo do GitHub com organização permitida, **Quando** um usuário de fora da organização tenta entrar, **Então** o login é recusado.

---

### Jornada do Usuário 3 - Conferir os provedores configurados (Prioridade: P3)

O administrador vê em Configurações → Instalação quais provedores de SSO estão ativos e quando foram verificados.

**Valor desta prioridade**: facilita auditoria de conformidade sem acesso ao console do Keycloak.

**Teste independente**: com dois modelos aplicados, conferir a lista e a situação de cada um.

**Cenários de aceite**:

1. **Dado** provedores configurados, **Quando** a vista Instalação é aberta, **Então** lista cada provedor, se está ativo e a hora da última verificação, sem exibir segredos.

---

## Casos de Borda

- Segredo do aplicativo expirado no provedor: o login pelo provedor falha com mensagem clara; o login local de administração continua disponível.
- Reaplicar o modelo: atualiza a configuração sem duplicar provedor nem apagar vínculos de usuários existentes.
- Usuário removido do grupo no provedor: perde as permissões no próximo login, ou antes, conforme a sincronização configurada.
- E-mail igual em dois provedores: não vincula contas automaticamente sem confirmação.

## Requisitos (obrigatório)

### Requisitos Funcionais

- **FR-001**: O projeto DEVE entregar modelos aplicáveis de provedor de identidade para Microsoft Entra ID, Google Workspace e GitHub, por parâmetros e de forma idempotente.
- **FR-002**: Cada modelo DEVE permitir restringir o login a domínio, tenant ou organização e mapear grupos do provedor para grupos do Dokpod.
- **FR-003**: Usuários novos NÃO DEVEM receber permissão sem mapeamento de grupo.
- **FR-004**: O tema de login DEVE exibir os provedores ativos com o visual do Dokpod e manter o login local de administração.
- **FR-005**: Segredos dos provedores NÃO DEVEM entrar em Git, imagem, argumentos de processo ou logs.
- **FR-006**: A vista Instalação DEVE listar os provedores ativos e sua situação, sem segredos.

## Entidades Principais

- **Modelo de provedor**: provedor, restrição (domínio, tenant ou organização), mapeamento de grupos, situação.

## Critérios de Sucesso (obrigatório)

### Resultados Mensuráveis

- **SC-001**: Um administrador aplica um modelo e conclui o primeiro login pelo provedor em até 15 minutos, seguindo só a documentação.
- **SC-002**: 100% dos logins de contas fora do domínio, tenant ou organização permitidos são recusados.
- **SC-003**: 100% dos usuários sem grupo mapeado entram sem nenhum ambiente visível.
- **SC-004**: A varredura por canário não encontra segredos de provedor no repositório, nas imagens ou nos logs.

## Premissas

- Edição prevista: comercial (Business); o login federado já possível pela configuração manual do Keycloak continua disponível na CE.
- Os aplicativos no Entra ID, Google e GitHub são criados pelo cliente; a documentação descreve o passo a passo.
- Fora do escopo: SAML genérico, provisionamento SCIM e gestão de usuários pelo console do Dokpod.
