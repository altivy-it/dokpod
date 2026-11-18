---
title: Requisitos y compatibilidad
description: Motores, hosts y versiones admitidos en la línea 0.8 de Dokpod.
sidebar_position: 2
---

## Motores admitidos

| Motor | Versión mínima | Socket que busca el agente |
| --- | --- | --- |
| Docker Engine | 27.0.0 | `/run/docker.sock`, `/var/run/docker.sock` |
| Podman (API compatible con Docker) | 5.0.0 | `$XDG_RUNTIME_DIR/podman/podman.sock` (rootless), `/run/podman/podman.sock` (rootful) |

- La variable `DOKPOD_AGENT_DOCKER_SOCKET` siempre prevalece cuando está
  definida. Sin ella, el agente usa el primer socket existente en el orden de
  la tabla.
- El motor real se detecta por la respuesta de `/version`, no por la ruta del
  socket.
- Los hosts por debajo de la versión mínima aparecen como **Actualizar** en la
  consola. Una versión que no se puede interpretar aparece como
  **Desconocida**.
- Los pods de Podman y la API Libpod quedan fuera de esta línea, salvo la
  creación de contenedores con secrets y health checks de Podman.

## Situación por destino

| Destino | Situación |
| --- | --- |
| Docker en Linux | validado en el stack real; pendiente de los gates de release |
| Podman rootless en Linux | validado en el stack real; pendiente de los gates de release |
| Docker en Windows (agente como Windows Service) | en calificación |
| Podman rootful | no calificado |
| Kubernetes, Swarm y Nomad | fuera del alcance |

## Plano de control

- Todos los componentes se ejecutan en contenedores: consola web, BFF, API,
  PostgreSQL y Keycloak.
- **Keycloak** es obligatorio. Dokpod usa un realm propio (`dokpod`) y no tiene
  usuario local ni modo que omita la autorización.
- El PostgreSQL de Dokpod y el de Keycloak tienen bases, credenciales y
  backups separados.
- El endpoint gRPC de los agentes requiere un certificado de servidor emitido
  por la PKI de la organización o por una autoridad de confianza.

## Instalador guiado

- Host Linux con Docker Engine o Podman en modo **rootless**, con *linger*
  activo para el usuario (`loginctl enable-linger <usuario>`).
- Nombre público que resuelva a la dirección que usan los navegadores.
- Un puerto HTTPS libre. Por debajo de `net.ipv4.ip_unprivileged_port_start`,
  solo con un nginx del host o con el administrador bajando ese límite.

## Red

- Los agentes abren una conexión **saliente** hacia el plano de control, por
  gRPC con mTLS. No se necesita ningún puerto de entrada en los hosts
  gestionados.
- El socket del motor nunca se publica en un puerto TCP.

## Compatibilidad entre versiones

- **Agente más antiguo con servidor más nuevo**: admitido. El servidor solo
  usa lo que el agente anuncia.
- **Agente más nuevo con servidor más antiguo**: el servidor descarta las
  capacidades que no conoce y mantiene la sesión.
- **Orden de actualización**: primero el plano de control, después los
  agentes.
