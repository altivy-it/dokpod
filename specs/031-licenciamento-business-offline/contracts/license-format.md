# Contrato: formato da licença e do pacote de revogação

**Versão**: perfil 1 (`schemaVersion: 1`, `meteringVersion: 1`) | **Decisões**: [D-001](../research.md#d-001-formato-da-licença), [D-003](../research.md#d-003-limites-defensivos-do-parser), [D-011](../research.md#d-011-revogação-e-rotação)

## Envelope

JWS Compact: `BASE64URL(header) . BASE64URL(payload) . BASE64URL(signature)`, sem padding, sem espaços, arquivo ≤ 64 KiB, texto ASCII.

### Header protegido (exatamente estes membros)

```json
{ "alg": "ES256", "kid": "dokpod-2026-1", "typ": "dokpod-license+jws" }
```

| Membro | Regra |
| --- | --- |
| `alg` | só `ES256` |
| `kid` | 1 a 64 caracteres `[a-z0-9-]`; procurado apenas no trust store embarcado |
| `typ` | `dokpod-license+jws` (licença) ou `dokpod-revocation+jws` (revogação) |

Qualquer outro membro (`jwk`, `jku`, `x5u`, `x5c`, `crit`…) recusa o documento.

### Assinatura

ECDSA P-256 com SHA-256 sobre `ASCII(header_b64 + "." + payload_b64)` recebidos, formato IEEE P1363 (64 bytes). Verificada antes de interpretar o payload.

## Payload da licença

```json
{
  "schemaVersion": 1,
  "meteringVersion": 1,
  "licenseId": "lic_7f3c9a",
  "organizationId": "org_51d2e0",
  "revision": 2,
  "product": "dokpod-business",
  "plan": "business",
  "issuedAt": "2026-11-01T00:00:00Z",
  "notBefore": "2026-11-01T00:00:00Z",
  "expiresAt": "2027-11-01T00:00:00Z",
  "capacity": { "mode": "limited", "nodeEquivalents": 10 },
  "vcpuBand": 32,
  "windowDays": 30,
  "graceDays": 30,
  "slots": [
    { "slotId": "prd", "kind": "production", "enabled": true, "nodeEquivalents": 8 },
    { "slotId": "hml", "kind": "staging", "enabled": true, "nodeEquivalents": 2 }
  ],
  "capabilities": ["image-update-notice", "change-windows"]
}
```

Os valores são fictícios. As regras de cada campo estão em [data-model.md](../data-model.md#licença-documento-assinado-não-tabela). Instantes UTC em RFC 3339 com `Z`; propriedades duplicadas ou desconhecidas recusam o documento; nenhum campo de preço, contato ou inventário existe no schema. `licenseId` e `organizationId` aceitam só `[A-Za-z0-9_-]`, de 1 a 128 caracteres; o resto é `malformed`. `slotId` aceita `[A-Za-z0-9_-]`, de 1 a 64. `windowDays` e `graceDays` são `30` no perfil 1 (outro valor é `unsupported_profile`). Em `capacity.mode = unlimited`, os slots não trazem `nodeEquivalents`; em `limited`, trazem sempre. As capabilities conhecidas por esta versão são `image-update-notice` (spec 025), `change-windows` (026), `audit-siem-export` (027), `code-only-deployment` (028) e `sso-templates` (029).

## Payload do pacote de revogação

```json
{
  "schemaVersion": 1,
  "organizationId": "org_51d2e0",
  "sequence": 3,
  "issuedAt": "2026-12-01T00:00:00Z",
  "revokedLicenses": [ { "licenseId": "lic_7f3c9a", "revisionsUpTo": 2 } ]
}
```

`sequence` estritamente crescente por organização; até 64 entradas; só IDs da própria organização.

## Falhas tipadas do verificador

| Código | Quando |
| --- | --- |
| `malformed` | envelope, Base64URL, UTF-8 ou JSON inválido; tamanho ou profundidade acima do limite; propriedade duplicada ou desconhecida |
| `unsupported_profile` | `alg`, `typ`, header extra, `schemaVersion` ou `meteringVersion` não suportados |
| `unknown_key` | `kid` fora do trust store |
| `bad_signature` | assinatura não confere |
| `wrong_product` | `product` diferente de `dokpod-business` |
| `inconsistent_dates` | ordem de datas inválida |
| `invalid_capacity` | capacidade, faixa, slots ou soma de cotas inválidos |
| `unknown_capability` | capability fora da allowlist da versão |

## Vetores de conformidade

O pacote do verificador publica vetores (chave sintética de teste, documentos válidos e inválidos com o código esperado) consumidos pelos testes do produto e da CLI. Chaves de teste nunca são aceitas pelo trust store de produção.
