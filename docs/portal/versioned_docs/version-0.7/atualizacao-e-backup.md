---
title: Atualização e backup
description: Backup verificado, atualização e rollback do Dokpod na linha 0.7.
sidebar_position: 5
---

## Princípios

- As migrations do banco são **forward-only**. O rollback troca imagens e
  nunca desfaz migrations, porque isso apagaria evidência de auditoria.
- O rollback só é direto entre versões **sem migration nova**. Caso contrário,
  restaure o backup feito antes da atualização.
- Backups contêm dados pessoais, auditoria e o realm do Keycloak. Guarde-os
  fora do repositório, com acesso restrito.

## Backup

1. Abra uma janela de manutenção e pare a API e o BFF, para que o backup veja
   um estado consistente.
2. Faça o dump completo do banco em formato custom:

   ```bash
   podman exec <postgres> sh -c 'pg_dump -U "$POSTGRES_USER" -d <banco> -Fc -Z 6' > dokpod.dump
   ```

3. Suba de novo a API e, depois, o BFF.

## Restore verificado

1. Restaure numa database temporária, nunca sobre a origem:

   ```bash
   podman exec <postgres> sh -c 'createdb -U "$POSTGRES_USER" dokpod_restore'
   podman exec -i <postgres> sh -c 'pg_restore -U "$POSTGRES_USER" -d dokpod_restore --exit-on-error' < dokpod.dump
   ```

2. Compare a cópia com a origem: contagem e digest da auditoria, histórico de
   migrations e contagem de linhas das tabelas principais.
3. Suba uma API temporária apontando para a cópia. Ela precisa responder 200
   em `/health/ready`.
4. Remova a API temporária, a database de teste e o dump de exercício.

## Atualização e rollback

Com uma instância de cada serviço, o console fica indisponível durante a
troca (cerca de 30 segundos no laboratório).

1. Antes de começar, registre a contagem da auditoria, a contagem de ambientes
   e a identidade de cada agente.
2. Troque as imagens de `api`, `bff` e `web` e recrie os containers. No Podman,
   remova o BFF antes da API.
3. Verifique, nesta ordem:
   1. containers saudáveis, com as imagens esperadas;
   2. console respondendo pela borda;
   3. cada agente reconectado e com inventário atualizado;
   4. auditoria com a mesma contagem de antes;
   5. nenhum erro grave no log da API.
4. Para voltar, repita o procedimento com as imagens da versão anterior.

Atualize sempre o plano de controle antes dos agentes.
