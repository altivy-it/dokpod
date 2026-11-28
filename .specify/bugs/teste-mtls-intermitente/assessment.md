# Avaliação de Bug: teste de recusa mTLS intermitente

- **Slug**: teste-mtls-intermitente
- **Criada em**: 2026-11-27
- **Origem**: texto colado (log do CI da main, run 45979424852, tentativa 1, commit 8de9245)
- **Veredito**: valid
- **Severidade**: low

## Relato (literal ou resumido)

No CI da main (run 45979424852, commit `8de9245`, 2026-11-27), o teste
`TransportBoundaryTests.KestrelMtlsHandshake_RejectsClientOutsideTrustedAuthority` falhou com:

```text
Unexpected rejection exception: System.IO.IOException
  at ...TransportBoundaryTests.KestrelMtlsHandshake_RejectsClientOutsideTrustedAuthority() in .../TransportBoundaryTests.cs:line 644
```

A nova execução dos jobs que falharam passou. O defeito é do teste, não do produto: a recusa do
certificado de cliente aconteceu, mas chegou como um tipo de exceção que o teste não aceita.
Autorizado pelo solicitante em 2026-11-27 ("Bug Fixing: sim").

## Sintoma

O servidor recusa o agente com certificado fora da autoridade confiável, como esperado. Às vezes o
cliente vê essa recusa como `System.IO.IOException` (conexão encerrada durante a escrita no
`SslStream`), e não como `HttpIOException` ou `RpcException`. A asserção do tipo falha. O esperado
é o teste passar sempre que a conexão for recusada.

## Reprodução

1. Rodar `TransportBoundaryTests.KestrelMtlsHandshake_RejectsClientOutsideTrustedAuthority` várias
   vezes, principalmente com o runner do CI sob carga.
2. Em parte das execuções, o encerramento da conexão pelo Kestrel após o handshake TLS 1.3 (o
   certificado de cliente é validado depois de o cliente já ter enviado dados) chega ao
   `SocketsHttpHandler` como `IOException` base, sem o wrapper `HttpIOException`.
3. A asserção da linha 644 falha com "Unexpected rejection exception: System.IO.IOException".

A reprodução local depende do timing.
[NEEDS CLARIFICATION: frequência no CI. Até agora há um único caso conhecido, nesta run.]

## Caminhos de Código Suspeitos

- `backend/tests/Dokpod.ControlPlane.Api.Tests/Agents/TransportBoundaryTests.cs:644-647`: aceita
  apenas `HttpIOException` ou `RpcException`. `HttpIOException` deriva de `IOException`, mas o
  inverso não vale, por isso a `IOException` base é rejeitada.
- `TransportBoundaryTests.cs:628-642`: a recusa pode aparecer no `WriteAsync` ou no `MoveNext`,
  e o tipo da exceção depende do momento em que o alerta TLS ou o RST chega ao cliente.

## Hipótese de Causa Raiz

No TLS 1.3, o cliente considera o handshake concluído antes de o servidor validar o certificado
de cliente. O servidor então recusa a conexão (alerta TLS ou fechamento). Se isso acontece enquanto
o cliente ainda escreve o frame HTTP/2, o `SslStream` lança `IOException` direto. Se acontece na
leitura da resposta, o `SocketsHttpHandler` embrulha em `HttpIOException`, ou o gRPC converte em
`RpcException`. O teste listou só os dois tipos observados até então, e a terceira forma é uma
corrida legítima. Confiança: high. O produto recusou a conexão como devia, e só o tipo da exceção
variou.

## Correção Proposta

**Preferencial**: na asserção, aceitar a família `IOException`, que inclui `HttpIOException`, além
de `RpcException`. Opcionalmente, também `HttpRequestException`, que o handler pode lançar em
outras corridas da mesma recusa. O teste continua garantindo que a chamada falha e que nenhuma
sessão é estabelecida: o `MoveNext` nunca retorna `SessionEstablished`, porque qualquer retorno
normal faria o `ThrowsAnyAsync` falhar. A mensagem de diagnóstico com o tipo inesperado continua.

**Alternativas**:
- Repetir a tentativa ou ignorar a falha intermitente: esconde a corrida e não melhora a precisão.
  Descartada.
- Verificar a recusa também no servidor (contador ou log de handshake recusado): é mais forte, mas
  exige instrumentação nova fora do escopo mínimo.

**Arquivos Prováveis de Alteração**:
- `backend/tests/Dokpod.ControlPlane.Api.Tests/Agents/TransportBoundaryTests.cs`

**Testes a Adicionar ou Atualizar**:
- Atualizar `KestrelMtlsHandshake_RejectsClientOutsideTrustedAuthority` para aceitar a família
  `IOException`, com um comentário sobre a corrida do TLS 1.3.
- Validar rodando o teste em laço, em container, para confirmar que não falha e que o
  controle positivo (agente confiável estabelece sessão) segue passando.

## Riscos e Considerações

- Aceitar `IOException` é mais amplo, mas no contexto do teste qualquer falha de transporte antes
  da sessão é a recusa esperada. O teste positivo com certificado confiável, no mesmo arquivo,
  impede que uma API quebrada passe despercebida.
- Sem impacto em contrato, segurança do produto, migração ou observabilidade: a mudança é só no
  teste.

## Perguntas em Aberto

- [NEEDS CLARIFICATION: frequência real no CI. Não bloqueia a correção.]
