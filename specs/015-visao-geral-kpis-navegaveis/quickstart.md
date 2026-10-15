# Quickstart: validar a spec 015

Pré-requisito: stack completa no ar (`deploy/e2e/README.md`, seção Linux com Podman), com containers em execução, parados, pausados e com falha.

1. Abra `https://localhost:7443/dokpod/` e selecione um estágio.
2. Clique em "Em execução": a tela Containers abre com o chip "Situação: Em execução", o mesmo estágio e o total igual ao número do card.
3. Volte e clique em "Parados": a lista mostra parados e pausados; o total bate com o card.
4. Clique em "Com falha ou reiniciando": a lista mostra falha e reinício.
5. Clique em "CPU média do cluster": lista em execução ordenada por CPU decrescente; o primeiro container é o de maior CPU, e quem não tem amostra aparece no fim com "—".
6. Clique em "Memória alocada": mesma lista ordenada por memória; o primeiro é o de maior memória.
7. Na lista, clique nos cabeçalhos CPU e Memória: a ordem alterna, e a URL reflete `sort`.
8. Navegue pelos cards só com Tab e Enter: todos recebem foco visível e abrem o destino.
9. Compare o card "Memória alocada" com a soma da coluna Memória da lista aberta por ele: diferença de até 1 ponto percentual.
