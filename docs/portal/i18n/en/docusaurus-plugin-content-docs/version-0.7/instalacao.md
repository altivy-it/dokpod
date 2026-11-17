---
title: Installation
description: How to evaluate Dokpod 0.7, which has no production installation package yet.
sidebar_position: 3
---

:::info No production installation in 0.7
The 0.7 line has no production package or installer. Evaluation uses the
repository's lab stack, aimed at project contributors. The guided installer
arrived in 0.8; see the [0.8 installation](/en/instalacao/).
:::

## Lab evaluation

The lab stack starts the console, the BFF, the API and, optionally, a Linux
agent with access to the local Docker socket. It relies on an external
identity platform (Keycloak and PostgreSQL) that is already running.

1. Follow the
   [`deploy/e2e/README.md` guide at tag v0.7.0](https://github.com/altivy-it/dokpod/blob/v0.7.0/deploy/e2e/README.md)
   to prepare the identity network, the API certificate, and the external
   variables.
2. Configure the realm, clients, and permissions according to the
   [Keycloak configuration](https://github.com/altivy-it/dokpod/blob/v0.7.0/docs/configuracao-keycloak.md).
3. See
   [distribution and operations](https://github.com/altivy-it/dokpod/blob/v0.7.0/docs/distribuicao.md)
   for engines, agents, and update order.

These repository documents are in Portuguese.

## Precautions

- Certificates, passwords, and `.env` files always stay outside the
  repository.
- Use an API server certificate whose SAN matches the name agents use to
  connect. Do not reuse a certificate limited to `localhost`.
- The lab stack uses PostgreSQL administrative credentials only for
  compatibility. Promoted environments must use their own database and
  runtime role.
