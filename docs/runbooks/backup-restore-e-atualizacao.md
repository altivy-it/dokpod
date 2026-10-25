# Runbook: backup, restore, atualização e rollback

Procedimento validado no laboratório local em 2026-10-24 (spec 005 T017): Podman 5.7.0 rootless e PostgreSQL 18.4 compartilhado com o Keycloak, na database `keycloak`, com schemas `dokpod` e `identity`. Ainda não foi repetido em VM isolada.

## Princípios

- As migrations do Dokpod são forward-only. O rollback da aplicação troca imagens e nunca executa `Down`, porque isso apagaria evidência de auditoria (ver [operação da auditoria](operacao-auditoria-postgresql.md)).
- Um rollback só é direto entre versões **sem migration nova**. Se a versão nova trouxer migration, o rollback exige que a versão anterior tolere o schema novo; sem essa garantia, restaure o backup feito antes da atualização.
- Backups contêm dados pessoais, auditoria e o realm do Keycloak. Guarde-os fora do repositório, com acesso restrito, e remova as cópias de exercício.

## Backup

1. Abra uma janela de manutenção e pare a API e o BFF, para que o backup e a verificação vejam o mesmo estado. No laboratório, a parada durou 31 s.
2. Registre a impressão digital da origem (consultas abaixo).
3. Faça o dump completo da database, incluindo o Keycloak, em formato custom:

   ```bash
   podman exec <postgres> sh -c 'pg_dump -U "$POSTGRES_USER" -d keycloak -Fc -Z 6' > keycloak.dump
   ```

   No laboratório, 4,5 GB viraram um dump de 325 MB em 27 s.
4. Volte com a API e depois o BFF.

## Restore verificado

1. Restaure numa database temporária, nunca sobre a origem:

   ```bash
   podman exec <postgres> sh -c 'createdb -U "$POSTGRES_USER" dokpod_restore'
   podman exec -i <postgres> sh -c 'pg_restore -U "$POSTGRES_USER" -d dokpod_restore --exit-on-error' < keycloak.dump
   ```

   No laboratório, o restore levou 80 s.
2. Compare a impressão digital da cópia com a da origem. Todos os itens precisam ser iguais:

   ```sql
   SET TIME ZONE 'UTC';
   SELECT count(*) FROM dokpod.audit_events;
   SELECT encode(sha256(convert_to(string_agg(a::text, E'\n' ORDER BY a.event_id, a.occurred_at_utc), 'UTF8')), 'hex')
     FROM dokpod.audit_events a;
   SELECT count(*) FROM pg_inherits WHERE inhparent = 'dokpod.audit_events'::regclass;
   SELECT count(*), max("MigrationId") FROM dokpod."__EFMigrationsHistory";
   SELECT count(*) FROM pg_trigger WHERE tgname = 'trg_audit_events_append_only';
   ```

   Some a isso a contagem de linhas de cada tabela de topo do schema `dokpod`, os grants de `dokpod_runtime` e os realms e usuários do schema `identity`.
3. Na cópia, `UPDATE` e `DELETE` em `dokpod.audit_events` precisam falhar com `audit events are append-only`.
4. Suba uma API temporária apontando para a cópia (`Database=dokpod_restore`), nas redes do banco e do Keycloak. Ela precisa responder 200 em `/health/ready`, `/health/ready/database` e `/health/ready/keycloak`.
5. Remova a API temporária, a database (`dropdb --force dokpod_restore`), o dump de exercício e qualquer arquivo de ambiente com credenciais.

## Atualização e rollback

A stack tem uma instância de cada serviço, então o console fica indisponível durante a troca. No laboratório, foram 33 s, contando a recriação dos containers e os health checks.

1. Antes de começar, registre a impressão digital da auditoria até o instante da troca, a contagem de ambientes e o fingerprint da identidade de cada agente.
2. Troque as imagens de `api`, `bff` e `web` e recrie os containers. No Podman, remova o BFF antes da API, porque ele depende dela.
3. Verifique, nesta ordem:
   1. containers saudáveis, com as imagens esperadas;
   2. console respondendo 200 pela borda;
   3. cada agente com sessão nova (`session_started_at_utc` posterior à troca) e inventário atualizado;
   4. leituras do console pelo BFF sem erro;
   5. auditoria até a troca com a mesma contagem e o mesmo digest;
   6. ambientes e identidades dos agentes inalterados;
   7. nenhum `fail` ou `crit` no log da API.
4. Para voltar, repita com as imagens da versão anterior.

Resultado em 2026-10-24, N = `dd9a645` e N-1 = `cd674fc` (`main`), sem migration entre as duas:

- **Rollback N → N-1**: stack saudável em 33 s. O agente Podman real (N) reconectou ao servidor N-1 em 36 s. Leituras sem erro, auditoria idêntica (27.200 eventos), 102 ambientes e identidade do agente preservados.
- **Atualização N-1 → N**: mesmo resultado, com stack saudável em 33 s e agente reconectado em 36 s.

## Desligamento da API

A partir de `a86058c`, a API encerra as sessões dos agentes ao receber SIGTERM e termina dentro do prazo de parada. Versões anteriores seguravam o desligamento até o SIGKILL do engine (20 s no compose) quando havia agentes conectados. Em atualizações a partir delas, conte com essa espera.
