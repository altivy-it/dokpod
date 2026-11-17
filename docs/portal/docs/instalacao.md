---
title: Instalação
description: Instalar o Dokpod com o instalador guiado em Linux com Docker ou Podman rootless.
sidebar_position: 3
---

O instalador guiado leva um host Linux com Docker Engine ou Podman em modo
**rootless** do zero ao Dokpod em execução. Ele é idempotente: rodar de novo
reconcilia a instalação em vez de recriá-la.

## Antes de começar

- Confira os [requisitos do instalador](requisitos.md#instalador-guiado).
- Baixe o `install.sh` e o `release-manifest.json` da release, ou o pacote
  `--offline`. O `install.sh` confere o digest e a assinatura de cada imagem
  antes de qualquer efeito e para com código 5 quando algo não confere.

## Cenários

| Cenário | Respostas | O que o instalador sobe |
| --- | --- | --- |
| Tudo novo | `postgres.mode: provision`, `keycloak.mode: provision`, `edge.mode: provision` | PostgreSQL, Keycloak, borda nginx, API, BFF, web e o agente local |
| PostgreSQL e/ou nginx existentes | `postgres.mode: existing-create` ou `existing-prepared`; `edge.mode: existing` | o mesmo, sem o banco e/ou sem a borda |
| Keycloak existente | `keycloak.mode: existing` | o mesmo, sem o Keycloak e o banco dele |

As combinações são válidas. O que já é seu (`existing`) nunca é recriado nem
removido.

## Executar

Sem `--answers`, o modo guiado pergunta tudo no terminal. Com um arquivo de
respostas, nada é perguntado, e uma resposta obrigatória faltando encerra com
código 2.

```bash
sh install.sh plan --answers respostas.yaml        # mostra o plano, sem efeito
sh install.sh install --answers respostas.yaml --yes
```

Segredos nunca entram no arquivo de respostas: cada senha é
`{ file: <caminho> }` ou `{ env: <variável> }`. As credenciais administrativas
de um PostgreSQL ou Keycloak existente podem ficar de fora; nesse caso, o
terminal pergunta, e elas são usadas só naquela execução.

`--yes` confirma apenas o plano normal. Migrations, backup, SQL sobreposto e
remoção de dados têm confirmação própria.

## Migrations revisadas por DBA

1. `sh install.sh migrations plan` lista as migrations pendentes, sem tocar no
   banco.
2. `sh install.sh migrations export --out pendentes.sql` gera o SQL exato. O
   DBA revisa e aplica com `psql -v ON_ERROR_STOP=1 -f pendentes.sql`.
3. `install` ou `upgrade` com `--manual-migrations` pausa nessa etapa com
   código 10. Depois da aplicação, o mesmo comando retoma e confere o schema.

## Certificados

`sh install.sh certificates check` lista a validade da CA, da borda, da API e
de cada agente. Perto do vencimento (30 dias por padrão), o instalador sugere a
ação: `recreate pki:<item>` com a CA local, um pacote novo para host remoto, ou
só o aviso para um certificado da organização.

## Desinstalação

O instalador não remove dados por conta própria. Para desinstalar:

1. Faça o [backup](atualizacao-e-backup.md) do banco e do Keycloak.
2. Pare e remova os containers, mantendo os volumes:
   `docker compose -f <installDir>/compose.yaml -p dokpod down`.
3. Só se quiser apagar os dados, remova os volumes do Dokpod.
4. Apague `installDir` e `secretsDir`. O `secretsDir` contém a chave da CA, as
   senhas e as conexões.

PostgreSQL, Keycloak e nginx que já eram seus não são tocados.
