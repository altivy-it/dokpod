# Pesquisa: Exportação de auditoria para SIEM

**Spec**: [spec.md](spec.md) | **Data**: 2026-11-01 | **Origem**: IA assistida; decisões técnicas sujeitas à revisão humana (lzocateli)

Cada decisão resolve um ponto do contexto técnico do [plano](plan.md). As decisões de produto vêm da sessão de esclarecimentos de 2026-11-01 da spec.

## D-001: Cursor na ordem de confirmação da trilha

- **Decisão**: acrescentar a `audit_events` duas colunas preenchidas pelo banco: `tx_id xid8` (padrão `pg_current_xact_id()`) e `export_seq bigint` (padrão de uma sequência própria). O cursor de cada destino é o par `(last_tx_id, last_seq)`, e a leitura só considera linhas com `tx_id < pg_snapshot_xmin(pg_current_snapshot())`, ordenadas por `(tx_id, export_seq)`.
- **Justificativa**: `audit_events` não tem ordem de confirmação. A chave é `(event_id, occurred_at_utc)`, e transações concorrentes confirmam fora da ordem de `occurred_at_utc`. Um cursor por horário pularia eventos confirmados atrasados e quebraria o "pelo menos uma vez" (FR-003). Abaixo do `xmin` do snapshot nenhuma transação ainda pode confirmar, então nada que ficou para trás do cursor aparece depois.
- **Migração (expand-contract)**: `ADD COLUMN` sem padrão (sem reescrever partições), seguido de `ALTER COLUMN ... SET DEFAULT`, que vale só para linhas novas. As linhas antigas ficam com `NULL` e nunca são exportadas: um destino novo começa no fim da trilha. O índice `(tx_id, export_seq)` fica em cada partição. O gatilho de append-only continua valendo, porque nada é atualizado.
- **Risco aceito**: uma transação longa em qualquer ponto do banco (backup lógico, migration, consulta pesada) segura o `xmin` e atrasa todos os destinos; nada se perde, mas SC-001 pode falhar enquanto ela durar. Mitigação: métrica e tela com o atraso do pendente mais antigo (`now − occurredAt`), e o runbook orienta encontrar a transação em `pg_stat_activity`.
- **Alternativas**: cursor por `occurred_at_utc` com atraso de segurança (perde eventos de transações longas); tabela de outbox ou de fila (cria tabela de crescimento alto, recusada no esclarecimento); `LISTEN/NOTIFY` (não é durável).

## D-002: Janela de 72 horas e lacunas

- **Decisão**: se o próximo evento pendente tiver `occurred_at_utc` anterior a `agora − 72 h`, o remetente avança o cursor até o primeiro evento dentro da janela e grava na trilha `audit_export.gap`, com destino, primeiro e último instante pulados e quantidade. A seção de Configurações mostra um aviso persistente com a lacuna mais recente.
- **Justificativa**: cumpre FR-003 e FR-006 sem fila própria. A lacuna é por instalação, e o motor de alertas da spec 016 trabalha por ambiente ou container; por isso o "alerta" da spec vira evento de auditoria, aviso no console e métrica `dokpod.audit_export.gaps`, sem regra nova da 016.
- **Alternativas**: criar alerta da 016 com `environment_id` fictício (distorce o modelo); descartar sem registro (proibido pela spec).

## D-003: Remetente único por destino entre réplicas

- **Decisão**: lease na própria linha do cursor (`lease_owner`, `lease_until`). Uma réplica assume o destino com `UPDATE ... WHERE lease_until < now()` e renova a cada lote. O lease vence em 30 s.
- **Justificativa**: funciona com pool de conexões, é observável e não prende uma conexão por destino. Numa troca de réplica, o lote não confirmado é reenviado com os mesmos identificadores (SC-006).
- **Alternativas**: `pg_try_advisory_lock` de sessão (prende uma conexão e some sem rastro); eleição de líder global (complexidade sem ganho).

## D-004: Syslog

- **Decisão**: RFC 5424 sobre TLS (RFC 5425), enquadramento por contagem de octetos, implementado com `SslStream` da BCL. `APP-NAME` `dokpod`, `MSGID` igual à ação, `PROCID` `-`, sem `STRUCTURED-DATA` própria. A `MSG` é o registro JSON de [export-record.md](contracts/export-record.md) em UTF-8 com BOM, como a RFC exige.
- **Justificativa**: um SD-ID privado exige número de empresa (PEN) registrado na IANA, que a Altivy ainda não tem. JSON na `MSG` é o que os SIEMs mais aceitam e mantém os campos iguais aos do OTLP.
- **Pendente**: registrar um PEN da Altivy permitiria acrescentar `STRUCTURED-DATA` depois, sem quebrar a `MSG`.
- **Alternativas**: biblioteca de syslog de terceiros (dependência nova sem necessidade); UDP (sem TLS, recusado).

## D-005: OpenTelemetry

- **Decisão**: OTLP/HTTP com protobuf (`POST /v1/logs`), com mensagens geradas a partir dos `.proto` oficiais do `opentelemetry-proto`, versão fixada, Apache-2.0, guardados em `contracts/vendor/opentelemetry-proto/`, compilados pelo `Grpc.Tools` e serializados pelo `Google.Protobuf` que o repositório já usa. Cada evento vira um `LogRecord` com `time_unix_nano`, `severity_text` `INFO` (ou `WARN` para negado e falha), `body` com a ação e atributos `dokpod.*` equivalentes aos campos. O `Resource` leva `service.name` `dokpod` e o `installationId`.
- **Justificativa**: nenhuma dependência nova de pacote; protocolo oficial; resposta síncrona por lote, que é o que confirma o cursor.
- **Alternativas**: `OpenTelemetry.Exporter.OpenTelemetryProtocol` (feito para o pipeline do `ILogger`, sem confirmação por lote nem destino por configuração); OTLP/gRPC (exige HTTP/2 no coletor; pode vir depois); OTLP/JSON (menos eficiente; fora do esclarecimento).

