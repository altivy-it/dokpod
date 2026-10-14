# Quickstart de Validação: Terminal

1. Hosts Docker e Podman; containers `alpine` (sh), `debian` (bash), distroless (sem shell), um com label `dokpod.exec=disabled` e um em Produção.
2. Abrir aba Terminal: prompt em ≤ 3 s; comandos, redimensionamento, colar multi-linha com confirmação.
3. Produção: justificativa obrigatória; conferir auditoria `requested/started/ended`.
4. Fechar aba, `exit`, parar container, desconectar agente, revogar scope: término em todos os casos; inspeção do exec (`GET /exec/{id}/json`) com `Running=false` em ≤ 5 s (SC-002); reiniciar o agente com sessão ativa e conferir `orphan_suspected` quando aplicável.
5. Ociosidade e duração máxima reduzidas em teste: aviso e término.
6. Limites de sessões simultâneas; saída massiva com taxa limitada; medir latência de comandos (SC-004).
7. Canários digitados e exibidos: ausentes em PostgreSQL, logs e telemetria (SC-005).
8. Acessibilidade: foco, saída do terminal por teclado, modo leitor de tela; loop visual desktop/mobile.
