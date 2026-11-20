# Avaliação de Bug: teste da cifra do pacote do host falha de forma intermitente

- **Slug**: teste-cifra-intermitente
- **Criada em**: 2026-11-20
- **Origem**: texto colado (conversa de 2026-11-20); falha observada no job `Installer` do CI de `main` no commit `a119e37` (run `44747021406`, tentativa 1), que passou na nova execução sem mudança de código.
- **Veredito**: valid
- **Severidade**: medium
- **Origem da avaliação**: IA assistida; revisor humano: Lincoln (`@lzocateli`).

## Relato (literal ou resumido)

Log do Pester no job `Installer`:

```text
[-] a cifra abre com a frase e não com outra 1.05s
  Expected an exception to be thrown, but no exception was thrown.
  at { Unprotect-HostPackage -Bytes $cipher -Passphrase 'frase-errada' } | Should -Throw, /opt/dokpod-installer/tests/Hosts.Tests.ps1:68
Tests Passed: 283, Failed: 1, Skipped: 69
```

A falha deixou o CI do commit do Release PR #157 vermelho. A guarda do release esperou até o tempo limite e não criou a tag `v0.9.2`. Quando o CI foi refeito, o `main` já tinha avançado com o #158, e a 0.9.2 final acabou não sendo publicada (o rótulo do #157 foi trocado para `autorelease: tagged` por decisão do solicitante).

## Sintoma

O teste exige que decifrar o pacote com a frase errada **sempre** lance exceção. Em cerca de 0,4% das execuções, a decifração com a frase errada termina sem erro e devolve bytes sem sentido, e o teste falha. O esperado é um teste determinístico, que só reprove quando a frase errada recuperar o conteúdo.

## Reprodução

1. Rode `Protect-HostPackage` com `-Passphrase 'frase-certa-033'` e um conteúdo qualquer; o sal muda a cada execução.
2. Rode `Unprotect-HostPackage` no resultado com `-Passphrase 'frase-errada'`.
3. Repita algumas centenas de vezes: em cerca de 1 a cada 256 tentativas não há exceção. Com 600.000 iterações do PBKDF2, cada tentativa leva cerca de 1 s.

## Caminhos de Código Suspeitos

- `deploy/installer/tests/Hosts.Tests.ps1:63-69` (`It 'a cifra abre com a frase e não com outra'`): a asserção `Should -Throw` na linha 68 parte de uma premissa falsa.
- `deploy/installer/src/Hosts.psm1:48-63` (`Unprotect-HostPackage`): usa `DecryptCbc` com `PaddingMode.PKCS7`. Com a chave errada, o último bloco vira 16 bytes pseudoaleatórios, e o padding PKCS7 é válido quando o último byte é `0x01` (1/256) ou nos casos raros de padding maior (cerca de 1/65.536). A exceção só ocorre quando o padding é inválido.
- `deploy/installer/src/Hosts.psm1:25-46` (`Protect-HostPackage`): o formato é o de `openssl enc -aes-256-cbc -pbkdf2` (FR do pacote de host da spec 033), sem autenticação do texto cifrado. Por isso a frase errada não é detectável com certeza na decifração.

## Hipótese de Causa Raiz

Confiança: **high**. O CBC com PKCS7 não autentica nada: a única "verificação" de frase é o padding, que passa por acaso com probabilidade de cerca de 1/256. O código está correto para o formato escolhido; o defeito está no teste, que trata a validação do padding como garantia. Isso afeta só os testes: `Unprotect-HostPackage` é usado apenas por eles (`deploy/installer/src/Hosts.psm1:49`).

## Correção Proposta

**Preferencial**: trocar a asserção da linha 68 por uma propriedade determinística: com a frase errada, a decifração **não recupera o conteúdo**. Ou lança exceção (padding inválido) ou devolve bytes diferentes do original. O teste captura a exceção quando ela ocorre e, quando não ocorre, compara o resultado com os bytes originais. A primeira parte do teste (cabeçalho `Salted__` e ida e volta com a frase certa) não muda.

**Alternativas**:

- Repetir a decifração errada até lançar exceção: continua probabilística e mais lenta (cada tentativa custa cerca de 1 s de PBKDF2). Rejeitada.
- Fixar o sal no teste para um caso conhecido em que o padding falha: exigiria expor um parâmetro de sal em `Protect-HostPackage` só para teste e tornaria o teste dependente de um valor mágico. Rejeitada.
- Mudar o formato para um modo autenticado (AES-GCM ou HMAC): quebraria a compatibilidade com o `openssl enc` do host de destino, que é requisito do pacote. Fora do escopo.

**Arquivos Prováveis de Alteração**:

- `deploy/installer/tests/Hosts.Tests.ps1`

**Testes a Adicionar ou Atualizar**:

- Atualizar `It 'a cifra abre com a frase e não com outra'`: com a frase errada, exigir exceção **ou** conteúdo diferente do original; nunca o conteúdo original.
- Validar com a suíte do instalador (Pester 284 testes) e PSScriptAnalyzer, como no CI.

## Riscos e Considerações

- **Segurança**: sem mudança. A confidencialidade depende da frase e do PBKDF2 com 600.000 iterações; o SHA-256 ao lado do `agent.tar.enc` confere a integridade do cifrado no host de destino.
- **Operação**: no host de destino, uma frase errada também pode passar pelo `openssl enc -d` sem erro (mesma probabilidade) e falhar só no `tar`, com uma mensagem menos clara. Isso é inerente ao formato e fica registrado como observação, sem mudança nesta correção.
- **Processo de release**: a falha intermitente de um job obrigatório no commit do Release PR deixa a release presa quando o `main` avança antes do CI ser refeito. A correção remove esta causa; não altera a guarda do Groundwork.

## Perguntas em Aberto

- Nenhuma.
