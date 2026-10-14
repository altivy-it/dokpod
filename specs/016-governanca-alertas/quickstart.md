# Quickstart: validar a spec 016

Pré-requisito: stack completa (`deploy/e2e/README.md`), com os scopes `alert:read` e `alert:manage` provisionados para o ambiente de teste.

1. Abra Governança → Alertas: as 9 regras padrão aparecem em Regras.
2. Crie um container que encerre com código 3 (`podman run --name falha <imagem> sh -c 'exit 3'`): em até 15 s, um alerta alto aparece em Abertos, no contador do menu e no sino.
3. Reconheça o alerta com comentário: ele fica "reconhecido" com você como responsável.
4. Inicie o container de novo com um comando que permaneça em execução: o alerta é resolvido automaticamente em até 1 minuto.
5. Num container de Produção, escolha Parar: o diálogo exige justificativa; envie; a lista mostra "Parado intencionalmente" e nenhum alerta abre.
6. Repita com Pausar e Remover; consulte `GET .../containers/{id}/justifications` e o histórico do host.
7. Crie um silenciamento de 30 minutos para o host e provoque nova falha: o alerta aparece como silenciado, fora do contador.
8. Desative a regra "Container encerrado com erro" e provoque falha: nenhum alerta abre.
9. Em Homologação, pare um container: a justificativa é opcional.
