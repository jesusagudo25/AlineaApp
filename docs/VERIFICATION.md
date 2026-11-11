# Evidencias de Fase 00

Fecha de negocio: **2026-11-10, America/Panama**. Algunas herramientas registran 2026-11-11 en UTC: no es una segunda ejecución futura. Se inspeccionó y leyó completo el contrato original antes de crear código. Solo existía ese archivo; no había código ni metadatos Git. Se inicializó Git local sin stage, commits, remotos ni push.

## Entorno y versiones efectivas

| Componente | Versión comprobada |
|---|---|
| Host | Windows amd64, PowerShell; SDK .NET ausente |
| Node / npm host | 22.23.1 / 12.0.2 |
| Git / Codex CLI | 2.56.0.windows.4 / 0.164.0 |
| Docker Desktop / Engine / Compose | 4.92.0 (240144) / 29.8.0 / 5.5.1 |
| Motor de contenedores | linux/amd64, contexto desktop-linux |
| SDK .NET de compilación | 10.0.404 |
| ASP.NET Core / .NET runtime | 10.0.15 / 10.0.15 |
| EF Core / Relational / Design / dotnet-ef | 10.0.15 |
| Npgsql EF provider / driver | 10.0.3 / 10.0.3 |
| PostgreSQL / Nginx | 18.6 / 1.30.6 |
| Node / npm de imagen de build | 22.23.4 / 10.9.9 |
| React / React DOM | 19.3.1 |
| TypeScript / Vite / plugin React | 7.0.2 / 8.3.7 / 6.3.0 |
| Vitest / jsdom | 5.1.0 / 31.0.0 |
| Testing Library React / jest-dom | 16.3.3 / 7.0.1 |
| Playwright / Chromium de pruebas | 1.65.0 / 158.0.8127.18 |
| xUnit / adapter / Microsoft.NET.Test.Sdk | 2.9.3 / 3.1.5 / 18.0.1 |

Versiones completas y dependencias transitivas: `frontend/package-lock.json` y los dos `packages.lock.json`. SDK fijado en `global.json`. Dockerfiles y Compose fijan **digests de índice multiarquitectura**:

| Imagen | Digest SHA256 |
|---|---|
| dotnet/sdk:10.0 | fe0fd4e93bd2fbb47c883b7a6369e5c8d3db8bf01cdc5981f6ee35fbdf3ff4fb |
| dotnet/aspnet:10.0 | bfd912c83295ce66d4b706ebb8541b9f14259b0e76fbb88b2751f58ce2399d2e |
| postgres:18 | e3d24f6906d0760bf4da0dae872415391ededefad23fd471c47dc53b0ddf3c42 |
| nginx:stable-alpine | 583b7f2fce1cdb8da5ad5c39ebd3c33b98b87d493190e548bdee1fa8c619c311 |
| node:22-alpine | 0e1754758c2df4cfa3e68d59e2e1b7374bb7fd9ea6328eaaacf15cc934af8202 |

## Resultado de aceptación

