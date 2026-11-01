# opentelemetry-proto (cópia parcial)

Cópia sem alterações dos arquivos de logs do [opentelemetry-proto](https://github.com/open-telemetry/opentelemetry-proto),
usados pelo transporte OTLP/HTTP da exportação de auditoria (spec 027, D-005).

| Item | Valor |
| --- | --- |
| Versão | `v1.11.1` |
| Commit | `58232798eae2f0d08ea6682c6885299db08177da` |
| Licença | Apache-2.0 ([LICENSE](LICENSE)), conferida em 2026-11-01 |
| Arquivos | `common/v1/common.proto`, `resource/v1/resource.proto`, `logs/v1/logs.proto`, `collector/logs/v1/logs_service.proto` |

Compilados pelo `Grpc.Tools` em `backend/libs/Dokpod.ControlPlane.Infrastructure` como tipos internos, sem serviço gRPC.
Para atualizar, troque os quatro arquivos pela mesma versão marcada e atualize esta tabela.
