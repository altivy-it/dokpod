# Contrato: CLI do instalador

Ponto de entrada no host: `install.sh` (asset da release; [D-001](../research.md#d-001-forma-do-instalador)). Ele repassa
os argumentos à imagem `dokpod-installer` da mesma versão.

```text
install.sh [comando] [opções]
```

## Comandos

| Comando | Efeito | Requisitos |
| --- | --- | --- |
| `install` (padrão) | Modo guiado. Sem `--answers`, pergunta tudo; com `--answers`, roda sem interação. Mostra o plano e aplica após confirmação. | FR-001 a FR-004 |
| `plan` | Mostra o plano sem efeitos. É o mesmo que `install --dry-run`. | FR-004 |
| `upgrade --version <X.Y.Z>` | Atualiza a instalação para uma versão maior, com o fluxo de migrations de FR-011 e FR-036 a FR-038. | FR-032 |
| `migrations status \| plan \| export --out <arquivo> \| verify` | Repassa ao [modo administrativo](admin-commands.md) com a conexão do *owner*. Nenhum desses comandos altera o banco. | FR-036 a FR-038 |
| `sql export --out <diretório>` | Extrai o catálogo SQL da versão (arquivos e `manifest.json`) para revisão e alteração. Sem efeitos. | FR-040 |
| `recreate <item>[,<item>...]` | Recria certificados (`pki:edge`, `pki:api`, `pki:agent:<id>`, `pki:ca`), segredos (`secret:<nome>`), configuração (`config`) ou serviços (`service:<nome>`). | FR-029, FR-030 |
| `recreate --all` | Recriação total. Pede a confirmação normal e depois a segunda confirmação nomeada para dados. | FR-029, SC-008 |
| `host add --name <nome> [--engine docker\|podman\|windows]` (Docker e Podman só rootless) | Cadastra o ambiente, emite o certificado e gera o pacote em `packages/`. Com `windows`, o pacote traz PFX, CA e os parâmetros de `manage-windows-service.ps1 -Action Install` (endpoint e `environmentId`), sem definição de container. | FR-025 |
| `host revoke --environment <id> --reason <texto>` | Revoga a identidade do agente. | FR-025 |
| `certificates check` | Lista validade e alerta vencimento. | FR-019 |
| `status` | Estado convergido ou divergente por componente, sem efeitos. | FR-026 |
| `help [comando]`, `--help` | Ajuda sem efeitos. | FR-035 |

## Opções globais

| Opção | Padrão | Observação |
| --- | --- | --- |
| `--answers <arquivo>` | — | [Esquema](answers.schema.json). Com ela, nada é perguntado; falta de resposta obrigatória sai com código 2. |
| `--install-dir`, `--secrets-dir` | [Layout](../data-model.md#layout-no-host) | Recusados se forem iguais, se um estiver dentro do outro ou se estiverem dentro de um checkout Git. |
| `--version <X.Y.Z>` | Versão do próprio `install.sh` | Só release publicada; não aceita tag livre. |
| `--offline <diretório>` | — | Imagens, manifesto e assinaturas exportados ([D-010](../research.md#d-010-imagens-assinatura-e-host-sem-internet)). |
| `--dry-run` | — | Plano sem efeitos. |
| `--yes` | — | Confirma o plano normal no modo não interativo. **Não** confirma remoção de dados nem aplicação de migrations; essas exigem `--confirm-data-removal <lista nomeada>` e `--confirm-migrations <hash do plano>`. |
| `--sql-dir <diretório>` | — | Catálogo SQL sobreposto ([D-013](../research.md#d-013-catálogo-de-arquivos-sql-e-sobreposição-externa)). O plano lista cada arquivo alterado com o hash original e o novo. |
| `--confirm-sql-overrides <lista>` | — | No modo não interativo, confirma exatamente os caminhos alterados. `--yes` não basta (FR-041). |
| `--manual-migrations` | — | Exporta o SQL, pausa com o código 10 e, na retomada, roda `verify` (FR-038). |
| `--non-interactive` | Automático sem TTY | Proíbe prompts. |

## Códigos de saída (FR-035)

| Código | Significado |
| --- | --- |
| 0 | Convergido ou aplicado com sucesso. |
| 1 | Erro na aplicação. O relatório indica a etapa, e a retomada é segura. |
| 2 | Uso inválido ou resposta obrigatória ausente. |
| 3 | Pré-requisito não atendido. Nada foi alterado. |
| 4 | Confirmação recusada ou ausente. Nada foi alterado depois do ponto de recusa. |
| 5 | Verificação de assinatura ou manifesto falhou. Nada foi alterado. |
| 6 | Divergência de migrations: o banco está mais novo, ou `verify` falhou. |
| 7 | Outra execução em andamento (lock). |
| 10 | Pausado para migration manual. Retome com o mesmo comando. |

## Saída

- **Plano**: tabela por componente com ação e motivo, mais o resumo de efeitos colaterais, por exemplo "os agentes
  precisarão do novo certificado da CA".
- **Segredos**: nenhuma linha contém segredo. A senha temporária do primeiro administrador é a única exceção. Ela é
  mostrada uma vez, num bloco separado, só em TTY, e nunca vai ao relatório (FR-014, FR-033). Sem TTY, ela vai para
  `secretsDir/initial-admin-password` (`0600`), e a saída indica o caminho. O arquivo é removido na primeira execução
  seguinte em que a senha já foi trocada.
- **Fim**: a URL pública, o usuário administrador, o caminho do relatório e os próximos passos.
