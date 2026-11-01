# Validação: Exportação de auditoria para SIEM

Cenários ponta a ponta para comprovar a feature depois da implementação. Tudo roda em containers; o navegador e o cliente HTTP usam a borda nginx (`https://localhost:7443/dokpod/`). Licença sintética dos vetores da spec 031, com e sem a capability `audit-siem-export`.

## Pré-requisitos

- Stack E2E com a imagem Business construída localmente (`Business.Dockerfile`) e uma chave de teste no trust store **só do build local**, nunca no repositório.
- Coletores de teste em containers: `rsyslog` com TLS (porta 6514) e `otel-collector` com receptor OTLP/HTTP e exportador para arquivo; os dois com certificado de uma CA de teste montada em `/run/secrets/dokpod-audit-export/<secretRef>/` na API.
- Usuários do Keycloak com e sem `audit-export:read` e `audit-export:manage`.

## Cenário 1: envio contínuo por syslog (Jornada 1, SC-001)

1. Criar um destino `syslog` com todas as categorias e enviar o evento de teste: o coletor recebe `audit_export.test`.
2. Executar 50 ações no console (reiniciar, parar e iniciar containers).
3. **Esperado**: 95% dos registros chegam em até 10 s, medido entre `occurredAt` e a chegada no coletor; cada `eventId` do coletor existe na trilha local com os mesmos campos.
4. Desmarcar a categoria `containers`: as novas ações de container não chegam e continuam na trilha local.

## Cenário 2: indisponibilidade e lacuna (Jornada 2, SC-002)

1. Parar o coletor, gerar 200 eventos e conferir a tela (estado `failing`, fila 200).
2. Religar o coletor: os 200 chegam, sem duplicar a trilha local.
3. Com relógio injetável nos testes de integração, deixar o pendente mais antigo passar de 72 h: o cursor pula, a trilha ganha `audit_export.gap` e a tela mostra o aviso.

## Cenário 3: OpenTelemetry (Jornada 3)

Repetir o cenário 1 com um destino `otlp`: os registros chegam ao arquivo do coletor com os atributos `dokpod.*` do [contrato](contracts/export-record.md#otlphttp-protobuf-post-path-padrão-v1logs).

## Cenário 4: licença restrita e sem capability (FR-007, FR-008, SC-005)

1. Com a licença válida, criar o destino e gerar eventos: chegam.
2. Avançar o relógio para além da tolerância (restrita): os eventos continuam chegando; criar, editar e testar respondem `403 license_expired`; desativar e excluir funcionam.
3. Importar uma licença sem a capability: criar destino responde `403 license_invalid`; o destino existente continua enviando.
4. Imagem CE: a seção não aparece e as rotas não existem (`404`).

## Cenário 5: autorização e segurança (FR-005, SC-003, D-007)

1. Sem `audit-export:read`: `403 authorization_denied`; sem `audit-export:manage`: criar e testar recusados.
2. Destinos `127.0.0.1`, `169.254.169.254` e um nome que resolve para loopback: `422 audit_export_address_blocked`.
3. Certificado do coletor expirado: nada é enviado, estado `failing` com `tls_error`.
4. Canário: um evento com segredo e conteúdo de terminal na origem chega ao coletor sem esses valores.

## Cenário 6: várias réplicas e lote (SC-004, SC-006)

1. Duas réplicas da API: cada `eventId` chega uma vez por destino; derrubar a réplica com o lease faz a outra assumir em até 30 s, e as repetições têm o mesmo `eventId`.
2. Ação em lote de 100 containers, 30 vezes com a exportação ligada e 30 desligada: o p95 ligado fica no máximo 5% acima do desligado.

## Comandos

```bash
dotnet test backend/tests/Dokpod.ControlPlane.Application.Tests --filter AuditExport
dotnet test backend/tests/Dokpod.ControlPlane.Api.Tests --filter AuditExport          # PostgreSQL real e coletores em containers
dotnet test backend/tests/Dokpod.Architecture.Tests --filter BusinessGate
frontend/tests/e2e/mock/run-mock-e2e.sh -- -k test_027
```
