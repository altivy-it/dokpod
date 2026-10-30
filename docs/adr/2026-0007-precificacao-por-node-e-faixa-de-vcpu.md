# ADR 2026-0007: Precificação por node com faixa de vCPU

- **Status:** proposed
- **Data:** 2026-10-27
- **Origem:** IA assistida, a pedido do solicitante
- **Revisor humano:** zocatel (solicitante e responsável pelo produto)
- **Revisa:** a seção de preço e de métricas comerciais do [ADR 2026-0003](2026-0003-licenciamento-e-edicoes.md); licença, edições e regras de enforcement daquele ADR permanecem.
- **Decisões do solicitante (2026-10-27):** três planos pagos, de US$ 900/ano, US$ 1.500/ano e sob medida; o plano de entrada se chama **Teams**, sem tradução em nenhum idioma. Quantidades de nodes, faixas de vCPU e regra de contagem abaixo são proposta e aguardam aceite.

## Contexto

O ADR 2026-0003 previu uma única oferta Business por US$ 99/mês ou US$ 990/ano por organização, até 100 nodes, e definiu que "a quantidade de containers, CPUs e usuários não altera a contagem". Cobrar só por node permite arbitragem: um host de 4 vCPUs e um de 96 vCPUs custam o mesmo, então quem consolida em poucos hosts grandes paga pouco e quem tem muitos hosts pequenos (filiais, edge) paga caro. O site da Altivy publica os novos planos como preços de lançamento previstos.

## Decisão proposta

1. **Planos anuais**, todos com os recursos da edição comercial e a Community Edition gratuita mantida:

   | Plano | Preço | Nodes | vCPUs por node | Suporte |
   | --- | --- | --- | --- | --- |
   | Teams | US$ 900/ano | até 5 | até 16 | comunidade e documentação; uma licença Teams por organização |
   | Business | US$ 1.500/ano | 5, expansível em pacotes de 5 até 25 | até 32 | e-mail em horário comercial, resposta no próximo dia útil; onboarding remoto |
   | Enterprise | sob medida | sem teto | acima de 32 | SLA e contrato personalizados, nodes de produção e não produção, builds com suporte estendido |

2. **Capacidade, não uso.** A métrica é a quantidade de vCPUs lógicos do host gerenciado, nunca o consumo de CPU. Uso exigiria telemetria contínua e faria a fatura oscilar com a carga.
3. **Nodes equivalentes.** Cada node conta `teto(vCPUs / faixa do plano)`. O total é a soma dos nodes equivalentes dos ambientes aprovados. Um host com mais de um engine conta uma vez.
4. **Medição local.** O agente já informa `cpu_count` no `Hello` (`contracts/agent/v1/agent.proto`, campo 10). A apuração usa o maior total dos últimos 30 dias, calculado no plano de controle do cliente contra a licença assinada e verificada offline; nada é enviado ao fornecedor.
5. **Enforcement inalterado.** Excedente gera aviso auditável, tolerância contratual e ajuste na renovação ou por aditivo. Nunca para containers, apaga dados ou bloqueia leitura, backup, exportação e funcionalidades da CE (ADR 2026-0003).

## Alternativas consideradas

- Node sem faixa (ADR 2026-0003): arbitragem por consolidação.
- Preço linear por vCPU: conta imprevisível e atrito a cada redimensionamento.
- Uso de CPU: exige telemetria e conflita com a promessa self-hosted.
- Por container: métrica volátil, incentiva empacotar tudo em poucos containers.
- Por usuário: penaliza adoção e a auditoria nominal.

## Consequências

- `docs/licenciamento.md` passa a descrever os três planos e a métrica de nodes equivalentes, marcada como proposta até o aceite deste ADR.
- O payload da licença comercial precisa de faixa de vCPU por plano, além do limite de nodes.
- A tela Configurações precisará mostrar nodes equivalentes apurados e a faixa da licença, sem enviar dados ao fornecedor.

## Pendências antes do aceite

