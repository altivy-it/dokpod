---
name: "Revisar Mudança"
description: "Revisa mudanças e evidências vinculadas ao Spec Kit em modo read-only, sem executar comandos ou correções."
argument-hint: "Diff, branch ou PR, tarefa/remediação Spec Kit e base de comparação"
agent: "Dokpod Code Reviewer"
---

Revise a mudança solicitada sem editar arquivos.

Relacione a mudança à tarefa/remediação Spec Kit e à autorização humana; sinalize ausência de rastreabilidade. Este prompt apoia o processo oficial e não autoriza implementação ou remediação. Não execute comandos, testes, builds ou operações mutantes; use evidências existentes e indique checks ao responsável autorizado.

1. Leia requisito, diff completo, call sites, contratos e testes.
2. Confirme comportamento anterior e proposto.
3. Priorize bypass de autorização, exposição de socket/secret, operação destrutiva, replay, incompatibilidade N/N-1, diferenças entre engines e falha de reconciliação.
4. Confira evidências existentes de validações estreitas e registre lacunas sem executar checks.
5. Ignore preferências estilísticas já cobertas por ferramentas.

Apresente achados primeiro por severidade, com localização, cenário, impacto, correção e teste. Depois liste dúvidas e lacunas. Se não houver achados, declare isso claramente.