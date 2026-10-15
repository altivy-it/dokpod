# Especificação da Feature: Registros

**Branch da feature**: `020-registros`

**Criada em**: 2026-10-15

**Estado**: `approved` - aprovada pelo solicitante em 2026-10-15 ("Siga para implementação").

**Entrada**: Pedido do solicitante em 2026-10-15: "Siga agora para Registros". Item "Registros" da seção Infraestrutura do menu, hoje uma página "Em breve" ("Registros de imagens configurados nos hosts").

**Depende de**: [012](../012-imagens-pull/spec.md) (inventário de imagens, pull, registros permitidos por estágio e credenciais no host), [013](../013-criacao-container/spec.md) (criação respeita os registros permitidos), [016](../016-governanca-alertas/spec.md) (política por estágio editável no console, com auditoria), [017](../017-auditoria/spec.md) (trilha consultável).

**Design**: não há board de Registros em `docs/design/app`. O mockup define o item "Registros" na seção Infraestrutura. A tela segue as telas Imagens, Volumes e Redes: lista, filtros, chips de resumo e detalhe lateral; a política por estágio segue a vista Regras de Alertas (spec 016).

## Esclarecimentos

### Sessão 2026-10-15 (recomendações adotadas, sujeitas à aprovação)

- P: O que é um "registro" na tela? → R: Cada servidor de registro (por exemplo `docker.io`, `quay.io`, `registry.empresa.com:5000`) que aparece em algum host: de onde vieram as imagens locais, configurado no engine (espelho, inseguro, de busca, bloqueado) ou com credencial no agente.
- P: As credenciais aparecem? → R: Nunca. A tela mostra só se o agente do host tem credencial para o registro (sim/não). Usuário, senha e token continuam somente no host (spec 012).
- P: Quem define os registros permitidos? → R: Hoje é uma configuração da API, invisível no console. Passa a ser uma política por estágio editável no console, com auditoria, por quem tem `environment:manage` em todos os hosts do estágio (mesma regra da política de justificativa da spec 016). A configuração atual vira o valor inicial; lista vazia continua significando "qualquer registro".
- P: Mudar a política afeta o que já existe? → R: Não remove nada. Vale para os próximos downloads e criações de container; a tela avisa quantas imagens existentes ficariam fora da lista.
- P: Editar a configuração do engine (`daemon.json`, `registries.conf`) ou as credenciais pelo console? → R: Fora desta entrega. Alteram o host fora do engine e exigem reinício do serviço; o console mostra a configuração e o host continua a fonte (constituição II).
- P: Testar acesso ou login a um registro a partir do host? → R: Fora desta entrega. Faria o agente abrir conexões para endereços escolhidos no console (risco de uso do host como proxy); os erros reais de download (spec 012) já mostram falha de credencial ou de acesso.
- P: Registro inseguro (HTTP ou TLS sem verificação)? → R: Destacado com alerta na lista e no detalhe, sem bloquear nada.

## Cenários do Usuário e Testes (obrigatório)

### Jornada do Usuário 1 - Ver os registros dos hosts (Prioridade: P1)

Um operador abre Infraestrutura → Registros e vê, em uma lista, os registros usados e configurados nos hosts que pode ler.

**Valor desta prioridade**: hoje não há como saber pelo console de quais registros vêm as imagens, quais hosts têm credencial para um registro privado ou quais aceitam registros inseguros.

**Teste independente**: com imagens de mais de um registro, um espelho configurado, um registro inseguro e credencial para um registro privado, abrir a tela e conferir cada registro com seus hosts, origem, quantidade de imagens e sinalizações, comparando com o engine e o arquivo de credenciais do agente.

**Cenários de aceite**:

1. **Dado** hosts autorizados, **Quando** o usuário abre Registros, **Então** vê cada registro uma vez, com a quantidade de hosts, a origem (imagens locais, espelho, inseguro, de busca, bloqueado, credencial no agente), a quantidade de imagens e o tamanho total vindos dele.
2. **Dado** um registro inseguro em algum host, **Quando** listado, **Então** aparece com alerta "inseguro" e o filtro "inseguros" mostra só esses.
3. **Dado** a política do estágio selecionado, **Quando** um registro está fora da lista permitida, **Então** aparece como "fora da política" e o resumo conta quantos estão nessa situação.
4. **Dado** um host com agente sem suporte ao inventário de registros, **Quando** listado, **Então** a tela informa que a configuração daquele host está indisponível, sem esconder os registros vindos das imagens.

---

### Jornada do Usuário 2 - Política de registros permitidos por estágio (Prioridade: P1)

Um administrador vê e edita a lista de registros permitidos para download e criação de containers em cada estágio.

**Valor desta prioridade**: a política hoje só existe na configuração da API, sem visibilidade nem trilha; em Produção ela é a principal barreira contra imagens de origem não aprovada.

**Teste independente**: com permissão de administração em todos os hosts de Produção, incluir e remover registros da lista de Produção, conferir a auditoria, tentar baixar uma imagem de registro fora da lista e ver a recusa; sem a permissão, ver a política somente para leitura.

**Cenários de aceite**:

1. **Dado** a vista de política, **Quando** aberta, **Então** mostra a lista de cada estágio (ou "qualquer registro" quando vazia), quem alterou por último e quando.
2. **Dado** um administrador com `environment:manage` em todos os hosts do estágio, **Quando** inclui ou remove um registro e confirma, **Então** a política passa a valer nos próximos downloads e criações e a alteração fica na auditoria com a lista anterior e a nova.
3. **Dado** a confirmação, **Quando** a nova lista deixa de fora registros de imagens existentes, **Então** a confirmação mostra quantas imagens e hosts ficam fora, sem remover nada.
4. **Dado** um usuário sem a permissão em algum host do estágio, **Quando** abre a política, **Então** vê a lista somente para leitura.
5. **Dado** um registro inválido (com caminho, credencial ou caracteres fora da gramática), **Quando** informado, **Então** é recusado com mensagem clara.

