---
title: Introdução
description: O que é o Dokpod, como ele funciona e o estado da próxima linha.
sidebar_position: 1
slug: /
---

O **Dokpod** é um console web self-hosted para descobrir, observar e operar
containers **Docker** e **Podman** em vários servidores **Linux** e
**Windows**, sem Kubernetes e sem nuvem do fornecedor.

:::warning Próxima linha em preparação
Este manual acompanha o que está na `main` do Dokpod depois da 0.8 e pode
mudar antes da próxima release. Para a versão publicada, use o manual da 0.8.
:::

## O que o Dokpod faz

**Operação de containers**

- Iniciar, parar, pausar, reiniciar e remover containers, inclusive em lote.
- Inspeção, logs ao vivo com segredos mascarados e terminal interativo no
  navegador, com a sessão auditada.
- Criação guiada de containers e pull de imagens de registros permitidos.
- Volumes, redes e pods do Podman, com verificação de uso antes da remoção.
- Métricas de CPU e memória por host e por container, e eventos dos engines em
  tempo real.

**Governança e segurança**

- Permissões por ambiente e por ação, decididas pelo Keycloak a cada
  requisição. Quem não tem permissão não enxerga o ambiente.
- Login com LDAP, Active Directory ou OIDC pela federação do Keycloak. O
  Dokpod não guarda senhas.
- Políticas por estágio (Produção, Homologação e Desenvolvimento):
  justificativa obrigatória e registros de imagens permitidos.
- Alertas por consumo e estado, com silenciamento, e trilha de auditoria
  filtrável e exportável em CSV.

## Como funciona

O plano de controle (console web, BFF, API, PostgreSQL e Keycloak) roda em
containers no seu ambiente. Em cada servidor gerenciado, um **agente** leve
fala só com o engine local e abre uma conexão **gRPC com mTLS de saída** para
o plano de controle.

- Nenhuma porta do Docker ou do Podman fica exposta na rede.
- O plano de controle nunca acessa o engine de um host remoto: só o agente
  executa comandos, e sempre comandos de domínio versionados, nunca um proxy
  genérico da API do engine.
- Em Linux, o agente roda em container. Em Windows, roda como Windows Service
  self-contained, sem runtime .NET instalado no host.
- Se o plano de controle ou o agente cair, os containers continuam rodando, e
  o estado é reconciliado quando a conexão volta.

## Edições

A **Dokpod Community Edition** é gratuita e de código aberto, sob a licença
`AGPL-3.0-only`, sem chave de licença e sem limite de servidores, usuários ou
containers. Segurança, mTLS, autorização por ambiente e por ação e auditoria
fazem parte da Community Edition.

## Próximos passos

1. Confira os [requisitos e a compatibilidade](requisitos.md).
2. [Instale com o instalador guiado](instalacao.md).
3. [Conecte hosts remotos](hosts-remotos.md) e conheça os [agentes](agentes.md).
