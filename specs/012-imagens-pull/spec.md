# Especificação da Feature: Inventário de Imagens e Download (Pull)

**Branch da feature**: `007-console-operacional`

**Criada em**: 2026-10-12

**Estado**: `implemented` - concluída em 2026-10-14; revisão humana de segurança antes de release; evidências e itens `NOT RUN` em [tasks.md](tasks.md).

**Entrada**: Board `01 · Visão geral` (botão "Baixar imagem", KPI "Imagens sem uso", evento "Imagem atualizada"), board `04 · Novo container` (campo Imagem com ajuda "Assinatura verificada · 184 MB · publicada em 09/10/2026") e item de menu "Imagens". A tela Imagens não tem board próprio e deriva do padrão de `Containers.dc.html`.

**Depende de**: [007](../007-fundacao-console-design-system/spec.md), [008](../008-visao-geral-hosts-eventos/spec.md). **Alimenta**: [013](../013-criacao-container/spec.md).

## Cenários do Usuário e Testes (obrigatório)

### Jornada do Usuário 1 - Ver imagens por host e as sem uso (Prioridade: P1)

Como operador, quero listar as imagens de todos os hosts do estágio (repositório, tag, digest curto, tamanho, criação, host, engine e quantos containers a usam) e saber quantas estão sem uso.

**Valor desta prioridade**: KPI "Imagens sem uso" e base para escolher imagem ao criar container.

**Teste independente**: Hosts com imagens usadas, não usadas e sem tag; comparar com `docker images`/`podman images`.

**Cenários de aceite**:

1. **Dado** imagens sem nenhum container (em qualquer estado) que as use, comparando pelo ID da imagem informado pelo engine, **Quando** a Visão geral carrega, **Então** o KPI "Imagens sem uso" mostra a contagem do estágio.
2. **Dado** a tela Imagens, **Quando** aberta, **Então** lista imagens com filtros por host, engine e "sem uso", busca por repositório/tag e ordenação por tamanho e data.
3. **Dado** imagem sem tag (`<none>`), **Quando** listada, **Então** aparece como "sem tag" com digest curto.

### Jornada do Usuário 2 - Baixar uma imagem em hosts escolhidos (Prioridade: P1)

Como operador com `image:pull`, quero informar uma referência de imagem e baixá-la em um ou mais hosts do estágio, acompanhando o progresso e o resultado.

**Valor desta prioridade**: Preparar hosts antes de criar ou atualizar containers.

**Teste independente**: Pull de imagem pública e de registro privado já autenticado no host, em Docker e Podman; pull de referência inexistente; verificar auditoria e evento.

**Cenários de aceite**:

1. **Dado** referência válida e 2 hosts selecionados, **Quando** confirma "Baixar imagem", **Então** um comando idempotente por host é enviado e cada host mostra o estado do comando: pendente, em andamento, concluído, falhou ou indeterminado.
2. **Dado** pull concluído, **Quando** o inventário atualiza, **Então** a imagem aparece no host e são registrados o evento `image.pulled` (do engine) e o resultado `command.succeeded` (do pipeline), com rótulos da spec 008.
3. **Dado** referência inexistente ou sem autorização no registro, **Quando** o pull falha, **Então** o resultado mostra motivo legível (`image_not_found`, `registry_unauthorized`, `registry_unreachable`), sem expor credenciais.
4. **Dado** referência por tag mutável (ex.: `latest`), **Quando** informada, **Então** a interface alerta que a tag é mutável e mostra o digest obtido após o pull.

### Jornada do Usuário 3 - Informações da imagem ao escolher (Prioridade: P2)

Como operador, quero ver tamanho, data de criação e se a imagem já existe no host ao digitá-la no formulário de novo container.

**Valor desta prioridade**: Ajuda contextual do mockup ("184 MB · publicada em 09/10/2026").

**Teste independente**: Digitar referência existente e não existente no host selecionado.

**Cenários de aceite**:

1. **Dado** imagem presente no host, **Quando** informada, **Então** a ajuda mostra tamanho e data e "presente no host".
2. **Dado** imagem ausente, **Quando** informada, **Então** a ajuda informa "será baixada do registro ao criar".

## Casos de Borda

