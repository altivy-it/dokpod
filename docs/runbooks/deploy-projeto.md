# Deploy do Dokpod

Semente do Altivy.Groundwork (componente `deploy`): este arquivo pertence ao projeto e
complementa o procedimento padrão de [deploy-e-rollback.md](deploy-e-rollback.md).
Preencha cada seção antes do primeiro deploy e mantenha-a atualizada em PR.

## Destinos

| Environment | Destino | Runner | Revisores |
| --- | --- | --- | --- |
| `quality` | VM de qualificação da spec 005 (`hostinger-staging-brazildevops`), stack de `deploy/e2e` com Podman rootless | `ubuntu-24.04` do GitHub, por SSH | `@lzocateli` |
| `production` | A definir; sem destino, o deploy falha fechado por falta das credenciais do Environment | `ubuntu-24.04` do GitHub, por SSH | `@lzocateli` |

## Execução

`.github/workflows/deploy.yml` não conhece o compose do host. Ele copia por SSH o
`release-manifest.json` já validado pelo guard e chama o hook do host, que aplica somente os
digests do manifesto:

1. `DEPLOY_HOOK apply <manifesto> <token> <usuário>`: login no GHCR com o token do job
   (`packages: read`, válido só durante o job, gravado em diretório temporário removido no fim),
   `pull` de cada `referência@digest`, gravação de `compose-release.yaml` (override com as imagens
   por digest) e recriação apenas dos serviços cuja imagem em execução difere da do manifesto.
2. `DEPLOY_HOOK check <manifesto> <versão>` (job Smoke test): cada container mapeado precisa estar
   saudável, rodar a imagem do digest do manifesto e ter `org.opencontainers.image.version` igual à
   versão da release.

O hook de referência é [deploy/hooks/compose-deploy-hook.sh](../../deploy/hooks/compose-deploy-hook.sh),
configurado no host por `~/.config/dokpod-deploy/deploy.env` (pasta e arquivos do compose, env-file,
projeto, perfis e mapa imagem → serviço). Idempotência: aplicar a mesma release de novo não recria
nada, porque a comparação é pelo ID da imagem resolvido do digest.

Credenciais, todas no Environment (nunca no repositório):

| Nome | Tipo | Conteúdo |
| --- | --- | --- |
| `DEPLOY_SSH_KEY` | secret | Chave privada ed25519 dedicada ao deploy desse Environment |
| `DEPLOY_SSH_KNOWN_HOSTS` | secret | Linha de `known_hosts` do host, conferida fora de banda (sem TOFU) |
| `DEPLOY_SSH_TARGET` | var | `usuário@host` |
| `DEPLOY_HOOK` | var | Caminho do hook no host |

No host, a chave pública entra em `authorized_keys` com `restrict` (sem PTY nem
encaminhamentos). Sem qualquer um dos quatro valores, o deploy falha antes de conectar.

## Migrações e compatibilidade

O deploy só troca imagens; não aplica migrations. Uma release com migration nova exige, antes do
deploy, o backup e a aplicação da migration conforme
[backup-restore-e-atualizacao.md](backup-restore-e-atualizacao.md). As migrations são
forward-only: o rollback direto só vale entre versões sem migration nova. Até a `v0.1.0` não houve
migration entre versões publicadas, e `rollbackMinimumVersion` segue `0.0.0`. Ao publicar uma
release cuja migration a versão anterior não tolera, suba `rollbackMinimumVersion` de
`.github/ci-cd.json` para essa versão no mesmo PR, e o rollback abaixo dela passa a ser recusado
com orientação de roll-forward.

## Smoke test e verificações após o deploy

O job Smoke test roda o `check` do hook (estado, imagem e versão por serviço). Depois de um deploy
em `quality`, confira também pelo runbook de atualização: console 200 pela borda, agentes
reconectados, auditoria até a troca idêntica e nenhuma linha `fail`/`crit` no log da API.

## Contatos

Aprovação, operação e incidentes: `@lzocateli`.
