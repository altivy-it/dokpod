---
title: Hosts remotos
description: Conectar e revogar hosts remotos com pacotes de agente gerados pelo instalador.
sidebar_position: 4
---

O instalador gera um pacote cifrado com o agente e a identidade de cada host
remoto. A instalação precisa de `agentGrpcPort` nas respostas: é a porta gRPC
com mTLS dos agentes, a única publicada além da borda.

## Adicionar um host

```bash
sh install.sh host add --name host-b --engine podman     # docker, podman ou windows
```

- O pacote fica em `installDir/packages/<environmentId>.tar`, com permissão
  `0600`, e contém o `README.txt`, o `agent.tar.enc` e o SHA-256 dele.
- A frase da cifra aparece uma única vez no terminal. Sem terminal, ela fica em
  `secretsDir/packages/<id>.passphrase`. Entregue-a por outro canal e apague o
  arquivo.

No host de destino:

1. Confira o pacote: `sha256sum -c agent.tar.enc.sha256`.
2. Decifre:
   `openssl enc -d -aes-256-cbc -pbkdf2 -iter 600000 -md sha256 -in agent.tar.enc -out agent.tar`.
3. Extraia e siga o `README.txt`. No Docker e no Podman é um
   `docker compose up`; no Windows, o agente roda como Windows Service.

O host aparece conectado em até 5 minutos. O comando `status` avisa sobre
pacotes gerados há mais de 7 dias cujo host não conectou.

## Revogar um host

```bash
sh install.sh host revoke --environment <id> --reason "host desativado"
```

A revogação fica na auditoria, e a API é reiniciada para derrubar a sessão
ativa do agente. Os demais agentes reconectam em seguida; o revogado não volta.
