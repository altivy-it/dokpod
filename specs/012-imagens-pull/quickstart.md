# Quickstart de Validação: Imagens e Pull

1. Stack E2E com registro de laboratório (`registry:2`) público e privado; host Docker autenticado (`docker login`) e host Podman autenticado (`podman login`).
2. Conferir KPI "Imagens sem uso" e tela `/imagens` contra `docker images`/`podman images`, incluindo container criado com `nginx` (sem tag) e imagem cuja tag foi movida para outra versão.
3. "Baixar imagem" na Visão geral: referência pública em 2 hosts; acompanhar resultados e evento "Imagem baixada".
4. Referência inexistente, privada sem login e plataforma incompatível: códigos de falha legíveis.
4a. Testes negativos: referência com `user:pass@`, com `?`/`&`, com espaço; registro fora da lista permitida; usuário sem `image:pull`; host não autorizado; agente sem capability 13. Todos recusados.
4b. Derrubar a resposta do agente durante um pull por tag já presente: reconciliação resulta `indeterminate`, não `succeeded`.
5. Tag `latest`: aviso de tag mutável e digest resolvido.
6. Repetir requisição com a mesma `Idempotency-Key` (inclusive com outro prazo): sem novo download e sem conflito.
6a. Medir SC-002 (imagem de até 200 MB em ≤ 2 min).
6b. Host com mais de 200 imagens: snapshot em várias páginas, nenhuma acima de 256 KiB.
7. Varredura por credenciais de laboratório em respostas, auditoria e logs (SC-003).
8. Loop visual desktop/mobile da tela Imagens e do drawer.
