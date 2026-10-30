# Especificação da Funcionalidade: Licenciamento Business offline

**Branch da Funcionalidade**: `031-licenciamento-business-offline`

**Criada em**: 2026-10-29

**Status**: Rascunho

**Entrada**: Descrição fornecida: "Licenciamento Business offline: licença assinada verificada localmente, apuração de nodes equivalentes por faixa de vCPU e CLI administrativa de emissão", com origem no plano privado `architecture-knowledge-base/projects/dokpod/plans/licenciamento-offline-e-cli-administrativa.md` (2026-10-28), aprovado pelo solicitante como entrada desta spec em 2026-10-29. Fase F0 (governança) do plano.

> Redija esta especificação em português brasileiro (pt-BR). Preserve identificadores, nomes de API, comandos e termos técnicos necessários.

**Depende de**: [ADR 2026-0003](../../docs/adr/2026-0003-licenciamento-e-edicoes.md) (`approved`: edições, CE sem chave, Business por arquivo assinado verificado offline), [ADR 2026-0007](../../docs/adr/2026-0007-precificacao-por-node-e-faixa-de-vcpu.md) (`proposed`: nodes equivalentes por faixa de vCPU), [política de licenciamento](../../docs/licenciamento.md) e [constituição](../../.specify/memory/constitution.md).

## Esclarecimentos

### Sessão 2026-10-29 (decisões do solicitante)

- P: O plano de licenciamento, ainda "pendente de revisão", pode servir de entrada? → R: Sim. Ele é insumo desta spec; ajustes de desenho viram esclarecimentos aqui, não edições no plano.
- P: A CE está estável para o gate do ADR 2026-0003 ("a Business só será iniciada após estabilização do MVP CE e revisão jurídica")? → R: Sim. A `v0.1.0`, publicada em 2026-10-29 e implantada em `quality`, é o marco de estabilização da CE. A revisão jurídica continua pendente e é gate antes de código Business (FR-030).
- P: Escopo desta etapa? → R: F0 documental: esta spec, a matriz CE/Business e um ADR `proposed` derivado do `/speckit-plan`. Sem código, sem repositório da CLI, sem aceite de ADR.

### Decisões herdadas do plano (2026-10-28)

- Capacidade em nodes equivalentes por faixa de vCPU, apurada pelo maior total simultâneo em 30 dias.
- Tolerância de 30 dias, declarada na licença assinada, para expiração de licença antes válida e para excesso de capacidade.
- Cotas separadas para a instalação de produção e a de homologação, cuja soma não excede a capacidade contratada.
- Verificador de código próprio, aberto, com licença MIT proposta (sujeita à revisão jurídica).
- Emissão por CLI administrativa em repositório separado do produto, criado só na implementação autorizada.
- Nenhum envio operacional da instalação para o fornecedor.

## Cenários de Usuário e Testes *(obrigatório)*

### História de Usuário 1 - CE continua completa sem licença (Prioridade: P1)

O administrador de uma instalação Community Edition atualiza para uma versão que já contém o mecanismo de licenciamento e continua usando todos os recursos da CE, sem arquivo de licença, sem aviso comercial obrigatório e sem nenhuma comunicação com o fornecedor.

**Motivo da prioridade**: o ADR 2026-0003 proíbe que a Business reduza a CE. Se esta história falhar, nada mais pode ser entregue.

**Teste independente**: instalar a composição CE sem licença e com saída externa bloqueada; executar o baseline da CE (matriz abaixo) e capturar tentativas de rede do licenciamento.

**Cenários de aceitação**:

1. **Dado** uma instalação CE sem licença, **Quando** o operador usa qualquer recurso da coluna CE da matriz, **Então** o recurso funciona como antes da atualização.
2. **Dado** uma instalação CE com saída externa bloqueada, **Quando** a instalação inicia, opera e reinicia, **Então** o licenciamento faz zero tentativas de DNS, TCP, UDP ou HTTP para fora.
3. **Dado** uma instalação CE, **Quando** um administrador abre Configurações, **Então** não há tela, bloqueio ou chamada para importar licença.

---

### História de Usuário 2 - Importar e verificar a licença offline (Prioridade: P1)

