# Runbook: rotação de chave, comprometimento e revogação da licença Business

Spec 031 (T053, História 6), D-011 e threat model (L9, R5, R7). Tudo funciona sem rede: chaves públicas viajam na
imagem Business e pacotes de revogação chegam ao cliente como arquivo.

## Onde ficam as chaves

- **Chave privada**: só no provedor de chave do fornecedor (decisão da fase F2). Nunca em repositório, imagem, argumento
  de linha de comando ou log.
- **Chaves públicas**: `backend/apps/Dokpod.ControlPlane.Api/Licensing/TrustStore/<kid>.pem`, PEM `PUBLIC KEY` P-256,
  embutidas só na composição Business. O nome do arquivo é o `kid` (`[a-z0-9-]{1,64}`, por exemplo `dokpod-2027-1`).
- O trust store aceita várias chaves por `kid` e vários `kid` ao mesmo tempo. Chaves sintéticas dos testes
  (`dokpod-test-*`) nunca entram nele.

## Rotação planejada

1. **Gerar a chave nova** no provedor e exportar só a parte pública.
2. **Publicar a chave nova antes de usá-la**: adicionar `TrustStore/<kid-novo>.pem`, mantendo a antiga, e lançar uma
   versão Business. Enquanto houver clientes na versão anterior, não assine nada com a chave nova.
3. **Passar a assinar com a chave nova** (`kid` novo no header) quando a versão do passo 2 estiver distribuída.
4. **Reemitir** as licenças vigentes assinadas pela chave antiga, na renovação ou antes, conforme o contrato.
5. **Retirar a chave antiga**: remover `TrustStore/<kid-antigo>.pem` numa versão posterior. A partir dela, documentos
   assinados pela chave antiga deixam de valer (`unknown_key` ou `bad_signature`).

Verificação: o teste `RevocationVerifierTests.Rotation_TrustStoreWithTwoKeysAcceptsLicensesFromBoth` cobre a
sobreposição e a retirada.

## Comprometimento da chave (threat model L9)

1. **Parar de assinar** com a chave comprometida e revogar o acesso a ela no provedor.
2. **Gerar a chave nova** e lançar, com urgência, uma versão Business **sem** a chave comprometida e com a nova.
3. **Reemitir** todas as licenças assinadas pela chave comprometida com a chave nova, uma revisão nova por licença.
4. **Comunicar os clientes**: atualizar a imagem e importar a licença reemitida. Até atualizarem, instalações offline
  continuam aceitando a chave comprometida (risco residual R7); o contrato trata o intervalo.
5. **Auditar** no registro de emissões tudo o que foi assinado pela chave comprometida e conferir, nos relatórios de
   apuração seguintes, importações sem emissão correspondente (resultado `mismatch` do `report verify`).

## Revogação

O pacote (`typ: dokpod-revocation+jws`, [license-format.md](../../specs/031-licenciamento-business-offline/contracts/license-format.md#payload-do-pacote-de-revogação))
revoga licenças da própria organização até uma revisão, com `sequence` estritamente crescente.

**No fornecedor**: emitir com a CLI (`revocation create`, spec 031 US5) e entregar o arquivo ao cliente pelo canal
combinado.

**No cliente**: Configurações → Instalação → Licença → *Importar revogação*, ou
`POST /api/v1/licensing/revocations` (`licensing:manage`, `application/jose`, até 64 KiB). Resultado:

| Situação | Resposta |
| --- | --- |
| Pacote válido, da organização da licença ativa, sequência maior que a importada | `200`; estado `restricted`, autenticidade `revoked`, evento `revocation.imported` |
| Sequência igual ou menor que a importada | `422 license_obsolete` |
| Outra organização, assinatura inválida ou outro tipo de documento | `422 license_invalid` |

A revogação só recusa novas execuções de recursos Business. Containers, dados, leitura, backup, exportação, renovação e
a CE seguem intactos (FR-003, FR-021). Uma licença com revisão maior que a revogada (renovação) volta a valer.

## Limites

- A revogação offline é cooperativa: restaurar um backup anterior à importação desfaz a revogação (risco R5). Depois de
  qualquer restauração, reimporte o último pacote de revogação recebido. A auditoria seguinte detecta a restauração como
  `discontinuous`.
- Sem rede, o fornecedor não sabe quando o pacote foi importado; o relatório de apuração da renovação mostra o evento
  `revocation.imported` na cadeia.