---

### Jornada do Usuário 3 - Investigar um registro (Prioridade: P2)

O operador abre um registro para ver, por host, como ele está configurado e quais imagens vieram dele.

**Valor desta prioridade**: diagnosticar falhas de download ("credencial recusada", "registro não encontrado") e preparar uma mudança de política.

**Teste independente**: abrir o detalhe de um registro privado e conferir, por host, credencial sim/não, espelhos, inseguro e as imagens, com link para a tela Imagens filtrada.

**Cenários de aceite**:

1. **Dado** um registro, **Quando** aberto, **Então** o detalhe mostra, por host: estágio, credencial no agente (sim/não), espelhos, inseguro, bloqueado e a quantidade de imagens, e se o registro é permitido no estágio do host.
2. **Dado** o detalhe, **Quando** o usuário escolhe ver as imagens, **Então** vai para Imagens filtrada pelo registro e pelo host.

---

## Casos de Borda

- Imagem sem registro explícito (`nginx:1.27`): conta como `docker.io` (normalização da spec 012).
- Registro com porta (`registry.local:5000`) ou endereço IP: tratado como registro distinto do mesmo host sem porta.
- Espelho configurado para `docker.io`: listado como registro próprio com a origem "espelho de docker.io".
- Mesmo registro configurado de forma diferente em hosts diferentes (inseguro em um, seguro em outro): o detalhe mostra a diferença por host e a lista sinaliza "inseguro em 1 host".
- Arquivo de credenciais ausente ou ilegível no host: credencial "não" e aviso no detalhe, sem falhar o inventário.
- Política com lista vazia em um estágio: "qualquer registro"; nenhum registro aparece como fora da política nesse estágio.
- Host sem estágio: segue a política "sem estágio" (chave existente da configuração).
- Agente desconectado: configuração mostrada como desatualizada; registros das imagens continuam pela projeção de imagens.

## Requisitos (obrigatório)

### Requisitos Funcionais

**Inventário**

- **FR-001**: O agente DEVE publicar, com capability própria, a configuração de registros do engine (espelhos, registros inseguros, de busca e bloqueados, quando o engine informar) e os nomes dos registros para os quais tem credencial; valores de credenciais NUNCA DEVEM ser lidos para fora do host.
- **FR-002**: O plano de controle DEVE consolidar, por registro, os hosts e as origens (imagens locais a partir da projeção de imagens, configuração do engine, credencial), com quantidade e tamanho das imagens.
- **FR-003**: A API DEVE listar registros dos hosts autorizados (`environment:read`) com filtros (estágio, host, origem, inseguro, fora da política), busca, ordenação e resumo (total, inseguros, fora da política).
- **FR-004**: O detalhe do registro DEVE mostrar a configuração por host e levar à tela Imagens filtrada.

**Política**

- **FR-005**: A política de registros permitidos por estágio DEVE ser persistida, consultável por quem lê algum host e editável por quem tem `environment:manage` em todos os hosts habilitados do estágio; a configuração atual da API DEVE ser o valor inicial.
- **FR-006**: Toda alteração DEVE ser auditada com a lista anterior e a nova, autor e horário, e valer imediatamente para downloads (spec 012) e criações de container (spec 013), sem afetar imagens e containers existentes.
- **FR-007**: A confirmação DEVE mostrar quantas imagens e hosts ficam fora da nova lista; registros DEVEM seguir a gramática de registro da spec 012 (sem caminho, credencial ou esquema).

**Navegação**

- **FR-008**: A tela Registros DEVE substituir a página "Em breve".
- **FR-009**: Hosts sem a capability de inventário de registros DEVEM aparecer como "configuração indisponível", sem esconder os registros vindos das imagens.

## Entidades Principais

- **Registro**: endereço (host e porta opcional), hosts em que aparece e, por host: origens, credencial no agente (sim/não), espelhos, inseguro, bloqueado, quantidade e tamanho das imagens.
- **Política de registros por estágio**: estágio, lista de registros (vazia = qualquer), autor e horário da última alteração.

## Critérios de Sucesso (obrigatório)

### Resultados Mensuráveis

- **SC-001**: Os registros listados e suas origens coincidem com o engine, as imagens locais e o arquivo de credenciais do agente em 100% dos hosts de teste.
- **SC-002**: Nenhum valor de credencial aparece na API, no banco, nos logs ou na tela (varredura por canário).
- **SC-003**: Uma alteração de política vale para o próximo download em até 5 segundos e fica na auditoria com as listas anterior e nova.
- **SC-004**: Com 56 hosts e 200 registros, a primeira página responde em até 2 s no p95.
- **SC-005**: A tela, o detalhe e a edição da política não têm violações sérias ou críticas de acessibilidade e são operáveis só por teclado.

## Premissas

- Docker e Podman informam a configuração de registros pela API do engine (`/info`); o que o engine não informar aparece como "não informado".
- O arquivo de credenciais do agente é o da spec 012 (`DOKPOD_AGENT_REGISTRY_AUTH_FILE`, formato `auths` do Docker); o agente lê só as chaves (nomes de registro).
- A normalização de referências e a gramática de registro são as da spec 012.
- Fora do escopo: editar a configuração do engine ou as credenciais pelo console, testar acesso ou login a registros, listar repositórios de um registro, verificação de assinatura, limpeza de imagens por registro.
