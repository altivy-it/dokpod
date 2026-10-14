# Modelo de Dados: Fundação do Console Operacional

Esta feature não cria entidades persistentes. Os modelos abaixo são de frontend.

## NavigationItem

| Campo | Tipo | Regra |
| --- | --- | --- |
| `id` | string | Único; ex.: `overview`, `containers`, `hosts`. |
| `label` | string | pt-BR, conforme mockup. |
| `icon` | IconName | Deve existir no sprite. |
| `section` | `operation` \| `infrastructure` \| `governance` | Ordem fixa. |
| `route` | string | Rota lazy; item sem feature entregue aponta para `/em-breve/:id`. |
| `requiredScope` | string? | Se informado, item só aparece quando algum ambiente concede o scope. |
| `count` | Signal<number \| null>? | Contador opcional (Alertas). |
| `countLabel` | string? | Rótulo acessível do contador. |

Itens e rotas iniciais:

| Seção | id | Rótulo | Rota | Spec que entrega |
| --- | --- | --- | --- | --- |
| Operação | overview | Visão geral | `/` | 008 |
| Operação | containers | Containers | `/containers` | 010 (legado até lá) |
| Operação | pods | Pods | `/em-breve/pods` | futura |
| Operação | images | Imagens | `/imagens` | 012 |
| Operação | volumes | Volumes | `/em-breve/volumes` | futura |
| Operação | networks | Redes | `/em-breve/redes` | futura |
| Infraestrutura | hosts | Hosts | `/hosts` | 008 (legado de ambientes até lá) |
| Infraestrutura | registries | Registros | `/em-breve/registros` | futura |
| Governança | alerts | Alertas | `/em-breve/alertas` | futura (contador vem de 008) |
| Governança | audit | Auditoria | `/em-breve/auditoria` | futura |
| Governança | settings | Configurações | `/em-breve/configuracoes` | futura |

## Stage

`production` (Produção, ícone shield) · `staging` (Homologação, ícone flag) · `development` (Desenvolvimento, ícone tool).

## SessionView (BFF `/bff/session`, extensão compatível)

| Campo | Tipo | Observação |
| --- | --- | --- |
| `authenticated` | boolean | Existente. |
| `subject` | string? | Existente. |
| `name` | string? | Existente. |
| `displayGroup` | string? | Novo, opcional, de claim configurável. |

## Tokens

Categorias e nomes conforme `docs/design/app/project/ds/altivy/tokens.json` e `altivy-app.css`, com prefixo `--dok-` no Dokpod (ex.: `--dok-surface-base`, `--dok-teal-light`, `--dok-radius-control`, `--dok-chart-1`). Mapeamento completo gerado na tarefa de tokens e conferido por teste.
