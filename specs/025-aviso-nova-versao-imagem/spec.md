# Especificação da Feature: Aviso de nova versão de imagem

**Branch da feature**: `025-029-roteiro-recursos`

**Criada em**: 2026-10-27

**Estado**: `draft` - aguardando revisão humana.

**Entrada**: Pedido do solicitante em 2026-10-27: "Itens de referência que ainda não estão no dokpod, inclua todos no roadmap", item 1 de 5: indicador de nova versão das imagens em uso.

**Depende de**: [012](../012-imagens-pull/spec.md) (inventário de imagens e pull), [020](../020-registros/spec.md) (registros, credenciais e política de registros permitidos), [011](../011-detalhe-container-logs/spec.md) (detalhe do container), [016](../016-governanca-alertas/spec.md) (alertas).

## Esclarecimentos

### Sessão 2026-10-27 (recomendações adotadas, sujeitas à aprovação)

- P: O que é "nova versão"? → R: A mesma referência de imagem usada pelo container (por exemplo `nginx:1.28`) apontar, no registro, para um conteúdo diferente do que está no host. Não se sugere tag diferente (por exemplo `1.29`) nesta entrega.
- P: O Dokpod atualiza o container sozinho? → R: Não. O aviso é informativo; baixar a nova versão usa o pull existente (spec 012) e recriar o container fica com o operador. Atualização automática fica fora do escopo.
- P: Quem consulta o registro? → R: O host gerenciado, com as credenciais que já possui, para não levar credenciais ao plano de controle e respeitar a rede de cada host.
- P: Com que frequência? → R: Uma vez a cada 6 horas por imagem em uso, e sob demanda pelo botão "Verificar agora", respeitando a política de registros permitidos.

## Cenários do Usuário e Testes (obrigatório)

### Jornada do Usuário 1 - Ver quais containers rodam imagem desatualizada (Prioridade: P1)

O operador abre Containers ou Imagens e vê, em cada imagem em uso, se existe versão mais nova da mesma referência no registro.

**Valor desta prioridade**: imagens defasadas acumulam correções de segurança não aplicadas; hoje só se descobre comparando digests à mão.

**Teste independente**: publicar nova versão de uma tag em um registro de teste, aguardar a verificação e conferir o aviso na lista de imagens, na lista de containers e no detalhe.

**Cenários de aceite**:

1. **Dado** um container cuja referência de imagem aponta, no registro, para conteúdo diferente do local, **Quando** a verificação termina, **Então** a imagem e o container mostram "Nova versão disponível", com a data da verificação.
2. **Dado** uma imagem igual à do registro, **Quando** verificada, **Então** mostra "Atualizada" com a data da verificação.
3. **Dado** uma imagem referenciada só por digest, construída localmente ou de registro fora da política, **Quando** exibida, **Então** mostra "Não verificável" com o motivo, sem consultar o registro.

---

### Jornada do Usuário 2 - Baixar a nova versão a partir do aviso (Prioridade: P2)

Com permissão de pull, o operador baixa a nova versão direto do aviso.

**Valor desta prioridade**: encurta o caminho entre descobrir e corrigir.

**Teste independente**: com aviso ativo, acionar "Baixar nova versão" e conferir o pull concluído, a auditoria e o aviso atualizado.

**Cenários de aceite**:

1. **Dado** o scope `image:pull` no host, **Quando** o operador aciona "Baixar nova versão", **Então** o pull da spec 012 é executado, auditado, e o aviso passa a indicar que a versão nova está no host e que o container ainda usa a anterior.
2. **Dado** um usuário sem `image:pull`, **Quando** vê o aviso, **Então** a ação não aparece.

---

### Jornada do Usuário 3 - Ser alertado sobre imagens desatualizadas (Prioridade: P3)

O administrador cria regra de alerta para containers em Produção com imagem desatualizada há mais de N dias.

**Valor desta prioridade**: leva o aviso a quem não abre o console todos os dias.

**Teste independente**: criar a regra, simular a defasagem e conferir o alerta aberto e resolvido após a atualização.

**Cenários de aceite**:

1. **Dado** uma regra "imagem desatualizada há mais de 7 dias" em Produção, **Quando** a condição é atingida, **Então** um alerta é aberto pelo fluxo da spec 016.
2. **Dado** o container recriado com a versão nova, **Quando** a verificação seguinte termina, **Então** o alerta é resolvido.

---

## Casos de Borda

- Registro indisponível, limite de requisições atingido ou credencial inválida: o estado vira "Falha na verificação" com o motivo e a última verificação bem-sucedida, sem gerar falso "Atualizada".
- Imagem com várias arquiteturas: a comparação considera a variante da plataforma do host.
- Tag removida do registro: mostra "Referência não encontrada no registro".
- Host desconectado: mantém o último resultado com a data, marcado como desatualizado.

## Requisitos (obrigatório)

### Requisitos Funcionais

- **FR-001**: O sistema DEVE verificar, para cada imagem em uso por container, se a referência aponta no registro para conteúdo diferente do presente no host, a cada 6 horas e sob demanda.
- **FR-002**: A consulta ao registro DEVE partir do host gerenciado, com as credenciais já configuradas nele, e DEVE respeitar a política de registros permitidos da spec 020.
- **FR-003**: As telas Imagens, Containers e o detalhe do container DEVEM exibir o estado da verificação (Nova versão disponível, Atualizada, Não verificável, Falha na verificação) em texto, com a data da verificação.
- **FR-004**: O sistema NÃO DEVE atualizar nem recriar containers automaticamente.
- **FR-005**: A ação "Baixar nova versão" DEVE reutilizar o pull da spec 012, com o scope `image:pull` e a auditoria existentes.
- **FR-006**: O sistema DEVE oferecer condição de alerta "imagem desatualizada há mais de N dias" no fluxo da spec 016.
- **FR-007**: Nenhuma credencial de registro DEVE ser transmitida ao plano de controle, registrada em log ou exibida.

## Entidades Principais

- **Verificação de imagem**: referência, host, conteúdo local, conteúdo no registro, estado, motivo, data.

## Critérios de Sucesso (obrigatório)

### Resultados Mensuráveis

- **SC-001**: Uma nova versão publicada no registro aparece no console em até 6 horas, ou em até 1 minuto com "Verificar agora".
- **SC-002**: Nenhum falso "Atualizada" em 100% dos cenários de falha de registro testados.
- **SC-003**: A varredura por canário não encontra credenciais de registro em respostas, banco ou logs.
- **SC-004**: A verificação de 200 imagens em um host não altera de forma perceptível o tempo de resposta das telas do console.

## Premissas

- Edição prevista: comercial (Business), conforme [licenciamento](../../docs/licenciamento.md#business); a fronteira de edição é revista no plano.
- Docker e Podman anunciam a capability de verificação separadamente (constituição, princípio IV).
- Fora do escopo: sugerir tags mais novas, atualização automática, recriação do container e varredura de vulnerabilidades.
