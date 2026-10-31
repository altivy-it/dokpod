# Dokpod.Licensing

Verificador offline da licença Business do Dokpod (spec 031), sob licença MIT e dependente só da BCL do .NET 10.

- `LicenseVerifier.Verify`: licença `dokpod-license+jws` (ES256, perfil fechado) contra um `TrustStore`;
- `RevocationVerifier.Verify`: pacote `dokpod-revocation+jws`;
- `LicensingReportVerifier.Verify`: relatório de apuração encadeado por hashes, com resultado
  `ok | tampered | discontinuous | mismatch | stale`, mais `gaps` e `clockRegressions`.

Nada aqui faz I/O, rede ou leitura de relógio: o instante e o trust store são sempre do chamador.

## Vetores de conformidade

A pasta `conformance/` do pacote traz casos estáticos com o resultado esperado em `manifest.json`:

- `conformance/license/`: licenças e revogação assinadas por uma chave sintética (`dokpod-test-1.pem`, só a parte
  pública); a chave privada foi descartada na geração e esse `kid` nunca entra no trust store de produção;
- `conformance/report/`: relatórios com o contexto do fornecedor (desafio, instalação, último elo aceito e emissões).

Consumidores (por exemplo, a CLI de emissão) devem passar por todos os casos com a versão exata do pacote que usam.

Formatos: `specs/031-licenciamento-business-offline/contracts/license-format.md` e `report-format.md` no repositório
`altivy-it/dokpod`.
