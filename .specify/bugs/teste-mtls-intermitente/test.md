# Verificação de Bug: teste de recusa mTLS intermitente

- **Slug**: teste-mtls-intermitente
- **Testado em**: 2026-11-27
- **Avaliação**: ./assessment.md
- **Correção**: ./fix.md
- **Resultado**: partial

## Resumo

O teste corrigido passou em 25 de 25 rodadas, e a suíte sem integração da API passou. A falha
original depende de timing e não foi provocada de forma determinística nesta verificação: o
caminho `IOException` não ocorreu em nenhuma rodada, nem antes nem depois da correção. Por isso o
resultado é `partial`, e não `verified`. Uma falha intermitente diferente apareceu na mesma
classe e foi registrada em separado.

## Verificações Executadas

| Verificação | Comando / Ação | Resultado | Notas |
|-------------|----------------|-----------|-------|
| Reprodução após correção | `TransportBoundaryTests` em laço, 25 vezes, no container `lzocateli/dotnet-sdk:10.0.403-noble-v1` | not-run | A corrida do TLS 1.3 não se manifestou: nenhuma `IOException` observada. O caso original segue sendo o único do CI. |
| Testes novos / atualizados | o mesmo laço | pass | `KestrelMtlsHandshake_RejectsClientOutsideTrustedAuthority` passou 25 de 25. |
| Suíte de regressão | `dotnet test backend/tests/Dokpod.ControlPlane.Api.Tests -c Release --filter 'Category!=Integration'` | pass | 177 aprovados, 62 ignorados (dependem de ambiente) e 0 falhas. |
| Lint / formatação | `dotnet format whitespace … --verify-no-changes --include …/TransportBoundaryTests.cs` | pass | exit 0. |

## Trechos de Saída

```text
[1..16, 18..25] Passed!  - Failed:     0, Passed:    18, Skipped:     0, Total:    18
[17]   Failed ...TransportBoundaryTests.KestrelSession_ReconnectFencesPreviousStream [743 ms]
Passed!  - Failed:     0, Passed:   177, Skipped:    62, Total:   239
```

`KestrelSession_ReconnectFencesPreviousStream`, rodado em laço separado (falhou na rodada 22):

```text
Assert.Equal() Failure: Values differ
Expected: Aborted
Actual:   Unknown
  at ...TransportBoundaryTests.cs:line 673
```

## Riscos Residuais

- A correção vem da análise do tipo de exceção: `IOException` base, da qual `HttpIOException`
  deriva. Não houve reprodução forçada da corrida. A confirmação definitiva será o CI sem
  reincidência.
- `KestrelSession_ReconnectFencesPreviousStream` tem outra intermitência, fora deste escopo: o
  status `Unknown` no lugar de `Aborted` no stream cercado. Pode envolver o produto e precisa de
  `/speckit-bug-assess` própria.

## Recomendação

Integre a correção. O teste-alvo ficou robusto às três formas da mesma recusa, sem regressões.
Encerre o bug se o CI não reincidir nas próximas execuções. Abra uma avaliação separada para a
intermitência do fencing na reconexão.