O administrador de uma instalação Business recebe do fornecedor um arquivo de licença, importa-o pelo console e vê o estado: organização, plano, vigência, capacidade contratada, cota da instalação e recursos liberados. A verificação acontece inteiramente na instalação.

**Motivo da prioridade**: é o menor recorte que entrega a Business: sem licença verificada, nenhum recurso exclusivo pode ser liberado.

**Teste independente**: importar licenças legítimas e adulteradas numa instalação Business isolada da internet e conferir estado, recusa e auditoria.

**Cenários de aceitação**:

1. **Dado** uma licença legítima e vigente, **Quando** um usuário com permissão de gestão de licença a importa, **Então** o estado mostra plano, vigência, capacidade, cota da instalação e recursos liberados, e a importação fica auditada.
2. **Dado** um arquivo com um único byte alterado, assinado por chave desconhecida ou de outro produto, **Quando** é importado, **Então** é recusado com motivo compreensível e a licença íntegra anterior continua em vigor.
3. **Dado** um usuário autenticado sem permissão de gestão de licença, **Quando** tenta importar ou ler o estado, **Então** a operação é negada pelo provedor de identidade, mesmo com licença válida.
4. **Dado** uma licença nova com revisão menor que a instalada, **Quando** é importada, **Então** é recusada como obsoleta.

---

### História de Usuário 3 - Apuração de nodes equivalentes e cotas por instalação (Prioridade: P2)

O administrador vê quantos nodes equivalentes a instalação consome agora, o pico da janela de 30 dias, a faixa de vCPU da licença e a cota da instalação (produção ou homologação), com hosts de capacidade desconhecida destacados.

**Motivo da prioridade**: a métrica comercial do ADR 2026-0007 depende disso; sem apuração confiável, excesso não pode ser avaliado.

**Teste independente**: com hosts sintéticos de 16, 17, 32, 33 e 64 vCPUs, picos em horários distintos e um host com dois engines, comparar a apuração exibida com o cálculo esperado.

**Cenários de aceitação**:

1. **Dado** faixa de 16 vCPUs e hosts de 16, 17 e 32 vCPUs, **Quando** a apuração roda, **Então** o consumo é 1 + 2 + 2 = 5 nodes equivalentes.
2. **Dado** dois picos de hosts diferentes em momentos diferentes, **Quando** a janela é apurada, **Então** vale o maior total simultâneo, não a soma dos picos isolados.
3. **Dado** um host com Docker e Podman associados, **Quando** a apuração roda, **Então** o host conta uma vez, com a maior capacidade confiável entre os engines.
4. **Dado** um host cuja CPU não pode ser medida de forma confiável, **Quando** a apuração roda, **Então** o host aparece como medição desconhecida, mantendo o último valor confiável, sem contar zero.
5. **Dado** um host desconectado ou removido, **Quando** a janela é apurada, **Então** sua contribuição histórica permanece até sair da janela.

---

### História de Usuário 4 - Expiração e excesso com tolerância, sem afetar workloads (Prioridade: P2)

Quando a licença expira ou a capacidade é excedida, o administrador recebe aviso auditável com o prazo final de tolerância. Durante a tolerância, os recursos Business continuam. Depois dela, só novas execuções de recursos exclusivos Business ficam bloqueadas; containers, dados, leitura, backup, exportação, renovação e toda a CE continuam.

**Motivo da prioridade**: é o enforcement exigido pela política, e o ponto de maior risco para o cliente.

**Teste independente**: com relógio controlado, levar uma instalação a expiração e a excesso, atravessar o prazo de tolerância, reiniciar e reimportar, conferindo prazos e o que continua disponível.

**Cenários de aceitação**:

