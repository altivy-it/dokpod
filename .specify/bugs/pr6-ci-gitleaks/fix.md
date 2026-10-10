# Bug Fix: Falhas de CI e falsos positivos no PR 6

- **Slug**: pr6-ci-gitleaks
- **Fixed**: 2026-10-06
- **Assessment**: ./assessment.md
- **Status**: applied

## Summary

Corrigidas a ordem de restore antes das migrations, a verificacao Git no
container frontend e as excecoes estritas para referencias runtime no Gitleaks.
O cliente gerado foi sincronizado com uma operacao ja existente no contrato.
A remediacao foi autorizada pelo usuario nesta sessao; a autorizacao condicional
para reescrever historico nao foi utilizada, pois nao houve segredo real confirmado.

## Changes

| File | Change | Notes |
| ------ | -------- | ------- |
| `.github/workflows/ci.yml` | modified | Restore da solucao antes do EF; safe.directory limitado ao checkout. |
| `.github/workflows/gitleaks.yml` | modified | Regressao das regras antes do scan historico, preservado com exit code bloqueante. |
| `.gitleaks.toml` | modified | Captura separada do valor; excecoes locais e ancoradas, sem excluir arquivos ou commits. |
| `tools/scripts/test-gitleaks.ps1` | added test | 26 casos positivos e negativos, fixtures em memoria e redaction total. |
| `frontend/web/src/app/data-access/generated/control-plane/index.ts` | modified | Exportacao gerada de revokeAgentIdentity e seus tipos. |
| `frontend/web/src/app/data-access/generated/control-plane/sdk.gen.ts` | modified | Operacao gerada de revogacao ja presente no OpenAPI. |
| `frontend/web/src/app/data-access/generated/control-plane/types.gen.ts` | modified | Tipos gerados da mesma operacao. |

## Tests Added or Updated

- `tools/scripts/test-gitleaks.ps1`: detecta passwords literais, DATABASE_PASSWORD,
  Basic, Bearer, Docker auth, regra padrao de token e concatenacoes com literais.
- O mesmo script aceita somente referencias runtime reconhecidas integralmente,
  valores vazios e placeholders documentais, sem ignorar credenciais reais.
- Credencial sintetica gerada aleatoriamente em memoria; relatorio temporario
  totalmente redigido e removido em finally. Secrets externos nao foram lidos.

## Local Verification

- Checkout backend sem assets: restore e metadata EF passaram em container.
- Migrations em PostgreSQL 17.6 efemero: todas as 12 aplicadas.
- `dotnet test Dokpod.slnx --configuration Release --verbosity minimal`:
  206 testes passaram, sem falhas ou skips, com PostgreSQL real isolado.
- `npm ci --no-audit --no-fund` e `npm run generate:api`: passaram em container
  com o mesmo usuario e imagem da CI. Segunda geracao manteve os hashes dos arquivos.
- Git no container reconheceu o checkout com safe.directory=/workspace especifico.
- `ng test --watch=false`: 12 testes passaram em cinco arquivos.
- `ng build --configuration production --base-href /dokpod/`: passou; aviso
  preexistente de CSS 34 bytes acima do budget, fora do escopo.
- `./tools/scripts/test-gitleaks.ps1`: 26 casos passaram.
- `./tools/scripts/test-gitleaks.ps1 --help`: exibiu finalidade, dependencias e exemplos.
- `git log --all --full-history -p --` enviado ao Gitleaks 8.30.1 por stdin,
  com configuracao versionada e redaction total: no leaks found, exit code 0.
- Diagnosticos dos arquivos alterados: nenhum erro; `git diff --check`: passou.
- Container PostgreSQL e rede temporarios removidos apos os testes.

## Deviations from Assessment

- A deteccao por sufixo de chave foi mantida para bloquear DATABASE_PASSWORD;
  secretGroup e allowlists estritas corrigem os falsos positivos sem perder esse caso.
- Apos corrigir o reconhecimento Git, a geracao revelou tres arquivos
  desatualizados. O usuario autorizou explicitamente "Autorizar sincronizacao
  do cliente gerado"; somente esses arquivos foram regenerados, sem alterar
  contrato, endpoint ou UI.
- O relatorio JSON do teste usa um diretorio temporario aleatorio, pois stdout
  nao entregou um relatorio utilizavel. O arquivo e totalmente redigido e
  eliminado ao fim; as fixtures e credenciais sinteticas nao sao persistidas.
- A ajuda foi movida para o inicio do script para ser reconhecida por Get-Help.

## Follow-ups

- Confirmar os checks remotos apos publicar esta remediacao em development.
- Tratar separadamente as vulnerabilidades criticas do job Images, fora desta
  avaliacao; Result continua dependendo desse job.
- Obter as aprovacoes exigidas pelo PR 6. Protecoes, merge e deploy nao foram alterados.
