# Avaliação de Bug: deploy hook reporta sucesso sem aplicar a release

- **Slug**: deploy-hook-falso-sucesso
- **Criada em**: 2026-11-23
- **Origem**: texto colado (sessão do solicitante, @lzocateli; autorização "Bug Fixing: Sim", 2026-11-23). Origem: IA assistida; revisor humano: pendente.
- **Veredito**: valid
- **Severidade**: high

## Relato (resumido)

O deploy da v0.12.0 em `quality` (run 45289473526, 2026-11-23) terminou com sucesso, inclusive o smoke test, mas o host
continua com as imagens da 0.8.0 (containers de 2026-11-17). O log do job mostra, no `apply` e no `check`:
`manifesto sem as imagens ['dokpod-agent', 'dokpod-api', 'dokpod-bff', 'dokpod-web']`, seguido de
`Todos os serviços já rodam os digests da release; nada recriado.` e `Release 0.12.0 aplicada em quality.`

## Sintoma

Quando o `SERVICES` do host não corresponde às imagens do `release-manifest.json`, o hook falha internamente, mas
`apply` e `check` saem com código 0: o workflow reporta deploy e smoke aprovados sem ter aplicado nem conferido nada.
O esperado é falhar fechado, com código diferente de zero, no `apply` e no `check`.

## Reprodução

1. Host com `SERVICES="dokpod-api=api dokpod-bff=bff dokpod-web=web dokpod-agent=agent-podman"` (nomes anteriores ao #150).
2. Manifesto de uma release a partir de 0.10.0, cujas imagens se chamam `dokpod-ce-api`, `dokpod-ce-bff`,
   `dokpod-ce-web` e `dokpod-ce-agent` (#150, `build(deploy)!`, 2026-11-17).
3. `compose-deploy-hook.sh apply <manifesto> <token> <usuário>` → imprime o erro do Python, "nada recriado" e sai 0.
4. `compose-deploy-hook.sh check <manifesto> 0.12.0` → imprime o erro e sai 0.

Reproduzido no log do run 45289473526 (deploy da v0.12.0) e, pelo estado do host, também nos deploys de 0.10.0,
0.11.0 e 0.12.0-rc.1.

## Caminhos de Código Suspeitos

- `deploy/hooks/compose-deploy-hook.sh:entries()` — o Python sai com `sys.exit("manifesto sem as imagens ...")`, código 1.
- `deploy/hooks/compose-deploy-hook.sh:apply()` — `done < <(entries)`: o código de saída de uma substituição de processo
  não é visto pelo `set -e` nem pelo `pipefail`; o laço roda zero vezes, `stale` fica vazio e a função retorna 0.
- `deploy/hooks/compose-deploy-hook.sh:check()` — mesmo padrão; `failed=0` sem nenhuma iteração, retorna 0.
- `deploy/hooks/compose-deploy-hook.sh:16` — exemplo de `SERVICES` no comentário ainda com os nomes antigos.
- Configuração do host `quality` (`~/.config/dokpod-deploy/deploy.env`, fora do repositório) — `SERVICES` não foi
  atualizado no #150.

## Hipótese de Causa Raiz

Confiança: high. Dois defeitos combinados: (1) o #150 renomeou as imagens da CE e a configuração do host não acompanhou;
(2) o hook consome `entries` por substituição de processo, cujo status o bash descarta, então o erro de mapeamento vira
"nenhum serviço" e o `apply` e o `check` aceitam um conjunto vazio como sucesso. O defeito (2) é o do produto: o hook
deve falhar fechado; o (1) é a correção operacional.

## Correção Proposta

**Preferencial**: em `apply` e `check`, materializar a lista antes do laço (`list=$(entries) || exit`), recusar lista
vazia com mensagem clara e só então iterar (`done <<< "$list"`). Assim o erro do Python e a ausência de serviços fazem
o hook sair com código diferente de zero, e o workflow de deploy falha no `Apply release` e no `Smoke test`. Atualizar o
exemplo de `SERVICES` do comentário para os nomes `dokpod-ce-*`.

**Alternativas**:
- `shopt -s inherit_errexit` ou `wait $!` após a substituição: depende de versão do bash e é menos legível.
- Validar só no `check`: o `apply` continuaria aceitando conjunto vazio; rejeitada.

**Arquivos Prováveis de Alteração**:
- `deploy/hooks/compose-deploy-hook.sh`
- `deploy/hooks/tests/compose-deploy-hook.test.sh` (novo)
- `.github/workflows/ci.yml` (rodar o teste em container)

**Testes a Adicionar ou Atualizar**:
- Mapeamento divergente do manifesto: `apply` e `check` saem com código diferente de zero e imprimem o erro.
- Manifesto sem nenhuma imagem mapeada (SERVICES vazio de correspondência): falha.
- Caminho feliz com `podman` falso: `apply` recria o serviço desatualizado e `check` aprova a versão esperada.

## Riscos e Considerações

- Depois da correção, qualquer host com mapeamento antigo passa a falhar no deploy, o que é o comportamento desejado.
- O aceite de candidata em `quality` da 0.10.0, 0.11.0 e 0.12.0-rc.1 não verificou nada; o solicitante decidiu não
  registrar isso nas notas de release (2026-11-23).
- O hook trata só atualizações sem migration (`Render.psm1`); o salto 0.8.0 → 0.12.0 no `quality` exige aplicar as
  migrations à parte.
- Instalações feitas pelo instalador da 033 geram `SERVICES` pelo `Render.psm1`; conferir que usam os nomes `dokpod-ce-*`.

## Perguntas em Aberto

- Nenhuma.
