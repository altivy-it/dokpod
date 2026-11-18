---
title: Instalación
description: Instalar Dokpod con el instalador guiado en Linux con Docker o Podman rootless.
sidebar_position: 3
---

El instalador guiado lleva un host Linux con Docker Engine o Podman en modo
**rootless** de cero a Dokpod en ejecución. Es idempotente: ejecutarlo de nuevo
reconcilia la instalación en lugar de recrearla.

## Antes de empezar

- Revise los [requisitos del instalador](requisitos.md#instalador-guiado).
- Descargue `install.sh` y `release-manifest.json` de la release, o el paquete
  `--offline`. `install.sh` verifica el digest y la firma de cada imagen antes
  de cualquier efecto y se detiene con código 5 cuando algo no coincide.

## Escenarios

| Escenario | Respuestas | Lo que levanta el instalador |
| --- | --- | --- |
| Todo nuevo | `postgres.mode: provision`, `keycloak.mode: provision`, `edge.mode: provision` | PostgreSQL, Keycloak, borde nginx, API, BFF, web y el agente local |
| PostgreSQL y/o nginx existentes | `postgres.mode: existing-create` o `existing-prepared`; `edge.mode: existing` | lo mismo, sin la base y/o sin el borde |
| Keycloak existente | `keycloak.mode: existing` | lo mismo, sin Keycloak y su base |

Las combinaciones son válidas. Lo que ya es suyo (`existing`) nunca se recrea
ni se elimina.

## Ejecutar

Sin `--answers`, el modo guiado pregunta todo en la terminal. Con un archivo de
respuestas, no se pregunta nada, y una respuesta obligatoria faltante termina
con código 2.

```bash
sh install.sh plan --answers respuestas.yaml        # muestra el plan, sin efecto
sh install.sh install --answers respuestas.yaml --yes
```

Los secretos nunca entran en el archivo de respuestas: cada contraseña es
`{ file: <ruta> }` o `{ env: <variable> }`. Las credenciales administrativas de
un PostgreSQL o Keycloak existente pueden quedar fuera; en ese caso, la
terminal las pide y se usan solo en esa ejecución.

`--yes` confirma solo el plan normal. Migrations, backup, SQL superpuesto y
eliminación de datos tienen confirmación propia.

## Migrations revisadas por el DBA

1. `sh install.sh migrations plan` lista las migrations pendientes, sin tocar
   la base.
2. `sh install.sh migrations export --out pendientes.sql` genera el SQL exacto.
   El DBA lo revisa y lo aplica con
   `psql -v ON_ERROR_STOP=1 -f pendientes.sql`.
3. `install` o `upgrade` con `--manual-migrations` se pausa en esa etapa con
   código 10. Después de aplicar el SQL, el mismo comando continúa y verifica
   el schema.

## Certificados

`sh install.sh certificates check` lista la validez de la CA, del borde, de la
API y de cada agente. Cerca del vencimiento (30 días por defecto), el
instalador sugiere la acción: `recreate pki:<item>` con la CA local, un paquete
nuevo para un host remoto, o solo el aviso para un certificado de la
organización.

## Desinstalación

El instalador no elimina datos por su cuenta. Para desinstalar:

1. Haga el [backup](atualizacao-e-backup.md) de la base y de Keycloak.
2. Detenga y elimine los contenedores, conservando los volúmenes:
   `docker compose -f <installDir>/compose.yaml -p dokpod down`.
3. Solo si quiere borrar los datos, elimine los volúmenes de Dokpod.
4. Borre `installDir` y `secretsDir`. `secretsDir` contiene la clave de la CA,
   las contraseñas y las conexiones.

PostgreSQL, Keycloak y nginx que ya eran suyos no se tocan.
