# Trust store da licença Business

Chaves **públicas** P-256 do fornecedor (PEM `PUBLIC KEY`), embutidas na composição Business como recurso
(spec 031 T027, ADR 2026-0008). O nome do arquivo é o `kid`: `<kid>.pem`, com `kid` em `[a-z0-9-]{1,64}`.

- Nenhuma chave privada entra neste repositório nem em imagem; o carregador recusa qualquer PEM que não seja `PUBLIC KEY`.
- Rotação: inclua a chave nova antes de emitir com ela e retire a antiga só depois que as licenças forem reemitidas
  (runbook da T053).
- Chaves sintéticas dos vetores de teste (`dokpod-test-*`) nunca entram aqui (threat model L8).

| `kid` | Desde | SHA-256 do SPKI |
| --- | --- | --- |
| `dokpod-2026-1` | 2026-10-31 | `0f95c9fe1aa8094e5a9d832716d11177902655e1ed00d10aad4abcf953d065e0` |

A chave privada correspondente fica só no cofre do fornecedor, em PKCS#8 cifrado (D-010).