| Prueba | Resultado | Evidencia |
|---|---|---|
| Build API y tests .NET | PASS, 0 warnings / 0 errores | [Salida del SDK en Docker](evidence/dotnet-build.txt) |
| Tests backend con PostgreSQL real aislado | PASS, **10/10** | [Salida xUnit](evidence/backend-tests.txt) |
| Migración y modelo EF | PASS, sin cambios pendientes de modelo | `dotnet ef migrations has-pending-model-changes`; [SQL generado e inspeccionado](../infra/initial-migration.sql) |
| Seed y reinicio | PASS, 2 claves, 1 migración, sin duplicados | [Antes](evidence/database-initial.txt), [después de recrear contenedores](evidence/database-after-recreate.txt); test conserva además un valor existente NO tras reiniciar |
| UI typecheck y build | PASS, TS estricto; Vite compila 18 módulos | `npm run build`: JS 223.27kB, gzip 70.06kB; resumen observado en esta ejecución |
| Tests de componentes | PASS, **6/6** | `npm test`: carga, ready, dependencia pendiente, error/reintento, vacío y respuesta inválida |
| E2E contra Nginx/API/BD reales | PASS, **3/3** | Chromium: escritorio 1280px, móvil 390px, compacto 320px; sin overflow horizontal ni pageerror, navegación y reintento por teclado |
| Revisión visual | PASS | [Escritorio](evidence/ui-desktop.png), [móvil](evidence/ui-mobile.png), [320px](evidence/ui-compact.png), capturas inspeccionadas |
| Compose normal y override dev | PASS | Ambos `docker compose ... config --quiet` terminaron 0; [estado final](evidence/compose-ps.txt) |
| Stack local y proxy HTTP | PASS | `GET /health` 200 ok; `/health/ready` 200 ready a través de 127.0.0.1:8080 |
| Fallo real de BD y recuperación | PASS, liveness 200 / readiness 503 / recuperación 200 | [Smoke ejecutado](evidence/outage-recovery.txt), reproducible con `infra/verify-local.ps1 -ExerciseOutage` |
| Superficie pública / CORS / opciones | PASS | Tests HTTP: no escritura de config ni login, POST health 405; origen desconocido rechazado, origen exacto permitido sin credenciales; validadores de opciones |
| Red / procesos sin root | PASS | Solo frontend 127.0.0.1:8080 publicado; Nginx uid 101, API uid 1654. PostgreSQL sin puerto al host por defecto |
| Override de desarrollo real | PASS | TCP PostgreSQL 127.0.0.1:5432 comprobado; API 127.0.0.1:5080/health/ready devuelve 200. Luego se restauró Compose normal |
| ARM64 manifests | PASS | Las cinco imágenes oficiales incluyen linux/arm64; PostgreSQL y Nginx linux/arm64/v8 |
| Builds API y web linux/arm64 | PASS | [Imágenes y arquitecturas](evidence/arm64-images.txt), [build API](evidence/arm64-api-build.txt), [build web](evidence/arm64-web-build.txt) |
| Runtime .NET ARM64 | PASS bajo emulación | [dotnet --info](evidence/arm64-dotnet-runtime.txt): arquitectura arm64, RID linux-arm64, runtime 10.0.15; **no equivale a stack probado en Pi física** |
| Backup y restore aislado | PASS local | [Contenido del dump](evidence/backup-contents.txt), [hash](evidence/backup-hash.txt), [tablas restauradas](evidence/restore.txt) |
| Skills | PASS, 6 válidas y 6 descubiertas/habilitadas | Validador `quick_validate.py` en Python temporal: seis `Skill is valid!`; [RPC real skills/list](evidence/skill-discovery.json), errorCount 0 |
| Secretos, datos personales y referencias | PASS | [Revisión de fuentes](evidence/source-review.json): 0 coincidencias del secreto local, 0 teléfonos E.164 y 0 enlaces Markdown rotos; `.env`, original del contrato, dependencias, builds y backup ignorados por Git |
| Dependencias frontend | PASS en instalación | `npm install` y `npm ci` en imagen reportaron 0 vulnerabilidades. No representa una auditoría integral ni futura |

Los logs de Docker contienen mensajes informativos de stderr que Windows PowerShell 5 representa a veces como `RemoteException` o `NativeCommandError`, incluso en builds completados. No se usan esas etiquetas como criterio de éxito: se verificaron imágenes resultantes, arquitecturas, salidas de tests, servicios saludables y HTTP real. La primera prueba E2E falló por la red web `internal`; se corrigió y la repetición final pasó 3/3. El primer build de tests expuso conflictos EF transitivos; se fijaron Core y Relational explícitamente y el build final quedó sin warnings.

## Persistencia y operación

Migración: **20261111023309_InitialConfiguration**, producto EF 10.0.15. Crea exclusivamente `CONFIG_GENERAL` con `Clave` PK varchar(100), `Valor` requerido varchar(500). Su `Down` generado no se ejecutó. Seed posterior a migrar, atómico con `ON CONFLICT DO NOTHING`:

```text
ACCESO_PROTEGIDO      YES
MONEDA_PREDETERMINADA USD
```

