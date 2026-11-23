# Correção de Bug: deploy hook reporta sucesso sem aplicar a release

- **Slug**: deploy-hook-falso-sucesso
- **Corrigido em**: 2026-11-23
- **Avaliação**: ./assessment.md
- **Estado**: applied

## Resumo

`apply` e `check` passam a materializar a lista de serviços antes dos laços e a sair com código 3 quando o `SERVICES`
não corresponde ao manifesto ou não sobra nenhum serviço; antes, o erro ficava numa substituição de processo e os dois
modos aprovavam um conjunto vazio. Um teste com `podman` falso reproduz o caso e entra no CI.

## Alterações

| Arquivo | Alteração | Notas |
| --- | --- | --- |
| `deploy/hooks/compose-deploy-hook.sh` | modificado | `entries_or_fail` (código 3); laços sobre `<<< "$list"`; `PROFILES` opcional de fato |
| `deploy/hooks/tests/compose-deploy-hook.test.sh` | teste adicionado | `podman` e `podman-compose` falsos, diretório temporário, `--help` |
| `.github/workflows/ci.yml` | modificado | passo "Deploy hook tests" no job Installer, em `python:3.13.7-slim-bookworm` |
| `docs/runbooks/deploy-projeto.md` | modificado | nomes do mapa `SERVICES` e a falha com código 3 |

## Destaques do Diff

```bash
entries_or_fail() {
  local list
  list=$(entries) || { echo "Mapeamento SERVICES não corresponde ao manifesto; nada aplicado." >&2; exit 3; }
  [[ -n "$list" ]] || { echo "Nenhum serviço de SERVICES encontrado no manifesto; nada aplicado." >&2; exit 3; }
  printf '%s\n' "$list"
}
```

## Testes Adicionados ou Atualizados

- `compose-deploy-hook.test.sh` — "apply recusa SERVICES com nomes fora do manifesto" (código 3, sem `podman-compose`).
- "check recusa SERVICES com nomes fora do manifesto" (código 3).
- "apply recria o serviço desatualizado" (com `PROFILES` ausente), "apply não recria o que já roda o digest",
  "check aprova a versão esperada" e "check reprova o container com outra imagem".

## Verificação Local

- `podman run --rm -v deploy/hooks:/h:ro python:3.13.7-slim-bookworm bash /h/tests/compose-deploy-hook.test.sh` →
  6 de 6 aprovados.
- O mesmo teste contra o hook do `main` (antes da correção) → 4 falhas, incluindo as duas do bug: código 0 com
  `manifesto sem as imagens ['dokpod-api']` e "nada recriado", como no run 45289473526.
- `shellcheck v0.11.0` no hook e no teste → sem avisos. `bash -n` → ok.

## Desvios da Avaliação

- A avaliação citava o exemplo de `SERVICES` no comentário do hook (linha 16) com nomes antigos. No repositório ele já
  usava `dokpod-ce-*`; os nomes antigos estão só na cópia instalada no host `quality`. Nada mudou no comentário.
- Escopo ampliado: o teste do caminho feliz expôs que `PROFILES`, documentado como opcional, quebrava o `apply` com
  `set -u` (`PROFILES: unbound variable`) quando ausente. Corrigido com `${PROFILES:+...}`. No `quality` não se
  manifestava porque o host define `PROFILES`.

## Acompanhamento

- Operação no `quality` (autorizada em 2026-11-23): atualizar `SERVICES` para `dokpod-ce-*`, instalar o hook corrigido,
  fazer backup, aplicar as migrations da 0.8.0 até a 0.12.0 e reaplicar a v0.12.0 pelo workflow de deploy.