1. **Dado** uma licença expirada há 10 dias com tolerância de 30, **Quando** o administrador abre o estado, **Então** vê o aviso e a data final da tolerância, e os recursos Business continuam.
2. **Dado** a tolerância encerrada, **Quando** alguém inicia uma nova execução de recurso exclusivo Business, **Então** recebe recusa tipada; operações já aceitas concluem; workloads, leitura, backup, exportação, renovação e a CE seguem intactos.
3. **Dado** um excesso em andamento, **Quando** a API reinicia ou a mesma licença é reimportada, **Então** o prazo de tolerância não recomeça.
4. **Dado** expiração e excesso simultâneos, **Quando** os prazos são calculados, **Então** as tolerâncias não se somam.
5. **Dado** uma renovação que aumenta a capacidade, **Quando** é importada, **Então** o excesso se resolve assim que a janela fica regular, sem reiniciar a instalação.

---

### História de Usuário 5 - Emissão, renovação e aditivo pelo fornecedor (Prioridade: P3)

O operador do fornecedor emite, renova e altera licenças com uma ferramenta de linha de comando própria, separada do produto, com conferência dupla do pedido, pré-visualização sem efeitos e emissão idempotente.

**Motivo da prioridade**: necessária para vender, mas independente do runtime do cliente, que só precisa verificar.

**Teste independente**: emitir, repetir o mesmo pedido, interromper a emissão em cada etapa e emitir em paralelo, conferindo que cada pedido gera um único resultado verificável pela instalação.

**Cenários de aceitação**:

1. **Dado** um pedido aprovado, **Quando** o operador emite, **Então** o arquivo resultante é aceito por uma instalação Business e o registro de emissões guarda pedido, revisão, operador, aprovação e hash.
2. **Dado** o mesmo pedido repetido, **Quando** é emitido de novo, **Então** devolve o mesmo resultado lógico, sem nova revisão.
3. **Dado** a pré-visualização, **Quando** o operador a executa, **Então** nada é assinado, gravado ou numerado.
4. **Dado** um pedido cuja soma das cotas excede a capacidade contratada, **Quando** é emitido, **Então** é recusado antes de assinar.

---

### História de Usuário 6 - Rotação de chave e revogação offline (Prioridade: P3)

O fornecedor troca a chave de assinatura sem invalidar licenças vigentes e, em caso de comprometimento, entrega ao cliente um pacote de revogação assinado que a instalação importa manualmente.

**Motivo da prioridade**: obrigatório antes da primeira venda, mas só depois de emissão e verificação existirem.

**Teste independente**: publicar versão com duas chaves públicas confiáveis, emitir com a nova, importar pacote de revogação e conferir efeito restrito a recursos Business.

**Cenários de aceitação**:

1. **Dado** uma versão do produto que confia na chave antiga e na nova, **Quando** licenças de ambas são importadas, **Então** ambas são aceitas.
2. **Dado** um pacote de revogação autenticado para a licença instalada, **Quando** é importado, **Então** novas execuções exclusivas Business param e o baseline CE, os workloads e os dados continuam.
3. **Dado** um pacote de revogação com sequência menor que a já importada, **Quando** é importado, **Então** é recusado.

### Casos de Borda

- Relógio da instalação volta no tempo ou o banco é restaurado de backup antigo: o estado fica indeterminado para recursos Business, sem conceder tolerância nova; CE e recuperação seguem disponíveis.
- Licença com início no futuro: não libera recursos até a data; não conta como expirada.
- Licença de cota zero para uma instalação: o slot fica desabilitado; zero nunca significa ilimitado.
- Capacidade ilimitada (Enterprise): representada explicitamente na licença, nunca por número zero ou negativo.
- Arquivo enorme, JSON com chave duplicada, campos desconhecidos ou texto inválido: recusado sem esgotar recursos e sem substituir a licença íntegra.
- Dois administradores importam licenças ao mesmo tempo: só uma vence, a outra recebe conflito.
- Uma cópia da mesma licença usada em duas instalações isoladas: não é detectável sem conexão; o risco fica no contrato, sem alegar controle global.
- Agente de versão anterior, sem informação de confiabilidade da CPU: tratamento explícito na matriz de compatibilidade, nunca contagem silenciosa.

## Requisitos *(obrigatório)*

### Requisitos Funcionais

**Preservação da CE**

