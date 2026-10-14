# Quickstart de Validação: Lista Unificada e Lote

1. Stack E2E com hosts Docker e Podman em Produção e Homologação; 40 containers sintéticos em todas as situações.
2. Abrir `/containers?stage=production`: chips (Engine, Estágio), resumo com as seis situações somando o total, colunas, ordenação de CPU/memória na página carregada, busca ("8080", nome de imagem, host), filtros, ordenação, "Carregar mais".
3. Selecionar 2 containers com falha e um em execução; "Parar": confirmação lista elegíveis/ignorados; acompanhar resultados.
4. Recarregar durante o lote na mesma aba: acompanhamento recuperado sem reenvio. Medir: 20 containers com resultado individual em ≤ 30 s (SC-002).
5. "Remover" 2 containers em Produção: botão só habilita após digitar "2"; volumes preservados (`docker volume ls`).
6. Pausar/retomar em Docker e Podman (rootful); Podman rootless cgroup v1: ação desabilitada.
7. Ações por linha (logs/terminal/inspecionar) abrem o detalhe ou aparecem desabilitadas; exportar CSV e conferir escape.
8. Usuário sem acesso a um host: ausência em linhas, resumo, busca e CSV.
9. Abrir link legado `/environments/{id}/containers` e confirmar redirecionamento.
10. Medir SC-001 com 1.120 containers em 56 e 100 hosts; contar interações de "parar 2 containers com falha" (SC-005); loop visual (1920, 1440, 1100, 390 e 320px) com revisão humana registrada.
11. Usuário sem `container:pause`: Pausar/Retomar desabilitados com motivo.
