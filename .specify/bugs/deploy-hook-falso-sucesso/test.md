# Verificação de Bug: deploy hook reporta sucesso sem aplicar a release

- **Slug**: deploy-hook-falso-sucesso
- **Testado em**: 2026-11-23
- **Avaliação**: ./assessment.md
- **Correção**: ./fix.md
- **Resultado**: verified

## Resumo

No host `quality`, com o `SERVICES` antigo e o manifesto da v0.12.0, o hook instalado repete o falso sucesso (código 0)
e o hook corrigido falha fechado (código 3). Os testes novos passam e o mesmo teste reprova o hook anterior.

## Verificações Executadas

| Verificação | Comando / Ação | Resultado | Notas |
| --- | --- | --- | --- |
| Reprodução antes da correção | hook instalado no `quality`: `compose-deploy-hook.sh check <manifesto v0.12.0> 0.12.0` | pass (reproduz) | erro do mapeamento e `exit=0` |
| Reprodução após correção | hook corrigido, copiado para diretório temporário no `quality`, mesmo `check` | pass | `exit=3`, "nada aplicado"; nada instalado, diretório removido |
| Testes novos | `podman run --rm -v deploy/hooks:/h:ro python:3.13.7-slim-bookworm bash /h/tests/compose-deploy-hook.test.sh` | pass | 6 de 6 |
| Teste contra o hook antigo | mesmo teste com o hook do `main` | fail (esperado) | 4 falhas, incluindo as duas do bug |
| Lint | `shellcheck v0.11.0` no hook e no teste; `bash -n` | pass | sem avisos |
| `apply` no host | — | skipped | altera o ambiente; fica para a reaplicação da v0.12.0 após ajustar `SERVICES` |
| CI (`actionlint`, job Installer) | — | not-run | roda no PR |

## Trechos de Saída

```text
--- hook corrigido
manifesto sem as imagens ['dokpod-agent', 'dokpod-api', 'dokpod-bff', 'dokpod-web']
Mapeamento SERVICES não corresponde ao manifesto; nada aplicado.
exit=3
--- hook instalado
manifesto sem as imagens ['dokpod-agent', 'dokpod-api', 'dokpod-bff', 'dokpod-web']
exit=0
```

## Riscos Residuais

- O `apply` do hook corrigido só foi exercitado com `podman` falso; o caminho real roda na reaplicação da v0.12.0 no
  `quality`.
- Hosts com mapeamento antigo passam a falhar no deploy até o `deploy.env` ser atualizado.

## Recomendação

Encerre o bug após o merge do PR e a instalação do hook corrigido no `quality`; a reaplicação da v0.12.0 serve de
prova do caminho real.
