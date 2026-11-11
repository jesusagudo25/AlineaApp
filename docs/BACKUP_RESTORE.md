# Backup y restauración

Los dumps contienen información sensible cuando exista información de usuarios. Guardarlos cifrados y fuera del repositorio, restringir acceso y definir retención antes de publicación. `.gitignore` excluye `backups/`. No hay programación automática ni copia externa garantizada.

Con stack local funcionando, PowerShell (dump binario, sin redirección de bytes):

```powershell
./infra/backup.ps1
```

Linux:

```sh
mkdir -p backups
docker compose exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc -f /tmp/alinea-backup.dump'
docker compose cp postgres:/tmp/alinea-backup.dump backups/alinea.dump
sha256sum backups/alinea.dump
```

Restaurar primero a una base NUEVA y desechable. No ejecutar `--clean`, `dropdb`, `down -v` ni reemplazar una base existente. El ejemplo usa `alinea_restorecheck`; si existe, `createdb` debe fallar y se debe detener el procedimiento.

```sh
docker compose cp backups/alinea.dump postgres:/tmp/alinea-restore.dump
docker compose exec -T postgres sh -c 'pg_restore --list /tmp/alinea-restore.dump'
docker compose exec -T postgres sh -c 'createdb -U "$POSTGRES_USER" alinea_restorecheck'
# Continuar SOLO si createdb terminó con código 0.
docker compose exec -T postgres sh -c 'pg_restore -U "$POSTGRES_USER" -d alinea_restorecheck --exit-on-error --no-owner --no-privileges /tmp/alinea-restore.dump'
docker compose exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d alinea_restorecheck -c "TABLE \"CONFIG_GENERAL\";"'
```

En PowerShell los comandos Docker también funcionan; para consultar sin problemas de comillas usar:

```powershell
'TABLE "CONFIG_GENERAL"; TABLE "__EFMigrationsHistory";' | docker compose exec -T postgres sh -c 'psql -U $POSTGRES_USER -d alinea_restorecheck -v ON_ERROR_STOP=1'
```

Comprobar códigos de salida en cada paso. Verificar hash, lista del archivo, esquema, historia de migración, conteos y valores esperados. Antes de despliegue repetir en otro entorno compatible y probar la aplicación contra la base restaurada. No conectar automáticamente la app al restore ni eliminar la base: su eliminación requiere una acción expresa sobre ese nombre concreto. Consultar `docs/VERIFICATION.md` para distinguir pruebas realizadas de pendientes.