- Referência com digest (`@sha256:`) e com registro, porta e namespace; validação de formato antes de enviar.
- Pull demorado: prazo configurável (proposto: 15 min); progresso por camadas fica fora do escopo (a interface mostra "em andamento").
- Mesma imagem pedida duas vezes: idempotência por chave; pull concorrente no mesmo host é serializado pelo engine.
- Referência com credencial embutida, caracteres fora da gramática OCI ou parâmetros de query: rejeitada com `invalid_reference` antes de chegar ao agente e de novo no agente.
- Registro fora da lista permitida do estágio, quando a lista estiver configurada: `registry_not_allowed`.
- Plataforma: o pull usa a plataforma do host; escolher outra plataforma fica fora do escopo.
- Disco cheio no host: falha `insufficient_storage`.
- Plataforma da imagem incompatível com a arquitetura do host: falha `platform_mismatch`.
- Registro privado sem credencial no host: falha `registry_unauthorized`; o Dokpod não armazena credenciais de registro.

## Requisitos (obrigatório)

### Requisitos Funcionais

- **FR-001**: O agente DEVE publicar inventário de imagens por host (id, repositórios/tags, digests, tamanho, criação, arquitetura) por snapshot e deltas, com capability própria.
- **FR-002**: O plano de controle DEVE manter a projeção de imagens reconstruível e calcular "em uso" pelo ID da imagem dos containers projetados (não pela referência textual).
- **FR-003**: A API DEVE listar imagens de hosts autorizados (`environment:read`) com filtros, busca, ordenação, paginação e resumo (total, sem uso, tamanho total), e incluir "imagens sem uso" no resumo da Visão geral.
- **FR-004**: A API DEVE aceitar comando de pull por host com scope `image:pull`, referência validada, idempotência, prazo, auditoria e capability, seguindo o pipeline de comandos existente (journal-before-ack, at-least-once, reconciliação).
- **FR-004a**: A plataforma DEVE permitir configurar, por estágio, uma lista opcional de registros permitidos para pull (valor padrão decidido na revisão humana do threat model).
- **FR-004b**: Comandos e parâmetros DEVEM seguir a retenção temporal definida (proposta: 90 dias), aplicada pela rotina genérica da spec 008.
- **FR-005**: O pull DEVE usar somente as credenciais de registro já configuradas no host pelo operador; o Dokpod não recebe, armazena nem transmite credenciais de registro.
- **FR-006**: O resultado do pull DEVE informar digest resolvido, tamanho e códigos de falha padronizados.
- **FR-007**: A interface DEVE oferecer "Baixar imagem" na Visão geral e na tela Imagens (drawer com referência, seleção de hosts do estágio e aviso de tag mutável) e acompanhar resultados por host.
- **FR-008**: A interface DEVE fornecer à spec 013 consulta de presença/tamanho/data da imagem em um host.
- **FR-009**: A tela Imagens DEVE substituir a página "Em breve" do item de menu.

### Entidades Principais

- **Imagem observada**: host, id, tags, digests, tamanho, criação, arquitetura, containers que a usam.
- **Comando de pull**: comando existente com tipo `pull_image` e parâmetro referência.

## Critérios de Sucesso (obrigatório)

### Resultados Mensuráveis

- **SC-001**: Contagem de imagens sem uso coincide com o engine em 100% dos hosts de teste.
- **SC-002**: Pull de imagem pública de até 200 MB conclui e aparece no inventário em até 2 minutos em rede de laboratório.
- **SC-003**: Nenhuma credencial de registro aparece em respostas, auditoria, eventos ou logs.
- **SC-004**: Replay do mesmo comando de pull não gera novo download nem novo evento duplicado.
- **SC-005**: 100% dos testes negativos do threat model passam (referência maliciosa, registro não permitido, scope ausente, host não autorizado, agente sem capability).

## Premissas

- Verificação de assinatura (cosign/sigstore) citada no mockup fica **fora do escopo**: a ajuda mostra digest, tamanho e data. Adoção futura exige ADR (nova dependência e política de confiança).
- "Publicada em" é a data de criação da imagem informada pelo engine, não a data de publicação no registro.
- Remoção/limpeza (`prune`) de imagens fica fora do escopo nesta spec.
- Scope `image:pull` documentado em `docs/configuracao-keycloak.md`, com os papéis que o recebem definidos na revisão humana.
