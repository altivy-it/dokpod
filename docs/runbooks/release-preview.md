# Runbook: release preview

Arquivo gerenciado pelo Altivy.Groundwork (componente `release-preview`). Não edite: mudanças vão
para o catálogo. A estratégia completa está em [../estrategia-de-release.md](../estrategia-de-release.md).

Repositório: `altivy-it/dokpod` · branch padrão: `main` ·
aceite: `deploy`.

## 1. Gerar a candidata

1. Confirme que o Release PR está aberto e atualizado:

   ```bash
   gh pr list --repo altivy-it/dokpod --head release-please--branches--main
   ```

2. Dispare o workflow na branch padrão:

   ```bash
   gh workflow run gw-preview.yml --repo altivy-it/dokpod --ref main
   ```

3. O workflow calcula `vX.Y.Z-rc.N`, cria a tag e o rascunho privado, compila/testa os pacotes e
   disponibiliza o artifact privado `release-assets` no run. Ainda não há publicação pública.
4. Abra a execução pelo link do Environment `distribution`, baixe `release-assets`, verifique
   `SHA256SUMS` e teste o pacote. Aprove `distribution` somente depois; o workflow anexa os mesmos
   artifacts à prerelease, sem rebuild, atesta o `release-manifest.json` e não atualiza Homebrew/winget.
5. Confira:

   ```bash
   gh release view vX.Y.Z-rc.N --repo altivy-it/dokpod
   ```

## 2. Validar em quality

O job `Dispatch preview environment` do `gw-preview.yml` dispara o `gw-deploy.yml` na tag da candidata
quando `deploy.autoDeployEnvironment` é `quality`. Sem isso, dispare:

```bash
gh workflow run gw-deploy.yml --repo altivy-it/dokpod --ref vX.Y.Z-rc.N -f environment=quality
```

Acompanhe o deploy e o smoke test; o sucesso é o aceite.

Com sucesso, o job `Record candidate acceptance` anexa `preview-acceptance.json` à prerelease e
reexecuta o CI do Release PR.

## 3. Promover

1. Verifique o check `Preview gate` no Release PR; ele mostra a candidata validada.
2. Faça o merge do Release PR (squash) e siga o [runbook de release](release.md).
3. Implante em `production` na tag estável:

   ```bash
   gh workflow run gw-deploy.yml --repo altivy-it/dokpod --ref vX.Y.Z -f environment=production
   ```

   O guard confere a candidata validada e o diff entre ela e a estável.

## 4. Falhas comuns

| Sintoma | Causa provável | Ação |
| --- | --- | --- |
| `Nenhum Release PR aberto` | não há commit releasable desde a última estável | integre um `feat`/`fix` ou use `Release-As:` |
| `O Release PR #N não está sobre <sha>` | o release-please ainda não atualizou o Release PR; sem `"always-update": true` em `release-please-config.json`, um commit que não muda as notas (`ci`, `docs`, `chore`) nunca o atualiza (`remained the same` no log) | aguarde o `gw-release.yml` na `main` e dispare de novo; sem a opção, adicione-a por PR |
| `Preview gate` vermelho depois do aceite | a `main` andou e o Release PR mudou de commit-base | gere nova candidata (`rc.N+1`) e valide |
| `Preview gate` vermelho sem nova integração | o CI do Release PR não foi reexecutado | `gh run rerun <id>` na execução do CI do Release PR |
| Deploy da candidata recusado | Environment diferente de `quality` ou tag fora do padrão `-rc.N` | use `quality`; prereleases `preview.N` nunca são implantáveis |
| `production` recusado por diff | código integrado depois da candidata | nova versão com nova candidata |
| Emergência sem `quality` disponível | — | commit com rodapé `Preview-Waiver: <motivo>` desde a última estável |
