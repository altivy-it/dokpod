---
title: Hosts remotos
description: Conectar y revocar hosts remotos con paquetes de agente generados por el instalador.
sidebar_position: 4
---

El instalador genera un paquete cifrado con el agente y la identidad de cada
host remoto. La instalación necesita `agentGrpcPort` en las respuestas: es el
puerto gRPC con mTLS de los agentes, el único publicado además del borde.

## Agregar un host

```bash
sh install.sh host add --name host-b --engine podman     # docker, podman o windows
```

- El paquete queda en `installDir/packages/<environmentId>.tar`, con permiso
  `0600`, y contiene el `README.txt`, el `agent.tar.enc` y su SHA-256.
- La frase del cifrado aparece una sola vez en la terminal. Sin terminal, queda
  en `secretsDir/packages/<id>.passphrase`. Entréguela por otro canal y borre
  el archivo.

En el host de destino:

1. Verifique el paquete: `sha256sum -c agent.tar.enc.sha256`.
2. Descifre:
   `openssl enc -d -aes-256-cbc -pbkdf2 -iter 600000 -md sha256 -in agent.tar.enc -out agent.tar`.
3. Extraiga y siga el `README.txt`. En Docker y Podman es un
   `docker compose up`; en Windows, el agente corre como Windows Service.

El host aparece conectado en hasta 5 minutos. El comando `status` avisa sobre
paquetes generados hace más de 7 días cuyo host no se conectó.

## Revocar un host

```bash
sh install.sh host revoke --environment <id> --reason "host desactivado"
```

La revocación queda en la auditoría, y la API se reinicia para cortar la sesión
activa del agente. Los demás agentes se reconectan enseguida; el revocado no
vuelve.
