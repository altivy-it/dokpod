# Runbook: exportação da auditoria para SIEM

Recurso Business da [spec 027](../../specs/027-exportacao-auditoria-siem/spec.md), capability `audit-siem-export`. A API
copia a trilha de auditoria local para até 5 destinos do cliente, por syslog (RFC 5424 sobre TLS, RFC 5425) ou
OpenTelemetry (OTLP/HTTP com protobuf). A trilha local continua sendo a fonte de verdade: a exportação nunca apaga,
atrasa nem bloqueia uma ação, e um destino fora do ar não afeta o console.

Formato dos registros: [contrato de exportação](../../specs/027-exportacao-auditoria-siem/contracts/export-record.md).

## Antes de configurar

- Imagem Business da API ([distribuição](licenciamento-imagem-business.md)) com licença que contenha `audit-siem-export`.
- No Keycloak, os scopes `audit-export:read` (ver destinos) e `audit-export:manage` (criar, editar, testar, desativar e
  excluir) no recurso `urn:dokpod:installation`, provisionados por `tools/scripts/configure-keycloak.ps1`. Dê
  `audit-export:manage` só a administradores da instalação: quem o tem escolhe para onde a auditoria vai.
- O SIEM é do cliente, que passa a ser o controlador dos dados exportados. Os registros levam identificador e nome dos
  usuários (os mesmos do console e do CSV), nunca segredos, conteúdo de terminal ou variáveis de ambiente.

## Variáveis da API

| Variável | Padrão | Uso |
| --- | --- | --- |
| `DOKPOD_AUDIT_EXPORT_SECRETS_ROOT` | `/run/secrets/dokpod-audit-export` | Raiz dos secrets montados |
| `DOKPOD_AUDIT_EXPORT_BLOCKED_NETWORKS` | vazio | Sub-redes do plano de controle (CIDR separados por vírgula) que não podem ser destino |
| `DOKPOD_AUDIT_EXPORT_HOSTNAME` | `-` | `HOSTNAME` do cabeçalho syslog |

Loopback, link-local (inclusive `169.254.169.254`), `0.0.0.0` e `::` são sempre recusados. Preencha
`DOKPOD_AUDIT_EXPORT_BLOCKED_NETWORKS` com as redes do PostgreSQL, do Keycloak e dos containers do Dokpod. A política vale
sobre o IP resolvido na hora da conexão, e a conexão vai a esse IP.

## Secret montado

Cada destino aponta para um `secretRef` (`^[a-z0-9-]{1,64}$`), que é uma pasta somente leitura dentro da raiz:

```text
/run/secrets/dokpod-audit-export/<secretRef>/
  ca.pem      CA que assina o certificado do coletor (sem ele, vale o trust store do sistema)
  tls.crt     certificado de cliente para mTLS (opcional, exige tls.key)
  tls.key     chave do certificado de cliente (opcional)
  token       token Bearer do OTLP (opcional; uma linha, só caracteres ASCII visíveis)
```

- Cada arquivo tem no máximo 64 KiB. Symlink para fora da raiz é recusado.
- A API e a tela mostram só quais arquivos existem, nunca o conteúdo.
- Os arquivos são lidos a cada conexão: trocar um arquivo vale no envio seguinte, sem reiniciar a API.

Exemplo de montagem no Compose, comentado em `deploy/e2e/docker-compose-dokpod.yaml`:

```yaml
volumes:
  - /caminho/externo/dokpod-audit-export:/run/secrets/dokpod-audit-export:ro
```

Guarde os arquivos fora do repositório, como qualquer outro secret do Dokpod.

## Criar um destino

Em **Configurações → Exportação para SIEM**:

1. **Novo destino**: nome, protocolo, host, porta (6514 para syslog, 4318 para OTLP), caminho (só OTLP, padrão
   `/v1/logs`), `secretRef` e categorias. A categoria `audit_export` é sempre enviada e não pode ser desmarcada: o SOC vê
   criação, mudança e exclusão de destinos e as lacunas.
2. **Enviar evento de teste**: manda `audit_export.test` (com `test: true`) e mostra o resultado. São no máximo 5 testes
   por minuto por instalação, todos auditados.

O destino novo começa no fim da trilha: eventos anteriores à criação não são enviados. Mudar as categorias também não
reenvia o passado.

## Estados