La recreación de contenedores conservó `alinea-local_postgres_data`. No se ejecutó `down -v`, eliminación de volúmenes ni migración destructiva. El test de conservación usa PostgreSQL de pruebas separado, modifica temporalmente el interruptor a NO y lo restaura a YES.

Backup real local: `backups/phase00.dump` (ignorado), SHA256 `8EB7E704F82EF8009A8B0020F8A592931B2124BB2D13FFAFB330BC109BB492AC`. Se restauró con `--exit-on-error` a **alinea_restorecheck_20261110**, una base nueva del mismo clúster; contiene las dos filas y la historia de migración. Se conserva para inspección, no se borró automáticamente. No hay copias externas ni programación de backups.

Estado al entregar: stack normal saludable en **http://127.0.0.1:8080**; PostgreSQL temporal de tests detenido. Imágenes ARM64 tienen etiquetas locales separadas `alinea-phase00-api:arm64` y `alinea-phase00-web:arm64`; no se publicaron.

## Skills y contrato seguro

Se usaron las guías `skill-creator` y OpenAI Docs para creación/validación y descubrimiento. La [documentación oficial](https://learn.chatgpt.com/docs/build-skills) establece `.agents/skills` como ubicación de repositorio. Además de validar frontmatter y referencias, se consultó **Codex CLI 0.164.0 `skills/list`** sobre este cwd: seis entradas scope repo, enabled true, cero errores. Reproducir: `node infra/verify-skills.cjs` (solo RPC de lectura, sin threads ni turnos).

El original del contrato se preservó localmente y se excluyó de Git por contener un teléfono personal. La [copia pública](contracts/FASE-00-FUNDACIONES-ALINEAAPP.public.md) mantiene los requisitos y reemplaza el teléfono por un marcador. No se copió ese dato a código, tests, migraciones, seeds o evidencias.

## Pendientes y límites precisos

1. **Ejecutar el stack completo en ARM64 físico**. Se verificaron manifests, builds y host .NET emulado, no PostgreSQL/API/UI juntos en Pi real. En un entorno local ARM64 autorizado, desde una copia del proyecto con `.env` propio:

   ```sh
   docker compose config --quiet
   docker compose up -d --build --wait
   curl --fail http://127.0.0.1:8080/health
   curl --fail http://127.0.0.1:8080/health/ready
   docker compose --profile tests run --build --rm backend-tests
   ```

2. **Restauración fuera del equipo**: repetir [procedimiento](BACKUP_RESTORE.md) con copia cifrada externa, base nueva, verificación de esquema y datos, y prueba de la app apuntando a esa base. Definir retención y rotación antes del despliegue. La prueba local no demuestra recuperación ante pérdida del host.
3. **Modo .NET nativo Windows**: SDK ausente; los comandos están documentados pero no ejecutados nativamente. Windows con Docker y los puertos de desarrollo sí se verificaron. No se instalaron servicios del host.
4. **Revisión de identidad visual y accesibilidad completa**: teclado, estados y responsive comprobados; pendiente validación con lector de pantalla y diseño definitivo.
5. **Antes de exposición pública**: autenticación/autorización real, cookies/CSRF, rate limits, HTTPS, proxy confiable, credenciales BD de mínimo privilegio y migración separada, revisión de seguridad/paquetes. Son trabajo posterior, no una afirmación de aptitud pública de Fase 00.

## Decisiones y riesgos

[ADR 0001](DECISIONS/0001-fundaciones.md) registra decisiones de arquitectura, CORS, seed, redes, versiones, build multiarquitectura y backup. Riesgos actuales: HTTP y migración al inicio son conveniencias exclusivamente locales; la cuenta PostgreSQL del contenedor tiene privilegios de inicialización, inadecuados como cuenta de aplicación pública; los digests fijados requieren actualización deliberada; no hay recuperación externa probada ni validación en hardware Pi.

Próxima fase recomendada: **FASE 01 — registro restringido y autenticación OTP por SMS**, sin implementación anticipada. No se hicieron commits, pushes, despliegues ni cambios de Cloudflare/Raspberry Pi.
