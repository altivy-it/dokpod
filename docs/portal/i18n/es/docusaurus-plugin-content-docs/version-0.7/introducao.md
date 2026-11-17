---
title: Introducción
description: Qué es Dokpod, cómo funciona y el estado de la línea 0.7.
sidebar_position: 1
slug: /
---

**Dokpod** es una consola web autoalojada para descubrir, observar y operar
contenedores **Docker** y **Podman** en muchos servidores **Linux** y
**Windows**, sin Kubernetes y sin la nube de un proveedor.

:::warning Línea 0.7 en vista previa
La 0.7 es una versión de **vista previa**. La consola se validó de punta a
punta con Docker y Podman rootless en Linux, pero todavía no hay versión
estable ni paquete de instalación para producción. Úsela solo para
evaluación.
:::

## Qué hace Dokpod

**Operación de contenedores**

- Iniciar, detener, pausar, reiniciar y eliminar contenedores, también en lote.
- Inspección, logs en vivo con secretos enmascarados y terminal interactiva en
  el navegador, con la sesión auditada.
- Creación guiada de contenedores y descarga de imágenes de registros
  permitidos.
- Volúmenes, redes y pods de Podman, con verificación de uso antes de eliminar.
- Métricas de CPU y memoria por host y por contenedor, y eventos del motor en
  tiempo real.

**Gobernanza y seguridad**

- Permisos por entorno y por acción, decididos por Keycloak en cada
  solicitud. Quien no tiene permiso ni siquiera ve el entorno.
- Inicio de sesión con LDAP, Active Directory u OIDC mediante la federación de
  Keycloak. Dokpod no guarda contraseñas.
- Políticas por etapa (Producción, Homologación y Desarrollo): justificación
  obligatoria y registros de imágenes permitidos.
- Alertas de uso y de estado con silenciamiento, y auditoría filtrable y
  exportable en CSV.

## Cómo funciona

El plano de control (consola web, BFF, API, PostgreSQL y Keycloak) se ejecuta
en contenedores en su entorno. En cada servidor gestionado, un **agente**
ligero habla solo con el motor local y abre una **conexión gRPC saliente con
mTLS** hacia el plano de control.

- Ningún puerto de Docker o Podman queda expuesto en la red.
- El plano de control nunca accede al motor de un host remoto: solo el agente
  ejecuta comandos, siempre comandos de dominio versionados, nunca un proxy
  genérico a la API del motor.
- En Linux, el agente se ejecuta en un contenedor. En Windows, como Windows
  Service autocontenido, sin runtime .NET instalado en el host.
- Si el plano de control o el agente caen, los contenedores siguen en
  ejecución y el estado se reconcilia cuando vuelve la conexión.

## Ediciones

**Dokpod Community Edition** es gratuita y de código abierto bajo la licencia
`AGPL-3.0-only`, sin clave de licencia y sin límite de servidores, usuarios o
contenedores. La seguridad, el mTLS, la autorización por entorno y por acción
y la auditoría forman parte de la Community Edition.

## Próximos pasos

1. Revise los [requisitos y la compatibilidad](requisitos.md).
2. Vea cómo [evaluar la línea 0.7](instalacao.md).
3. Conozca la [configuración de los agentes](agentes.md).
