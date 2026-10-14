# Quickstart de Validação: Visão Geral, Hosts e Eventos

1. Subir stack E2E (`deploy/e2e`) com dois agentes: um em Docker e outro em Podman (rootless, serviço adicionado pela tarefa de infraestrutura E2E), estágios Produção e Homologação.
2. Configurar `EngineSupport:Docker:MinimumVersion` acima da versão do agente Docker para forçar "Atualizar".
3. Criar containers sintéticos (OOM deve gerar um único evento de erro): em execução, parado (exit 0), falha (exit 1), OOM (`--memory 16m` com alocação), unhealthy (health check falhando), pausado, reiniciando (`--restart always` com saída imediata), com portas publicadas.
4. Abrir `/` pela borda nginx com `?stage=production`: conferir título, subtítulo, indicadores, rosca por engine, hosts e eventos contra o engine.
5. Deixar os hosts sem mudanças por 5 minutos: continuam "Em dia". Parar o agente Podman: após ~45 s o host aparece "Desconectado" e um evento é registrado. Reiniciar o plano de controle com um agente parado: o host volta como "Desconectado".
5a. Provocar um container OOM e medir o tempo até o evento aparecer no feed (≤ 5 s, SC-002).
6. Abrir `/hosts`: busca/filtros, cadastro com estágio, alteração de estágio (repetir o mesmo estágio não gera auditoria) e revogação com confirmação; conferir auditoria.
7. Usuário sem `environment:read` em um host: confirmar ausência em contagens, eventos e hosts.
8. PostgreSQL: conferir partições de `environment_events`, executar retenção com período reduzido em ambiente de teste.
9. Rodar agente N-1 contra servidor N e confirmar inventário e comandos; rodar agente N contra servidor N-1 e confirmar sessão estável sem `engine_events`.
10. Loop visual (1920, 1440, 1100, 390 e 320px) da Visão geral e de Hosts nos estados carregando, vazio, erro, forbidden e desatualizado, com revisão humana registrada.
