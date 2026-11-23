# Runbook: janelas de mudança por estágio

Recurso Business da [spec 026](../../specs/026-janelas-de-mudanca/spec.md), capability `change-windows`. Cada estágio
(Produção, Homologação, Desenvolvimento) pode ter uma janela: faixas semanais de horário num fuso IANA em que as ações
que mudam o host são aceitas. Fora delas, a API recusa a ação antes de ela chegar à fila e responde
`409 change_window_closed` com a próxima abertura. Leituras, logs, inspeção, alertas e auditoria nunca são afetados.

A janela é um controle de governança, não de contenção: quem tem acesso direto ao engine, ao host ou ao banco não é
contido por ela ([threat model](../../specs/026-janelas-de-mudanca/threat-model.md)).

## O que fica sujeito à janela

Iniciar, parar, reiniciar, pausar, retomar e remover containers; criar container; baixar imagem; remover volume;
remover rede; as ações de pod; e abrir terminal. Toda ação que enfileira comando passa pela janela por construção (a
fila exige a liberação do guard), então uma ação nova entra sozinha na regra.

Não ficam sujeitos: host sem estágio, estágio sem janela e a edição CE.

## Antes de configurar

- Imagem Business da API ([distribuição](licenciamento-imagem-business.md)) com licença que contenha `change-windows`.
- Para editar a janela de um estágio, `environment:manage` em todos os hosts do estágio que você enxerga (a mesma regra
  da política de justificativa da spec 016). Sem isso, a janela aparece só para leitura.
- Relógio do plano de controle sincronizado por NTP. A decisão usa sempre o relógio da API, nunca o do navegador.

## Configurar a janela

Em **Configurações → Políticas**, cartão **Janelas de mudança**:

1. **Definir janela** no estágio.
2. Informe o fuso IANA (por exemplo `America/Sao_Paulo`) e as faixas: dias, início e fim em `HH:mm`. Fim menor que o
   início cruza a meia-noite e pertence ao dia de início (sexta 22:00–06:00 vale até sábado 06:00). Faixas sobrepostas
   valem como união. Até 14 faixas por estágio.
3. **Salvar**. A mudança grava `change_window.updated` em cada host do estágio, com fuso e faixas anteriores e novos.

Remover a janela grava `change_window.removed` e volta a aceitar as ações a qualquer hora. Dois administradores editando
ao mesmo tempo: o segundo recebe "A janela mudou desde a leitura" (`412`) e a lista é atualizada.

Pela API: `GET /api/v1/change-windows`, `PUT` e `DELETE /api/v1/change-windows/{stage}` com `If-None-Match: *` para criar
e `If-Match` com a versão para editar ou remover ([contrato](../../specs/026-janelas-de-mudanca/contracts/api.md)).

## Exceção de emergência

Fora da janela, quem tem `change:override` no host executa a ação mesmo assim, com justificativa de 10 a 500
caracteres. A exceção vale para aquela ação: a próxima pede de novo. Não existe período de exceção.

Conceda o scope por ambiente, só ao plantão:

```text
tools/scripts/provision-environment-authorization.ps1 -EnvironmentId <id> -GroupPath /dokpod/plantao -Scope change:override
```

Nenhum papel recebe `change:override` por padrão. No console, a opção **Executar fora da janela (exceção de
emergência)** só aparece para quem tem o scope em algum host fora da janela; a API decide de novo em toda tentativa. Cada
exceção grava `change_window.override` na mesma transação do comando, com a justificativa mascarada, e aparece na
auditoria na categoria "Alertas e política". Revise as exceções periodicamente: a justificativa só é conferida pelo tamanho.

Recusas da exceção: `422 justification_required` (sem justificativa válida), `403 change_override_denied` (sem o scope;
auditado como `change_window.denied`) e `503` com o Keycloak indisponível.

## Comandos e sessões em andamento

- A decisão vale no aceite. Um comando aceito dentro da janela executa mesmo que ela feche antes da entrega, até o prazo
  do comando. Com a janela aberta, o prazo pedido é encurtado para o fechamento mais 5 minutos; o comando que não for
  entregue nesse prazo expira.
- O reenvio de um comando já aceito, com o mesmo `Idempotency-Key`, recebe o resultado de duplicado também depois que a
  janela fecha. Exceção: reenviado numa janela seguinte, depois do prazo do comando original, ele pode voltar como
  `command_id_conflict`; gere uma chave nova para repetir a ação.
- A sessão de terminal aberta dentro da janela continua depois do fechamento até os limites da spec 014: 30 minutos por
  padrão (`Terminal:MaxDuration`, no máximo 8 horas).

## Licença restrita

Com a licença vencida além da tolerância, ou sem `change-windows`, as janelas gravadas e a exceção continuam valendo:
a licença não desliga a governança. Só criar e editar janela são recusados (`403 license_*`); remover continua
permitido, para a instalação nunca ficar presa a uma regra que não consegue mudar.

## Troca de edição para a CE

A tabela `change_windows` fica no banco nas duas edições, mas a imagem CE não a lê: trocar a imagem Business pela CE
**desliga as janelas sem aviso**, e as ações voltam a ser aceitas a qualquer hora. Ao voltar para a Business, as janelas
gravadas voltam a valer. Trate a troca de edição como mudança de governança e registre-a fora do Dokpod.

## Fuso e `tzdata`

O fuso é validado e calculado pelo `tzdata` da imagem base da API. Mudanças de regra de um país (horário de verão
criado ou extinto) só valem depois de atualizar a imagem. No buraco da mudança de horário, o início vale o primeiro
instante válido depois dele; num horário repetido, vale a primeira ocorrência no início e a segunda no fim.

## Problemas comuns

| Sintoma | Causa provável | O que fazer |
| --- | --- | --- |
| A seção não aparece em Políticas | Imagem CE, ou licença sem `change-windows` | Confira a edição em Configurações → Instalação e a licença |
| "Somente leitura" na janela | Falta `environment:manage` em algum host do estágio | Conceda o scope no Keycloak ou peça a quem administra todos os hosts |
| Ação recusada na tela, aberta segundo o relógio local | O console mostra o fuso do estágio; a decisão usa o relógio da API | Compare com a próxima abertura mostrada no aviso; verifique o NTP do plano de controle |
| A opção de exceção não aparece | Sem `change:override` no host | Conceda pelo script acima |
| `409 change_window_closed` numa automação | Automação fora do horário | Agende para dentro da janela ou use a exceção com justificativa |
