---
title: Actualización y backup
description: Backup verificado, actualización y rollback de Dokpod en la línea 0.7.
sidebar_position: 5
---

## Principios

- Las migrations de base de datos son **solo hacia adelante**. El rollback
  cambia imágenes y nunca revierte migrations, porque eso borraría evidencia
  de auditoría.
- El rollback es directo solo entre versiones **sin migration nueva**. En caso
  contrario, restaure el backup tomado antes de la actualización.
- Los backups contienen datos personales, la auditoría y el realm de Keycloak.
  Guárdelos fuera del repositorio, con acceso restringido.

## Backup

1. Abra una ventana de mantenimiento y detenga la API y el BFF, para que el
   backup vea un estado consistente.
2. Haga un volcado completo de la base en formato custom:

   ```bash
   podman exec <postgres> sh -c 'pg_dump -U "$POSTGRES_USER" -d <base> -Fc -Z 6' > dokpod.dump
   ```

3. Levante de nuevo la API y, después, el BFF.

## Restore verificado

1. Restaure en una base temporal, nunca sobre el origen:

   ```bash
   podman exec <postgres> sh -c 'createdb -U "$POSTGRES_USER" dokpod_restore'
   podman exec -i <postgres> sh -c 'pg_restore -U "$POSTGRES_USER" -d dokpod_restore --exit-on-error' < dokpod.dump
   ```

2. Compare la copia con el origen: cantidad y digest de la auditoría,
   historial de migrations y cantidad de filas de las tablas principales.
3. Levante una API temporal apuntando a la copia. Debe responder 200 en
   `/health/ready`.
4. Elimine la API temporal, la base de prueba y el dump del ejercicio.

## Actualización y rollback

Con una instancia de cada servicio, la consola queda indisponible durante el
cambio (unos 30 segundos en el laboratorio).

1. Antes de empezar, registre la cantidad de auditoría, la cantidad de
   entornos y la identidad de cada agente.
2. Cambie las imágenes de `api`, `bff` y `web` y recree los contenedores. En
   Podman, elimine el BFF antes que la API.
3. Verifique, en este orden:
   1. contenedores saludables, con las imágenes esperadas;
   2. consola respondiendo por el borde;
   3. cada agente reconectado, con inventario actualizado;
   4. auditoría con la misma cantidad que antes;
   5. ningún error grave en el log de la API.
4. Para volver atrás, repita el procedimiento con las imágenes de la versión
   anterior.

Siempre actualice el plano de control antes que los agentes.
