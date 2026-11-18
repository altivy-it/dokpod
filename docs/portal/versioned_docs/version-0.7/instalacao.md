---
title: Instalação
description: Como avaliar o Dokpod 0.7, que ainda não tem pacote de instalação para produção.
sidebar_position: 3
---

:::info Sem instalação de produção na 0.7
A linha 0.7 não tem pacote nem instalador para produção. A avaliação usa a
stack de laboratório do repositório, voltada a quem contribui com o projeto.
O instalador guiado chegou na 0.8; veja a
[instalação da 0.8](/instalacao/).
:::

## Avaliação em laboratório

A stack de laboratório sobe o console, o BFF, a API e, opcionalmente, um
agente Linux com acesso ao socket Docker local. Ela usa uma plataforma de
identidade externa (Keycloak e PostgreSQL) já em execução.

1. Siga o guia
   [`deploy/e2e/README.md` da tag v0.7.0](https://github.com/altivy-it/dokpod/blob/v0.7.0/deploy/e2e/README.md)
   para preparar a rede de identidade, o certificado da API e as variáveis
   externas.
2. Configure o realm, os clients e as permissões conforme
   [a configuração do Keycloak](https://github.com/altivy-it/dokpod/blob/v0.7.0/docs/configuracao-keycloak.md).
3. Consulte a
   [distribuição e operação](https://github.com/altivy-it/dokpod/blob/v0.7.0/docs/distribuicao.md)
   para engines, agentes e ordem de atualização.

## Cuidados

- Certificados, senhas e arquivos `.env` ficam sempre fora do repositório.
- Use um certificado de servidor para a API com SAN igual ao nome que os
  agentes usam para conectar. Não reutilize um certificado limitado a
  `localhost`.
- A stack de laboratório usa credenciais administrativas do PostgreSQL só por
  compatibilidade. Ambientes promovidos devem usar banco e role de runtime
  próprios.
