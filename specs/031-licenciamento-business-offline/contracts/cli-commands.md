# Contrato: comandos da CLI de emissão (repositório separado)

**Decisão**: [D-010](../research.md#d-010-cli-de-emissão) | **Executável proposto**: `dokpod-license-admin`

Desenho a revisar na fase F2; não há repositório nem comando existente. Saída estruturada (JSON) em stdout, diagnóstico redigido em stderr.

| Comando | Efeito | Precisa de chave |
| --- | --- | --- |
| `--help`, `--version` | ajuda e versão | não |
| `license inspect --input <arquivo>` | mostra header e payload, marcados como "não autenticados" até verificar | não |
| `license verify --input <arquivo>` | verifica com o trust store autorizado e mostra o resultado tipado | não |
| `report verify --input <relatório> --request <pedido>` | verifica o relatório de apuração contra o registro de emissões e o desafio do pedido ([report-format.md](report-format.md#verificação-pelo-fornecedor)) | não |
| `license issue --request <pedido> --report <relatório> --output <arquivo> [--dry-run]` | contratação aprovada; exige relatório `ok` (FR-034) | sim |
| `license renew --request <pedido> --report <relatório> --output <arquivo> [--dry-run]` | nova revisão do mesmo `licenseId`; exige relatório `ok` (FR-034) | sim |
| `license amend --request <pedido> --output <arquivo> [--dry-run]` | aditivo de direitos ou cotas | sim |
| `revocation create --request <pedido> --output <arquivo> [--dry-run]` | pacote de revogação | sim |
| `key list` | IDs, fingerprints públicos e estado operacional | não |
| `audit export --output <arquivo>` | exporta o registro de emissões sem segredos | não |

## Códigos de saída

| Código | Significado |
| --- | --- |
| 0 | sucesso, ou repetição idêntica de um pedido já emitido |
| 1 | erro de uso ou de ambiente |
| 2 | pedido inválido (datas, cotas acima da capacidade, capability desconhecida) |
| 3 | conflito: mesmo `requestId` com conteúdo diferente, ou arquivo de saída existente com outro conteúdo |
| 4 | aprovação ausente ou não confere com o hash do pedido |
| 5 | registro de emissões ou provedor de chave indisponível; nada foi emitido |
| 6 | relatório ausente, `tampered`, `discontinuous`, `mismatch` ou `stale`; nada foi emitido |

## Pedido de emissão

Documento do fornecedor, nunca gerado pela instalação do cliente: `requestId`, referência opaca do contrato, `organizationId`, licença anterior (renovação ou aditivo), direitos, datas, cotas e referência da aprovação. Validado antes de assinar: operador e aprovação, produto e schema, datas e revisão em relação à anterior, soma das cotas, idempotência por `requestId` + hash, chave permitida.

## Regras

- `--dry-run` não assina, não grava, não numera revisão e não acessa o provedor de chave.
- Passphrase, chave privada ou token nunca por argumento.
- Saída gravada de forma atômica; reexecução após falha recupera o mesmo resultado lógico.
- O artefato é verificado com `Dokpod.Licensing` antes de ser entregue.
- O pedido de contratação ou renovação traz o `challenge` que o cliente informa ao exportar o relatório. Emitida a licença, o registro guarda `installationId`, `headSeq` e `headHash` do relatório aceito, base da continuidade na auditoria seguinte.
- Emitir com relatório não `ok` exige `--accept-report-findings <justificativa>` e a segunda aprovação ligada ao hash do pedido e do relatório; os achados ficam no registro de emissões.
