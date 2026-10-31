# Contrato: comandos da CLI de emissão (repositório separado)

**Decisão**: [D-010](../research.md#d-010-cli-de-emissão) | **Executável proposto**: `dokpod-license-admin`

Implementado em `altivy-it/dokpod-license-admin` (privado, proprietário; decisões da F2 em D-010). Saída estruturada (JSON) em stdout, diagnóstico redigido em stderr.

| Comando | Efeito | Precisa de chave |
| --- | --- | --- |
| `--help`, `--version` | ajuda e versão | não |
| `registry init` | cria ou atualiza o schema `license_admin` do registro (idempotente) | não |
| `request hash --input <pedido>` | hash canônico do pedido, que o aprovador cita em `approval` | não |
| `license inspect --input <arquivo>` | mostra header e payload, marcados como "não autenticados" até verificar | não |
| `license verify --input <arquivo> --keys <pasta> [--at <instante>]` | verifica com as chaves públicas da pasta e mostra o resultado tipado (licença ou revogação) | não |
| `report verify --input <relatório> --request <pedido>` | verifica o relatório de apuração contra o registro de emissões e o desafio do pedido ([report-format.md](report-format.md#verificação-pelo-fornecedor)) | não |
| `license issue --request <pedido> --report <relatório> --output <arquivo> [--dry-run]` | contratação aprovada; exige relatório `ok` (FR-034) | sim |
| `license renew --request <pedido> --report <relatório> --output <arquivo> [--dry-run]` | nova revisão do mesmo `licenseId`; exige relatório `ok` (FR-034) | sim |
| `license amend --request <pedido> --output <arquivo> [--dry-run]` | aditivo de direitos ou cotas | sim |
| `revocation create --request <pedido> --output <arquivo> [--dry-run]` | pacote de revogação | sim |
| `key list --keys <pasta>` | IDs, SHA-256 do SPKI e se a chave privada está presente | não |
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

Os comandos que assinam recebem `--keys <pasta>` (com `<kid>.key.pem` em PKCS#8 cifrado e `<kid>.pub.pem`), `--key-id <kid>` e, opcionalmente, `--passphrase-fd <n>`; sem ele, a passphrase é pedida no terminal sem eco. O registro vem de `DOKPOD_LICENSE_ADMIN_REGISTRY`, sem senha (passfile do PostgreSQL).

## Pedido de emissão

Documento JSON do fornecedor, nunca gerado pela instalação do cliente. Propriedades desconhecidas recusam o pedido (código 2).

| Campo | Regra |
| --- | --- |
| `requestId`, `contractRef`, `organizationId`, `operator` | `[A-Za-z0-9_-]{1,128}` |
| `kind` | `issue`, `renew`, `amend` ou `revocation` |
| `licenseId` | obrigatório fora da revogação; em `issue` não pode existir, em `renew`/`amend` precisa existir na mesma organização |
| `challenge` | só em `issue` e `renew`: `[A-Za-z0-9_-]{8,64}`, o mesmo informado ao cliente |
| `license` | fora da revogação: `plan`, `notBefore`, `expiresAt` (UTC, `Z`, sem frações), `capacity`, `vcpuBand`, `slots`, `capabilities` do [formato](license-format.md#payload-da-licença); `notBefore` não pode ser anterior ao instante de emissão |
| `revokedLicenses` | só na revogação: 1 a 64 itens `{licenseId, revisionsUpTo}` de licenças emitidas para a organização |
| `approval` | `{approver, requestSha256}`: outra pessoa, ligada ao hash do pedido (código 4 se ausente ou divergente) |
| `reportApproval` | opcional: `{approver, requestSha256, reportSha256}` de uma terceira pessoa, só com `--accept-report-findings` |

O hash é o SHA-256 do JSON canônico do pedido (chaves em ordem ordinal, sem espaços, só inteiros) sem `approval` e `reportApproval`. A revisão é numerada pela CLI (1 na contratação, a última mais um depois), a sequência da revogação por organização, e `product`, `schemaVersion`, `meteringVersion`, `windowDays` e `graceDays` são fixos do perfil 1. Validado antes de assinar: aprovação, datas, soma das cotas dos slots habilitados contra a capacidade, capabilities conhecidas, idempotência por `requestId` + hash.

## Regras

- `--dry-run` não assina, não grava, não numera revisão e não acessa o provedor de chave.
- Passphrase, chave privada ou token nunca por argumento.
- Saída gravada de forma atômica; reexecução após falha recupera o mesmo resultado lógico.
- O artefato é verificado com `Dokpod.Licensing` antes de ser entregue.
- O pedido de contratação ou renovação traz o `challenge` que o cliente informa ao exportar o relatório. Emitida a licença, o registro guarda `installationId`, `headSeq` e `headHash` do relatório aceito, base da continuidade na auditoria seguinte.
- Emitir com relatório não `ok` exige `--accept-report-findings <justificativa>` e a segunda aprovação ligada ao hash do pedido e do relatório; os achados ficam no registro de emissões.
