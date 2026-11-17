---
title: Instalación
description: Cómo evaluar Dokpod 0.7, que todavía no tiene paquete de instalación para producción.
sidebar_position: 3
---

:::info Sin instalación de producción en la 0.7
La línea 0.7 no tiene paquete ni instalador para producción. La evaluación usa
el stack de laboratorio del repositorio, pensado para quienes contribuyen al
proyecto. El instalador guiado llega en la próxima línea; vea la versión
**Vista previa** de este manual.
:::

## Evaluación en laboratorio

El stack de laboratorio levanta la consola, el BFF, la API y, opcionalmente,
un agente Linux con acceso al socket Docker local. Depende de una plataforma
de identidad externa (Keycloak y PostgreSQL) ya en ejecución.

1. Siga la
   [guía `deploy/e2e/README.md` de la tag v0.7.0](https://github.com/altivy-it/dokpod/blob/v0.7.0/deploy/e2e/README.md)
   para preparar la red de identidad, el certificado de la API y las variables
   externas.
2. Configure el realm, los clientes y los permisos según la
   [configuración de Keycloak](https://github.com/altivy-it/dokpod/blob/v0.7.0/docs/configuracao-keycloak.md).
3. Consulte
   [distribución y operación](https://github.com/altivy-it/dokpod/blob/v0.7.0/docs/distribuicao.md)
   para motores, agentes y orden de actualización.

Estos documentos del repositorio están en portugués.

## Precauciones

- Certificados, contraseñas y archivos `.env` siempre quedan fuera del
  repositorio.
- Use un certificado de servidor de la API cuyo SAN coincida con el nombre que
  usan los agentes para conectarse. No reutilice un certificado limitado a
  `localhost`.
- El stack de laboratorio usa credenciales administrativas de PostgreSQL solo
  por compatibilidad. Los entornos promovidos deben usar base y rol de
  ejecución propios.
