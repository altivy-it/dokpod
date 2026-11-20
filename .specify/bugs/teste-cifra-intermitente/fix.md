# Correção de Bug: teste da cifra do pacote do host falha de forma intermitente

- **Slug**: teste-cifra-intermitente
- **Corrigido em**: 2026-11-20
- **Avaliação**: ./assessment.md
- **Estado**: applied
- **Origem**: IA assistida; revisor humano: Lincoln (`@lzocateli`).

## Resumo

O teste `a cifra abre com a frase e não com outra` exigia exceção sempre que a frase errada era usada. No formato do `openssl enc` (CBC sem autenticação), o padding passa por acaso em cerca de 1/256 das vezes. Agora o teste exige o que o formato garante: com a frase errada, a decifração lança exceção ou devolve bytes diferentes do original. Nunca devolve o conteúdo. O código do instalador não mudou.

## Alterações

| Arquivo | Alteração | Notas |
|------|--------|-------|
| `deploy/installer/tests/Hosts.Tests.ps1` | modificado | A asserção `Should -Throw` com a frase errada foi trocada por "exceção ou conteúdo diferente". |

## Destaques do Diff (opcional)

```powershell
$wrong = $null
try { $wrong = Unprotect-HostPackage -Bytes $cipher -Passphrase 'frase-errada' } catch { $wrong = $null }
if ($null -ne $wrong) { [System.Convert]::ToBase64String($wrong) | Should -Not -Be ([System.Convert]::ToBase64String($bytes)) }
```

## Testes Adicionados ou Atualizados

- `deploy/installer/tests/Hosts.Tests.ps1::a cifra abre com a frase e não com outra`: a frase errada nunca recupera o conteúdo; a ida e volta com a frase certa e o cabeçalho `Salted__` continuam verificados.

## Verificação Local

- `podman build --target test -f deploy/installer/Dockerfile .` e execução da imagem (o mesmo do job `Installer` do CI): **284 passed, 0 failed, 69 skipped**.
- PSScriptAnalyzer com `deploy/installer/PSScriptAnalyzerSettings.psd1` em `src/`: sem achados (código de saída 0).
- Exercício do caso raro, na imagem de teste, com `$script:Iterations = 1` só nesta verificação: 3.000 cifragens com a frase certa e decifragens com a frase errada. Houve **16 sem exceção** (0,53%, compatível com cerca de 1/256), todas com conteúdo diferente do original (**0 recuperações**). O teste antigo falharia nesses 16 casos; o novo passa.

## Desvios da Avaliação

Nenhum.

## Acompanhamento

- O PR desta correção só pode entrar depois do #159 (release 0.10.0): um push no `main` antes disso faz o release-please reescrever o Release PR e invalida a RC aceita.
- Observação da avaliação, sem mudança aqui: no host de destino, uma frase errada também pode passar pelo `openssl enc -d` e falhar só no `tar`.
