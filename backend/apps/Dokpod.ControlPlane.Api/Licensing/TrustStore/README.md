# Trust store da licença Business

Chaves **públicas** P-256 do fornecedor (PEM `PUBLIC KEY`), embutidas na composição Business como recurso
(spec 031 T027, ADR 2026-0008). O nome do arquivo é o `kid`: `<kid>.pem`, com `kid` em `[a-z0-9-]{1,64}`.

- Nenhuma chave privada entra neste repositório nem em imagem; o carregador recusa qualquer PEM que não seja `PUBLIC KEY`.
- Rotação: inclua a chave nova antes de emitir com ela e retire a antiga só depois que as licenças forem reemitidas
  (runbook da T053).
- Chaves sintéticas dos vetores de teste (`dokpod-test-*`) nunca entram aqui (threat model L8).

A primeira chave de produção depende da decisão do provedor de chave na fase F2 (D-010). Sem chave, toda licença é
recusada como `unknown_key`, o que é o comportamento esperado.