## D-006: Credenciais do destino

- **Decisão**: o destino guarda só `secretRef` (`[a-z0-9-]{1,64}`). Os arquivos ficam em `/run/secrets/dokpod-audit-export/<secretRef>/`: `ca.pem` (opcional; sem ele vale o repositório do sistema), `tls.crt` e `tls.key` (opcionais, para mTLS) e `token` (opcional; vai no cabeçalho `Authorization: Bearer` do OTLP). O caminho é montado a partir do `secretRef` validado e normalizado antes de qualquer acesso (regra invariável de paths). O console e a API nunca devolvem conteúdo, só a presença de cada arquivo.
- **Justificativa**: cumpre a premissa "secret montado" e a regra de segredos fora do console e do banco.
- **Alternativas**: segredo no PostgreSQL cifrado (exige gestão de chave); Keycloak (não é cofre de segredos de terceiros).

## D-007: TLS e destinos permitidos

- **Decisão**: TLS 1.2 ou 1.3, cadeia e nome do host conferidos sempre e sem opção de desligar. O endereço é um nome DNS ou IP com porta explícita. São recusados loopback, link-local (inclusive `169.254.169.254`), `0.0.0.0` e os endereços da rede interna do próprio plano de controle, conferidos também depois da resolução DNS a cada conexão.
- **Justificativa**: quem tem `audit-export:manage` faz a API abrir conexões de saída; sem esse filtro, o destino viraria uma forma de SSRF (fazer a API acessar endereços internos). Redes privadas comuns (10/8, 172.16/12, 192.168/16) continuam permitidas, porque SIEMs costumam estar nelas.
- **Alternativas**: allowlist configurada pelo operador (mais segura, mais trabalho de implantação; pode vir depois); sem filtro (recusado).

## D-008: Licença e composição

- **Decisão**: o módulo só existe na composição Business (`ProductEdition.AddServices`). Os casos de uso de criar, editar, reativar e testar destino são `[BusinessCapability("audit-siem-export")]` e consultam `IBusinessCapabilityGate`. O remetente e as leituras não consultam o gate (FR-008). Quando a licença vigente deixa de trazer a capability, por exemplo num aditivo que a retire, o tratamento é o mesmo da restrição: o envio continua até o administrador desativar o destino.
- **Justificativa**: uma regra só para "não posso mais" (restrita, revogada ou sem a capability) e coerente com "nunca bloquear exportação".
- **Alternativas**: parar o envio quando a capability some (contradiz FR-008 por outro caminho).

## D-009: Ordem, lote e desempenho

- **Decisão**: o envio segue a ordem de confirmação `(tx_id, export_seq)`, em lotes de até 500 eventos ou 1 s, num serviço em segundo plano. O caminho de escrita da trilha não muda além das duas colunas com padrão. A spec pede a ordem de gravação na trilha, que é esta; cada registro leva o `occurredAt` original, e entre transações concorrentes a ordem de envio pode diferir por milissegundos da ordem de `occurredAt`.
- **Justificativa**: SC-001 (95% em 10 s) e SC-004 (p95 do lote de 100 no máximo 5% maior). Nada do envio roda no request. A spec (Jornada 2, cenário 2) pede a ordem de gravação na trilha, que é esta.
- **Alternativas**: reordenar por `occurredAt` antes do envio (exige esperar transações longas; troca latência por uma ordenação que o SIEM já faz).

## D-010: Conteúdo exportado

- **Decisão**: o registro exportado é a mesma projeção que o console e o CSV da spec 017 usam, já sanitizada, acrescida de `category` (de `AuditCategories`), `installationId` e `test` (só no evento de teste). Os filtros por categoria usam o mesmo mapa `AuditCategories`.
- **Justificativa**: FR-002 (mesmos campos e sanitização) com uma fonte só. O canário de SC-003 roda contra o registro exportado.

## D-011: Limites e contagem da fila

- **Decisão**: até 5 destinos por instalação, conferidos na mesma transação que cria o destino, sob lock. O tamanho da fila é contado até 10.000 e mostrado como "10.000+" acima disso. A idade do pendente mais antigo vem do primeiro evento após o cursor.
- **Justificativa**: contar sem limite sobre uma trilha grande custa caro a cada atualização da tela.

## D-012: Eventos da instalação na trilha

- **Decisão**: os eventos `audit_export.*` são da instalação inteira e são gravados com um identificador de ambiente reservado, `AuditEvent.InstallationScopeId`, aceito pelo domínio somente para as ações da categoria `audit_export`. Nenhum ambiente real usa esse identificador, então esses eventos não aparecem na tela de auditoria, que filtra por ambiente autorizado. Eles vão para o SIEM (a categoria `audit_export` é obrigatória, L7), com `environmentId: null` no registro exportado.
- **Justificativa**: a trilha exige ambiente em todo evento e a tela de auditoria da spec 017 é por ambiente. Um identificador reservado mantém a gravação única, sem duplicar o evento no SIEM, e não muda a spec 017. Decisão humana de 2026-11-01 (opção recomendada).
- **Alternativas**: gravar um evento por ambiente existente, como o `audit.exported` faz hoje (visível na tela de auditoria, mas duplicado no SIEM); criar uma permissão de leitura da auditoria da instalação na tela de auditoria (mais completo, mas muda a spec 017).
