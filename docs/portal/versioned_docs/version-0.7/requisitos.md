---
title: Requisitos e compatibilidade
description: Engines, hosts e versões homologados na linha 0.7 do Dokpod.
sidebar_position: 2
---

## Engines homologados

| Engine | Versão mínima | Socket procurado pelo agente |
| --- | --- | --- |
| Docker Engine | 27.0.0 | `/run/docker.sock`, `/var/run/docker.sock` |
| Podman (API compatível com Docker) | 5.0.0 | `$XDG_RUNTIME_DIR/podman/podman.sock` (rootless), `/run/podman/podman.sock` (rootful) |

- A variável `DOKPOD_AGENT_DOCKER_SOCKET`, quando definida, sempre vence. Sem
  ela, o agente usa o primeiro socket existente na ordem da tabela.
- O engine efetivo é detectado pela resposta de `/version`, não pelo caminho
  do socket.
- Hosts abaixo da versão mínima aparecem como **Atualizar** no console. Versão
  que não pode ser interpretada aparece como **Desconhecido**.
- Pods e a API Libpod do Podman ficam fora desta linha, exceto na criação de
  containers com segredos e health check do Podman.

## Situação por alvo

| Alvo | Situação na 0.7 |
| --- | --- |
| Docker em Linux | validado na stack real; aguardando os gates de release |
| Podman rootless em Linux | validado na stack real; aguardando os gates de release |
| Docker em Windows (agente como Windows Service) | em qualificação |
| Podman rootful | não qualificado |
| Kubernetes, Swarm e Nomad | fora do escopo |

## Plano de controle

- Todos os componentes rodam em containers: console web, BFF, API,
  PostgreSQL e Keycloak.
- O **Keycloak** é obrigatório. O Dokpod usa um realm próprio (`dokpod`) e
  não tem usuário local nem modo que ignore a autorização.
- O PostgreSQL do Dokpod e o do Keycloak têm bancos, credenciais e backups
  separados.
- O endpoint gRPC dos agentes exige certificado de servidor emitido pela PKI
  da organização ou por uma autoridade confiável.

## Rede

- Os agentes abrem conexão **de saída** para o plano de controle, por gRPC com
  mTLS. Não é preciso abrir portas de entrada nos hosts gerenciados.
- O socket do engine nunca é publicado em porta TCP.

## Compatibilidade entre versões

- **Agente anterior com servidor novo**: suportado. O servidor usa só o que o
  agente anuncia.
- **Agente novo com servidor anterior**: o servidor descarta capacidades que
  não conhece e mantém a sessão.
- **Ordem de atualização**: atualize primeiro o plano de controle e depois os
  agentes.