- **FR-001**: A composição CE DEVE funcionar sem arquivo de licença, sem o verificador em uso e sem infraestrutura do fornecedor, com todos os recursos da coluna CE da matriz.
- **FR-002**: O licenciamento NÃO DEVE iniciar comunicação externa (DNS, TCP, UDP, HTTP) em nenhuma edição, inclusive em erro, expiração e revogação.
- **FR-003**: Nenhum estado de licença (ausente, inválida, expirada, excedida, revogada, indeterminada) DEVE parar containers ou agentes, apagar ou corromper dados, ou bloquear leitura, backup, exportação, renovação e recursos da CE.
- **FR-004**: Recursos já entregues na CE (matriz abaixo) NÃO DEVEM migrar para a Business.

**Licença e verificação**

- **FR-005**: A licença DEVE ser um arquivo assinado digitalmente com algoritmo padronizado de mercado, verificado na instalação apenas com chaves públicas distribuídas com o produto; o arquivo não pode indicar a própria chave ou algoritmo.
- **FR-006**: A licença DEVE conter, no mínimo: versões do formato e da regra de apuração, identificadores opacos de licença e organização, revisão crescente, produto, plano, datas de emissão, início e expiração, capacidade em nodes equivalentes (ou ilimitada de forma explícita), faixa de vCPU, janela de 30 dias, tolerância de 30 dias, cotas por instalação e recursos liberados.
- **FR-007**: A licença NÃO DEVE conter dados pessoais, nomes de host, endereços, números de série, inventário ou preço.
- **FR-008**: A verificação DEVE recusar arquivo adulterado, de outro produto, com formato ou regra de apuração não suportados, com campos inesperados ou duplicados, datas inconsistentes ou acima dos limites de tamanho definidos no plano.
- **FR-009**: A importação DEVE ser atômica e idempotente, com controle de concorrência e revisão monotônica; arquivo recusado nunca substitui a última licença íntegra.
- **FR-010**: A licença persistida DEVE ser reverificada ao ser usada; um resultado de verificação gravado não vale como prova.

**Capacidade**

- **FR-011**: O consumo DEVE ser calculado como a soma, por host vinculado, de `teto(vCPUs do host / faixa)`, e comparado ao maior total simultâneo da janela móvel de 30 dias.
- **FR-012**: Um host físico ou VM com mais de um engine DEVE contar uma vez, por associação administrativa e auditada entre host e ambientes.
- **FR-013**: CPU de origem não confiável DEVE aparecer como medição desconhecida, preservando o último valor confiável; nunca contar como zero nem usar silenciosamente o valor do processo do agente.
- **FR-014**: Hosts desconectados ou suspensos DEVEM continuar contando com a última capacidade conhecida; a remoção encerra a participação futura sem apagar o histórico da janela.
- **FR-015**: A troca de faixa por licença nova DEVE reapurar a janela a partir dos dados brutos.
- **FR-016**: Cada instalação (produção e homologação) DEVE ter cota própria assinada; a emissão DEVE recusar soma de cotas acima da capacidade contratada.
- **FR-017**: Mudanças na contagem de CPU do protocolo agente-servidor DEVEM ser aditivas e compatíveis com a versão anterior do agente (N/N-1).

**Estados e direitos**

- **FR-018**: O estado da licença DEVE separar autenticidade, vigência, apuração e direitos, com motivo legível para cada restrição.
- **FR-019**: A expiração DEVE abrir tolerância até `expiração + dias de tolerância assinados`; o excesso DEVE registrar início e prazo final, que não recomeçam por reinício, reimportação ou alteração local.
- **FR-020**: Irregularidades simultâneas NÃO DEVEM somar prazos de tolerância.
- **FR-021**: Após a tolerância, revogação autenticada ou estado indeterminado, só novas execuções de recursos exclusivos Business DEVEM ser recusadas; operações já aceitas concluem e reconciliam.
- **FR-022**: Licença válida NUNCA DEVE suprir permissão ausente: a ordem é autenticar, autorizar no provedor de identidade, avaliar o direito comercial, persistir a intenção e executar.
- **FR-023**: Ler e gerir a licença DEVEM exigir permissões próprias no provedor de identidade (leitura e gestão de licenciamento), sem papéis locais.
- **FR-024**: Recusas por licença DEVEM ser distinguíveis de falha de autenticação e nunca encerrar a sessão do usuário.