| Estado | Significado | O que fazer |
| --- | --- | --- |
| `ok` | Em dia | Nada |
| `catching_up` | Mais de 500 eventos pendentes, enviando | Acompanhe a fila; normal depois de uma volta do destino |
| `failing` | O último envio falhou; o código está na tela | Veja a tabela abaixo; o remetente tenta de novo com espera de 1, 2, 4 … até 60 s |
| `gap` | Houve lacuna nas últimas 24 h | Veja [Lacuna](#lacuna) |
| `disabled` | Desativado | Nada é enviado e o cursor fica parado |

| Código | Causa provável |
| --- | --- |
| `tls_error` | Cadeia não confiável, nome que não confere ou certificado expirado (do coletor ou do cliente) |
| `connection_refused` | Porta fechada, nome que não resolve, coletor fora do ar, ou HTTP 429/5xx no OTLP |
| `timeout` | Sem resposta em 10 s (conexão) ou 30 s (envio), ou HTTP 408/504 |
| `rejected` | O coletor recusou o lote (HTTP 3xx ou 4xx no OTLP); redirecionamentos não são seguidos |
| `secret_missing` | Pasta ou arquivo ausente, PEM inválido, par de cliente incompleto ou token inválido |
| `address_blocked` | O nome passou a resolver para um endereço proibido |

O envio é pelo menos uma vez: uma falha depois de o coletor receber, ou a troca de réplica, repete eventos com o mesmo
`eventId`. Deduplique no SIEM por `eventId`.

## Fila e atraso

A tela mostra os pendentes (contados até 10.000, depois "10.000+") e a idade do mais antigo. Só saem eventos de
transações já confirmadas, na ordem de gravação: uma transação longa no PostgreSQL segura todos os destinos até
terminar, sem perder nada. Se o atraso crescer sem erro no destino, procure transações abertas:

```sql
SELECT pid, now() - xact_start AS idade, state, left(query, 80)
FROM pg_stat_activity
WHERE xact_start IS NOT NULL
ORDER BY xact_start
LIMIT 5;
```

Com várias réplicas da API, cada destino é enviado por uma réplica de cada vez (lease de 30 s). Se a réplica cair, outra
assume em até 30 s.

## Lacuna

Os eventos pendentes há mais de 72 h são pulados: o cursor avança, a trilha recebe `audit_export.gap` com o intervalo e a
quantidade, e a tela mostra o aviso por 24 h. Os eventos pulados continuam na trilha local. Para completar o SIEM,
exporte o intervalo do evento pelo CSV do console de auditoria (spec 017) e importe no SIEM.

## Troca de certificado

1. Coletor com certificado novo da mesma CA: nada a fazer.
2. CA nova: coloque em `ca.pem` as duas CAs (antiga e nova, concatenadas), troque o certificado do coletor e depois tire
   a antiga.
3. mTLS: troque `tls.crt` e `tls.key` juntos; o próximo envio usa o par novo.
4. Use **Enviar evento de teste** para confirmar.

## Licença restrita ou sem a capability

- Licença vencida além da tolerância (restrita) ou sem `audit-siem-export`: os destinos ativos **continuam enviando**.
- Criar, editar, reativar e testar respondem `403` (`license_expired` ou `license_invalid`).
- Desativar e excluir sempre funcionam, para que o cliente possa parar a exportação sem depender da licença.
- Na imagem CE a seção não aparece e as rotas não existem (`404`).

## Métricas

Meter `Dokpod.ControlPlane`, por `destination.id` (nunca host nem secret). No Prometheus:

| Métrica | Significado |
| --- | --- |
| `dokpod_audit_export_pending` | Eventos pendentes (até 10.001) |
| `dokpod_audit_export_lag_seconds` | Idade do pendente mais antigo |
| `dokpod_audit_export_failures_total{error_code}` | Envios recusados ou sem resposta |
| `dokpod_audit_export_gaps_total` | Lacunas |

O log da API registra só o tipo da exceção de um ciclo que falhou (`Audit export cycle failed: <tipo>`), sem host,
resposta do coletor ou conteúdo do secret.

## Validar com coletores reais

`tools/scripts/test-audit-export-collectors.ps1 -Build` sobe a imagem Business com rsyslog TLS e otel-collector em
containers e mede o atraso de 50 ações (SC-001). Veja `--help`.
