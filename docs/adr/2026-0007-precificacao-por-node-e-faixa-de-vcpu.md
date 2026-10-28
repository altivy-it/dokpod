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
4. Se o plano Teams inclui todos os recursos comerciais ou um subconjunto.