**Auditoria e privacidade**

- **FR-025**: Importação, recusa, mudança de estado, início e fim de tolerância, alteração de associação de host e revogação DEVEM gerar trilha de auditoria global de licenciamento, separada da auditoria por ambiente.
- **FR-026**: Dados de licença e de apuração NÃO DEVEM sair da instalação por observabilidade, exportações, relatórios de erro ou suporte automático.
- **FR-027**: A interface NÃO DEVE exibir o arquivo de licença completo em listas, notificações ou erros.

**Emissão (ferramenta do fornecedor)**

- **FR-028**: A emissão, renovação, aditivo e revogação DEVEM ser feitos por ferramenta de linha de comando em repositório e artefato separados do produto distribuído, sem qualquer componente emissor ou chave privada nas imagens do cliente.
- **FR-029**: A ferramenta DEVE oferecer pré-visualização sem efeitos, emissão idempotente por identificador do pedido, conferência dupla registrada, recuperação após falha em qualquer etapa e registro de emissões sem dados de inventário do cliente.

**Governança**

- **FR-030**: Nenhum código exclusivo Business DEVE ser escrito antes da revisão jurídica de titularidade, contribuições, licença MIT do verificador, composição CE/Business e contrato (ADR 2026-0003).
- **FR-031**: As regras comerciais do ADR 2026-0007 DEVEM ser implementadas somente após seu aceite humano; até lá, a faixa de vCPU, a janela e a tolerância só valem como parâmetros da licença.

### Matriz CE/Business (reconciliação de 2026-10-29)

A matriz da [política de licenciamento](../../docs/licenciamento.md) é anterior às specs 008 a 023. A reconciliação abaixo mantém na CE tudo o que já foi entregue (FR-004). Ela não cria nem reprioriza recursos Business.

| Recurso | CE | Business | Fonte |
| --- | --- | --- | --- |
| Ambientes, hosts, inventário, saúde e sincronização de containers | sim | sim | specs 005, 008 |
| Iniciar, parar, reiniciar, pausar e excluir containers | sim | sim | specs 005, 010 |
| **Ações em lote sobre containers** | **sim** (entregue) | sim | spec 010; a política lista "ações em lote" na Business, corrigir |
| Métricas de recursos de hosts e containers | sim | sim | spec 009 |
| Detalhe, inspeção sanitizada e logs sob demanda | sim | sim | spec 011 |
| Imagens e pull, criação de container | sim | sim | specs 012, 013 |
| Terminal interativo no container | sim | sim | spec 014 |
| Visão geral com KPIs | sim | sim | spec 015 |
| **Governança de alertas e parada justificada** | **sim** (entregue) | sim | spec 016; a política lista "alertas" na Business, corrigir para integrações externas de alerta |
| Auditoria persistente, filtros e exportação CSV | sim | sim | spec 017 |
| Volumes, redes, registros (política de registries) e pods | sim | sim | specs 018 a 021 |
| Configurações (acesso, instalação, parâmetros) | sim | sim | spec 022 |
| Autenticação e autorização por ambiente e ação no Keycloak, mTLS de agentes | sim | sim | ADR 2026-0003 |
| Backup, restauração e atualização manuais | sim | sim | ADR 2026-0003 |
| Correções de segurança | sim | sim, sem atraso deliberado | ADR 2026-0003 |
| Administração de licença (esta spec) | não se aplica | sim | esta spec |
| Aviso de nova versão de imagem | não | sim | spec 025 (`draft`) |
| Janelas de mudança por estágio | não | sim | spec 026 (`draft`) |
| Exportação de auditoria para SIEM e retenção configurável | não | sim | spec 027 (`draft`) |
| Implantação somente por código em produção | não | sim | spec 028 (`draft`) |
| Modelos de SSO prontos | não | sim | spec 029 (`draft`) |
| Permissões por equipe e recurso individual geridas no console; políticas centralizadas; operações agendadas; integrações externas de alerta; GitOps/stacks; backup automático; rollout coordenado de agentes; relatórios; builds de suporte estendido; suporte comercial | não | intenção de produto, sem spec | política de licenciamento |

