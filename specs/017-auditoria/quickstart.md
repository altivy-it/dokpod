# Quickstart: Auditoria

1. Aplique a migration `202610150001_AuditConsole` e recrie a API e o web.
2. Entre como `dokpod-admin` (grupo com `audit:read` nos hosts) e gere ações: mude o estágio de um host, pare um container com justificativa, abra o terminal, reconheça um alerta.
3. Abra Governança → Auditoria: as ações aparecem com o nome do usuário, host, alvo e resultado.
4. Filtre por ator, categoria e resultado; copie o link e reabra.
5. Abra o evento de parada: veja a justificativa, o resultado do comando entre os relacionados e o link para o container.
6. No detalhe de um container, use "Ver auditoria".
7. Exporte o período e confira o CSV e o evento `audit.exported` na trilha.
8. Com um usuário sem `audit:read` num host, confirme que os eventos dele não aparecem.
