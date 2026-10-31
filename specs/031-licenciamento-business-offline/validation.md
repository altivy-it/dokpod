# Validação da spec 031: evidências do quickstart (T065)

Origem: IA assistida. Revisor humano: pendente (GO de F7). Data: 2026-10-31, branch `feat/031-acabamento`.
Ambiente: tudo em containers Podman (SDK `lzocateli/dotnet-sdk:10.0.402-noble-v1`, `lzocateli/angular-cli:22.2.4-node24.22.1-bookworm-v1`,
PostgreSQL 17.6). Nenhuma imagem foi publicada.

| Cenário | Evidência | Resultado |
| --- | --- | --- |
| 1. CE intacta sem licença | `test-licensing-no-egress.ps1` com a imagem CE (job `Licensing CE` do CI, PRs #87 a #91); mock E2E `-k "031 or 022"` (8 testes) | PASS |
| 2. Importação e recusa | `Dokpod.Licensing.Tests` (79); `Dokpod.ControlPlane.Api.Tests` com PostgreSQL real (232) | PASS |
| 3. Apuração | `Dokpod.ControlPlane.Application.Tests` (211), incluindo `CapacityMetering`; testes de ledger na API | PASS (T067 com Podman real validado na US3) |
| 4. Tolerância e restrição | `LicenseStateEvaluator`, `ExpiryWarning`, `CapacityDailySummary`, `GracePersistence`, `BusinessCapabilityGate`, `TrustedInstantReset` | PASS |
| 5. Emissão | `dokpod-license-admin`: 25 testes com PostgreSQL real em três execuções (pré-visualização, idempotência, conflito, cotas, aprovação, relatório, interrupção em 7 etapas, concorrência, renovação, revogação, vetores do pacote). Ponta a ponta com o executável: chave `openssl` em PKCS#8 cifrado, passphrase por descritor, relatório gerado em Python, emissão e repetição. `CliIssuedLicenseTests` importou o arquivo na composição Business (aceito) e recusou com chave pública errada (`license_invalid`) | PASS (2026-10-31, US5) |
| 6. Rotação e revogação | `RevocationVerifier` e `RevocationEndpoints` | PASS |
| 7. Artefatos do cliente | `test-licensing-image-contents.ps1 -Build`: CE `community` sem trust store, Business `business` sem `kid` de teste, sem chave privada nem emissor; refutado com PEM fictício (falhou como esperado). `test-licensing-no-egress.ps1` com a imagem Business: 0 pacotes externos, 0 DNS inesperado, canário ausente. `BusinessGateTests` (12 testes de arquitetura) | PASS |
| 8. Relatório de apuração | `LicensingReportVerifierTests` (vetor de referência, íntegro, 6 adulterações, continuidade, `mismatch`, `stale`, `gaps`, recuo de relógio) e `LicensingReportTests` com PostgreSQL real (exportação, privacidade, continuação, adulteração) | PASS |

Verificações transversais:

- build das duas edições (`dotnet build` e `-p:DokpodEdition=Business`): 0 erros;
- `dotnet format --verify-no-changes` nos arquivos alterados: limpo (há débito de formatação anterior em `Dokpod.Agent.Application`, fora do escopo);
- frontend: 34 arquivos, 187 testes;
- OpenAPI: `openapi-spec-validator` 0.7.1 OK; cliente regenerado;
- Gitleaks 8.30.1 nos arquivos alterados: nenhum achado.

Pendências para o GO: publicação do pacote `Dokpod.Licensing` (workflow `licensing-package.yml`) e o CI da CLI depende dela, canal de publicação da imagem Business (U4, T061 `publish.yml`) e revisão humana
de `threat-model.md#revisão-de-segurança-e-privacidade-da-implementação-t064`.