Recursos da coluna Business só viram direitos da licença (FR-006) quando tiverem spec própria aprovada e implementada. Planos (Teams, Business, Enterprise) diferem por capacidade, faixa e suporte, conforme o ADR 2026-0007; quais recursos cada plano libera é dado da licença, não regra embutida no produto.

### Entidades Principais

- **Licença**: documento assinado com identidade, revisão, plano, vigência, capacidade, faixa, janela, tolerância, cotas por instalação e recursos liberados.
- **Revisão importada**: cada licença ou pacote recebido, com hash, autoria e resultado da verificação; nunca sobrescreve a última íntegra quando recusado.
- **Estado efetivo**: autenticidade, vigência, apuração, direitos, prazos de tolerância e último instante confiável da instalação.
- **Host**: máquina física ou VM com identificador local opaco, associada a um ou mais ambientes Docker/Podman.
- **Observação de capacidade**: vCPUs de um host num instante, com origem e confiabilidade, mantida pelo tempo da janela.
- **Pacote de revogação**: documento assinado de escopo mínimo, com sequência monotônica.
- **Evento de licenciamento**: entrada da trilha global de auditoria.
- **Pedido de emissão** (fornecedor): solicitação aprovada com identificador, contrato opaco, direitos, datas e cotas.
- **Registro de emissões** (fornecedor): histórico de pedidos, revisões, hashes, operadores e aprovações, sem dados de inventário do cliente.

## Critérios de Sucesso *(obrigatório)*

### Resultados Mensuráveis

- **SC-001**: 100% dos recursos da coluna CE da matriz passam nas suítes existentes numa instalação CE sem licença, antes e depois da atualização.
- **SC-002**: Zero tentativas de comunicação externa do licenciamento em captura de rede durante início, importação, erro, renovação, expiração e revogação, nas duas edições.
- **SC-003**: 100% dos arquivos adulterados, de outro produto, obsoletos ou malformados do conjunto de testes são recusados, e a licença íntegra anterior continua em vigor em todos os casos.
- **SC-004**: A apuração exibida coincide com o cálculo esperado em 100% dos cenários de capacidade (faixas 16/17/32/33/64, picos em horários diferentes, host com dois engines, host desconectado, CPU desconhecida).
- **SC-005**: Após o fim da tolerância, zero containers parados, zero perda de dados e 100% das operações de leitura, backup, exportação e renovação disponíveis.
- **SC-006**: Reinício, reimportação e restauração não estendem nenhum prazo de tolerância em 100% dos casos testados.
- **SC-007**: O administrador vê o estado da licença, o consumo e o prazo de tolerância em uma única tela, em até 2 cliques a partir de Configurações.
- **SC-008**: Inspeção das imagens distribuídas ao cliente não encontra emissor nem chave privada em nenhuma camada.
- **SC-009**: Cada pedido de emissão repetido, interrompido ou concorrente resulta em exatamente uma revisão verificável.

## Premissas

- A revisão jurídica (titularidade, contribuições, MIT do verificador, composição CE/Business, contrato) acontece antes de código Business (FR-030) e pode alterar requisitos desta spec.
- O ADR 2026-0007 continua `proposed`; faixa, janela e tolerância entram como parâmetros da licença, e só a regra de cálculo é do produto (FR-031).
- Preço, moeda, tributação, pacote adicional de nodes e meio de cobrança ficam fora desta spec.
- Checkout, servidor de ativação, telemetria comercial, fingerprint de hardware e qualquer kill switch estão fora de escopo e proibidos.
- O desenho técnico (formato da assinatura, limites de tamanho, persistência, API, comandos da CLI, provedor de chave) fica no `/speckit-plan`, a partir das seções correspondentes do plano de origem.
- O repositório da CLI, seu nome, licença, visibilidade e responsáveis são decididos e criados só na implementação autorizada.
- Instalações isoladas não detectam cópia da licença em outra instalação; o contrato trata esse risco.
- A `v0.1.0` é o marco de estabilização da CE (decisão de 2026-10-29); a verificação de que a CE continua completa (História 1) vale para cada versão Business.
