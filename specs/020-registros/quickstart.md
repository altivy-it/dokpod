# Quickstart: Registros

1. Aplique a migration `202610150005_RegistryInventory` e recrie API, web e agente.
2. Monte no agente um arquivo de credenciais de teste (`auths` com `registry.teste.local`) em `DOKPOD_AGENT_REGISTRY_AUTH_FILE`.
3. Abra Infraestrutura → Registros: `docker.io` (e demais registros das imagens) com hosts e imagens; `registry.teste.local` com "credencial" sim, sem imagens.
4. Abra o detalhe de `docker.io`: por host, imagens, credencial e link para Imagens filtrada.
5. Na política, defina Homologação = `quay.io`: a confirmação mostra as imagens fora; após salvar, `docker.io` aparece "fora da política" e um download de `docker.io/library/alpine` em Homologação é recusado em até 5 s; a auditoria mostra as listas anterior e nova.
6. Volte a política de Homologação para "qualquer registro".
7. Varra API, banco e logs pelo canário da senha do arquivo de credenciais: nenhuma ocorrência.
