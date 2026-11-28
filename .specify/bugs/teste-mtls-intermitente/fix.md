# Correção de Bug: teste de recusa mTLS intermitente

- **Slug**: teste-mtls-intermitente
- **Corrigido em**: 2026-11-27
- **Avaliação**: ./assessment.md
- **Estado**: applied

## Resumo

A asserção de `KestrelMtlsHandshake_RejectsClientOutsideTrustedAuthority` passou a aceitar toda a
família `IOException`, além de `HttpRequestException` e `RpcException`. No TLS 1.3, a recusa do
certificado de cliente pode chegar ao cliente na escrita, como `IOException` do `SslStream`, e não
só na leitura.

## Alterações

| Arquivo | Alteração | Notas |
|---------|-----------|-------|
| `backend/tests/Dokpod.ControlPlane.Api.Tests/Agents/TransportBoundaryTests.cs` | modificado | asserção do tipo da recusa e comentário sobre a corrida do TLS 1.3 |

## Destaques do Diff

```csharp
// No TLS 1.3 o servidor recusa o certificado depois do handshake do cliente: conforme o momento,
// a recusa chega na escrita (IOException do SslStream) ou na leitura (HttpIOException, RpcException).
Assert.True(
    exception is IOException
    || exception is HttpRequestException
    || exception is RpcException,
```

## Testes Adicionados ou Atualizados

- `TransportBoundaryTests.KestrelMtlsHandshake_RejectsClientOutsideTrustedAuthority`: continua
  exigindo que a chamada falhe antes de estabelecer sessão. Agora aceita as três formas da mesma
  recusa.

## Verificação Local

- Comando executado: `dotnet test backend/tests/Dokpod.ControlPlane.Api.Tests --filter
  FullyQualifiedName~TransportBoundaryTests`, 25 vezes seguidas, no container
  `lzocateli/dotnet-sdk:10.0.403-noble-v1` (a mesma imagem do CI).
- Resultado: o teste corrigido passou em 25 de 25. Em 24 rodadas a classe teve 18 de 18
  aprovados. Na rodada 17, falhou outro teste da classe, descrito em Acompanhamento.

## Desvios da Avaliação

Nenhum. O `HttpRequestException` estava como opcional na avaliação e foi incluído.

## Acompanhamento

- **Outro teste intermitente, fora deste escopo:**
  `TransportBoundaryTests.KestrelSession_ReconnectFencesPreviousStream`.
  - Falhou 1 vez em 25 rodadas da classe, e na rodada 22 de um laço só dele.
  - Erro: `Assert.Equal() Failure: Expected: Aborted, Actual: Unknown`, linha 673.
  - Ao reconectar, o stream anterior às vezes termina com `StatusCode.Unknown` em vez de `Aborted`.
  - Pode ser comportamento do produto (o status do fencing perdido quando a conexão cai), e não só
    do teste. Por isso precisa de avaliação própria: `/speckit-bug-assess`, por exemplo com
    `slug=fencing-reconexao-status-unknown`.