1. Preço do pacote adicional de 5 nodes no Business.
2. Tolerância de excedente e regra de arredondamento no contrato.
3. Moeda, meio de cobrança e tributação no Brasil.
4. Se o plano Teams inclui todos os recursos comerciais ou um subconjunto. **Proposta abaixo (2026-10-29), aguardando decisão.**

## Proposta para a pendência 4: recursos do plano Teams

- **Data:** 2026-10-29
- **Origem:** IA assistida, a pedido do solicitante ("faça a proposta formal para a pendência 4")
- **Estado:** proposta; não altera o status deste ADR nem constitui aceite

### Contexto

A CE já entrega, sem custo, todo o baseline operacional (ambientes, ciclo de vida e ações em lote, métricas, logs, terminal, imagens, volumes, redes, registries, pods, governança de alertas, auditoria com CSV e autorização no Keycloak). Pelo ADR 2026-0003 nada disso pode migrar para a Business; a reconciliação está na [spec 031](../../specs/031-licenciamento-business-offline/spec.md#matriz-cebusiness-reconciliação-de-2026-10-29). O valor pago vem de três fontes: recursos de governança e conformidade (specs 025 a 029 e roteiro), suporte, e a própria licença comercial, que dispensa as obrigações da `AGPL-3.0-only` para organizações cuja política interna a proíbe.

O público do Teams (até 5 nodes de até 16 vCPUs, sem suporte comercial) é o mesmo que a CE já atende bem. Um Teams com subconjunto de recursos ficaria com pouca razão para existir.

### Opções

| Opção | Teams recebe | Vantagens | Desvantagens |
| --- | --- | --- | --- |
| A. Todos os recursos comerciais | as mesmas capabilities do Business | proposta simples ("Business para times pequenos"); uma única allowlist de capabilities; o upgrade compra porte e suporte, não funcionalidade; nenhuma tabela de recursos por plano para manter | menor diferenciação entre Teams e Business; risco de canibalização pelo Teams contido pelos limites de 5 nodes e 16 vCPUs |
| B. Subconjunto (sem conformidade: SIEM, retenção, implantação só por código) | governança operacional, sem os recursos de auditoria externa | diferenciação maior; empurra quem tem exigência regulatória para o Business | Teams fica pouco atraente frente à CE; matriz recurso × plano para comunicar, testar e emitir; o cliente pequeno regulado (clínica, escritório, fintech inicial) é justamente quem precisa de SIEM |
| A′. Todos os de governança e conformidade; escala de frota só no Business | specs 025 a 029; sem rollout coordenado nem operações agendadas em lote | mantém o Teams atraente para o cliente pequeno regulado; dá ao Business diferencial funcional que acompanha o crescimento | uma linha a mais na comunicação; exige classificar cada capability nova como governança ou escala |
| C. Teams só como licença comercial, sem recursos exclusivos | apenas a licença não AGPL | atende quem só precisa sair da AGPL | confunde o produto; o preço passa a parecer taxa jurídica |

### Proposta

**Opção A com reserva de escala (A′).** O Teams inclui todas as capabilities comerciais de governança e conformidade disponíveis na versão. Ficam reservadas ao Business e à Enterprise apenas as capabilities de escala de frota, que só fazem sentido com muitos hosts. Os planos diferem em:

| Eixo | Teams | Business | Enterprise |
| --- | --- | --- | --- |
| Capabilities de governança e conformidade (specs 025 a 029: aviso de versão, janelas de mudança, SIEM e retenção, implantação só por código, SSO) | todas | todas | todas |
| Capabilities de escala de frota (rollout coordenado de agentes, operações agendadas em lote sobre vários hosts) | não | sim | sim |
| Builds de suporte estendido | não | não | sim |
| Nodes | até 5 | 5, em pacotes de 5, até 25 | sem teto |
| Faixa de vCPU por node | 16 | 32 | acima de 32 |
| Instalações | 1 produção + 1 homologação | 1 + 1 | contrato |
| Suporte | comunidade e documentação | e-mail, próximo dia útil, onboarding | SLA contratual |

A variante A′ preserva o que torna o Teams atraente (o cliente pequeno regulado recebe SIEM, retenção e controle de mudança) e dá ao Business um diferencial funcional natural: recursos de frota, sem valor real com até 5 nodes, passam a acompanhar o crescimento do cliente. As capabilities de escala ainda não têm spec; a classificação vale quando forem especificadas.

### Gatilhos de migração entre planos

Com as specs 025 a 029 implementadas:

| Migração | Gatilho |
| --- | --- |
| CE → Teams ou Business | recursos de governança e conformidade; licença comercial para quem não pode usar AGPL; suporte |
| Teams → Business | crescimento acima de 5 nodes equivalentes na faixa de 16 vCPUs; custo por vCPU (abaixo); suporte por e-mail e onboarding; recursos de escala de frota (A′) |
| Business → Enterprise | mais de 25 nodes ou mais de 32 vCPUs por node; SLA contratual; builds de suporte estendido |

Um cliente Teams com poucos hosts pequenos e sem necessidade de suporte não tem motivo para migrar. É intencional: ele paga pelo porte que usa, e a migração acompanha o crescimento.

### Custo por vCPU

A faixa limita a capacidade efetiva de cada plano, porque cada node conta `teto(vCPUs / faixa)`:

| Plano | Capacidade incluída | Preço anual | Custo por vCPU por ano |
| --- | --- | --- | --- |
| Teams | 5 nodes equivalentes × 16 vCPUs = 80 vCPUs | US$ 900 | US$ 900 / 80 ≈ **US$ 11,25** |
| Business | 5 nodes equivalentes × 32 vCPUs = 160 vCPUs | US$ 1.500 | US$ 1.500 / 160 ≈ **US$ 9,38** |

Exemplos: um host de 32 vCPUs consome 2 dos 5 nodes equivalentes do Teams, mas só 1 no Business; três hosts de 32 vCPUs (6 nodes equivalentes na faixa 16) já não cabem no Teams e cabem no Business. Quem usa hosts maiores que 16 vCPUs tende a achar o Business mais vantajoso antes de atingir o limite do Teams. Acima de 5 nodes no Business, o custo por vCPU depende do preço do pacote adicional (pendência 1), ainda não definido; a conta acima vale para a capacidade incluída.

Consequências para o produto:

- Cada licença carrega a allowlist de capabilities do plano contratado; na A′, a do Teams omite as capabilities de escala de frota. O plano não é regra embutida no produto (spec 031, FR-006), então a variante não muda o mecanismo.
- A Enterprise continua sendo a única com builds de suporte estendido, porque isso é custo de manutenção, não funcionalidade.
- A comunicação comercial passa a ser: "CE para operar; Teams para governar; Business para governar uma frota, com suporte; Enterprise para escala e contrato".

### Recomendação complementar: primeira capability comercial

Enquanto nenhuma das specs 025 a 029 estiver implementada, a edição comercial vende só suporte e licença. Propõe-se priorizar a capability que responde a uma exigência de auditoria, não de conveniência:

1. **[027 - Exportação de auditoria para SIEM](../../specs/027-exportacao-auditoria-siem/spec.md)**, com retenção configurável: independente das outras, atende auditoria de segurança (ISO 27001, SOC 2, setor financeiro e saúde) e não tem equivalente na CE.
2. **[026 - Janelas de mudança](../../specs/026-janelas-de-mudanca/spec.md) seguida da [028 - Implantação somente por código](../../specs/028-implantacao-somente-por-codigo/spec.md)**: controle de mudança em produção, a dupla com maior apelo para processos ITIL.
3. 029 (SSO) e 025 (aviso de versão), de menor esforço e menor peso na decisão de compra.

Todas dependem do mecanismo de direitos da spec 031 (fase F5) e de aprovação da respectiva spec.

### Como falsificar a proposta

Revisar a variante A′ se, após o lançamento, a maioria das contratações Teams vier de organizações que poderiam pagar o Business pelo porte (sinal de canibalização), ou se clientes recusarem o Business por considerar o Teams equivalente apesar da diferença de suporte. Revisar a reserva de escala se clientes Teams com até 5 nodes pedirem esses recursos com frequência (sinal de que não são só de frota).
