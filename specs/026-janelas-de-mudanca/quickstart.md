# Validação: Janelas de mudança por estágio

Cenários ponta a ponta para comprovar a feature depois da implementação. Tudo roda em containers; o navegador e o cliente HTTP usam a borda nginx (`https://localhost:7443/dokpod/`). Licença sintética dos vetores da spec 031, com e sem a capability `change-windows`.

## Pré-requisitos

- Stack E2E com a imagem Business construída localmente (`Business.Dockerfile`) e a chave de teste no trust store **só do build local**, nunca no repositório.
- Um agente real em Produção e outro em Homologação, cada um com containers de teste.
- Usuários do Keycloak:
  - `admin-026`, com `environment:manage` em todos os hosts de Produção;
  - `operador-026`, com as ações de container e sem `change:override`;
  - `plantao-026`, com as ações de container e com `change:override` só no host de Produção.
- Testes de unidade e de integração com `TimeProvider` falso; os cenários manuais usam uma janela montada em torno do horário atual.

## Cenário 1: definir a janela (Jornada 1)

1. Como `admin-026`, criar em Configurações → Políticas a janela de Produção de segunda a sexta, das 22:00 às 06:00, em `America/Sao_Paulo`.
2. **Esperado**: a vista mostra as faixas, o fuso, o autor e o horário; a auditoria tem um `change_window.updated` para cada host de Produção.
3. Como `operador-026`, abrir a mesma vista: a janela aparece só para leitura.

## Cenário 2: recusa fora da janela (Jornada 2, SC-001, SC-004)

1. Montar a janela de Produção para fechar agora, num horário que exclui o atual.
2. No console, conferir que os botões das ações controladas do host de Produção mostram "Fora da janela de mudança" e a próxima abertura.
3. Pela API, tentar parar, reiniciar, criar container, baixar imagem, remover volume, remover rede, executar uma ação de pod e abrir terminal.
4. **Esperado**: todas as tentativas respondem `409 change_window_closed` com `nextWindowStartsAt`; nenhum comando chega ao agente (o log do agente e a tabela `agent_commands` ficam sem entradas novas); cada tentativa gera `change_window.denied`.
5. Logs, inspeção, alertas e auditoria do mesmo host continuam respondendo `200` (SC-003).
6. Num lote com containers dos dois hosts, só os itens de Produção são recusados, cada um com o motivo.

## Cenário 3: exceção de emergência (Jornada 3, SC-002)

1. Como `operador-026`, confirmar que a exceção não é oferecida e que o envio de `changeWindowOverride` dá `403 change_override_denied`.
2. Como `plantao-026`, parar um container de Produção com a exceção e uma justificativa.
3. **Esperado**: o container para; a auditoria tem `container.stop` e `change_window.override` com o mesmo `commandId`, autor e justificativa mascarada.
4. Parar outro container: a tela pede uma nova exceção, porque não existe período de exceção.
5. Lote com exceção num host com o scope e em outro sem: o primeiro executa e é auditado; o segundo é recusado com `change_override_denied`.

## Cenário 4: licença restrita e sem capability (FR-007)

1. Importar a licença vencida além da tolerância.
2. **Esperado**: a recusa do cenário 2 e a exceção do cenário 3 continuam iguais; editar a janela dá `403 license_*` e a tela explica a restrição; remover a janela dá `204`.
3. Repetir com uma licença válida sem `change-windows`: o mesmo resultado.

## Cenário 5: CE

Com a imagem CE e o mesmo banco: as rotas `/api/v1/change-windows` não existem (`404`), `changeWindow.state` é `none` e as ações passam. É o efeito de troca de edição registrado no runbook.

## Testes automatizados de referência

- `ChangeWindowCalendarTests`: faixa que cruza a meia-noite; fins de semana; faixas sobrepostas e contíguas; entrada e saída do horário de verão em `America/Sao_Paulo` (histórico) e `Europe/Lisbon`; próxima abertura além do fim de semana.
- `ChangeWindowGuardTests` (aplicação): cada `AgentCommandKind` é controlado; exceção com e sem scope; Keycloak indisponível nega.
- `ChangeWindowGuardTests` (arquitetura): todo caminho que muda o host depende do guard.
- Integração com PostgreSQL real: migration `Up`/`Down`, `If-Match`, exceção gravada na mesma transação do comando.
- Playwright pela borda nginx: aviso nos botões, confirmação da exceção, vista Políticas só para leitura.
