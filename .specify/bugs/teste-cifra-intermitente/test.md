# Verificação de Bug: teste da cifra do pacote do host falha de forma intermitente

- **Slug**: teste-cifra-intermitente
- **Testado em**: 2026-11-20
- **Avaliação**: ./assessment.md
- **Correção**: ./fix.md
- **Resultado**: verified
- **Origem**: IA assistida; revisor humano: Lincoln (`@lzocateli`).

## Resumo

Reproduzi o sintoma de forma determinística: com a mesma amostra de 3.000 cifragens, a asserção antiga reprovou 14 vezes (0,47%, perto de 1/256) e a nova reprovou nenhuma. Em nenhum caso a frase errada recuperou o conteúdo. A suíte completa do instalador e o PSScriptAnalyzer passam sem regressão.

## Verificações Executadas

| Verificação | Comando / Ação | Resultado | Notas |
|-------|------------------|--------|-------|
| Reprodução após correção | Na imagem `--target test`, com `$script:Iterations = 1` só nesta verificação: 3.000 cifragens com a frase certa, cada uma avaliada pela asserção antiga (`Should -Throw`) e pela nova (exceção ou conteúdo diferente) | pass | Antiga: 14/3.000 reprovações; nova: 0/3.000 |
| Testes novos / atualizados | `It 'a cifra abre com a frase e não com outra'` na execução da imagem de teste | pass | 856 ms, com as 600.000 iterações reais |
| Suíte de regressão | `podman build --target test -f deploy/installer/Dockerfile .` e `podman run` da imagem (o mesmo do job `Installer` do CI) | pass | 284 passed, 0 failed, 69 skipped |
| Lint / verificação de tipos | `Invoke-ScriptAnalyzer -Path /i/src -Recurse -Settings /i/PSScriptAnalyzerSettings.psd1 -EnableExit` | pass | Código de saída 0 |

## Trechos de Saída

```text
asserção antiga reprovou: 14 de 3000; asserção nova reprovou: 0 de 3000
[+] a cifra abre com a frase e não com outra 856ms
Tests Passed: 284, Failed: 0, Skipped: 69, Inconclusive: 0, NotRun: 0
```

## Riscos Residuais

- A reprodução usa 1 iteração do PBKDF2 para ser viável. O padding depende só de a chave estar errada, não do número de iterações, então a probabilidade é a mesma com 600.000 iterações.
- O CI do PR ainda vai rodar a suíte no GitHub Actions; o resultado local usa podman, não Docker.

## Recomendação

Encerre o bug: verificado com reprodução determinística, sem regressão. O PR só pode entrar depois do #159 (release 0.10.0).
