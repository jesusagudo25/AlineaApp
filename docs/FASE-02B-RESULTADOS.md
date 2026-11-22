# Fase 02B — Bancos, cuentas y categorías

Fecha: 2026-11-22, America/Panama. Contrato activo leído completo y cambios previos de Brevo conservados. Alcance limitado al primer incremento financiero. No commits, pushes, despliegues, SMS reales ni cambios en Cloudflare/Raspberry. Se inició Docker Desktop para ejecutar las pruebas locales autorizadas. No se rediseña ni aprueba la identidad visual rechazada de 00B.

## Funcionalidad

- **Bancos:** alta, listado, consulta individual, edición y desactivación/reactivación. Nombres normalizados, únicos por usuario incluso entre inactivos. No se desactiva un banco que conserve cuentas activas: devuelve 409 y explica cómo resolverlo.
- **Cuentas bancarias y efectivo:** alta, consulta, edición y desactivación/reactivación. Banco propio activo obligatorio para bancaria; efectivo sin banco y saldo no negativo. Saldo inicial decimal de hasta dos decimales, fecha de apertura y moneda USD. Se muestran saldo inicial y actual; ambos coinciden porque todavía no hay movimientos.
- **Apertura:** cualquier cambio de saldo/fecha exige confirmación explícita. Si OpeningLockedAt está establecido, se rechaza modificar apertura, tipo o banco. No hay API de desbloqueo. El futuro módulo de movimientos deberá establecer ese campo dentro de su operación atómica antes del primero; esta fase no demuestra ni implementa movimientos reales.
- **Categorías:** ingresos/gastos, alta, consulta, edición y desactivación/reactivación. Duplicados por usuario y tipo. Botón para añadir categorías iniciales, idempotente y opt-in: Salario, Otros ingresos, Alimentación y Transporte. Conserva modificaciones y desactivaciones previas. No se crean bancos, cuentas, usuarios ni importes de demostración en producción.
- **Frontend:** navegación de Bancos/Cuentas/Categorías dentro de la sesión existente, lateral en escritorio y compacta en móvil. Formularios con validación, guardado, errores 400/409, carga, vacíos, inactivos y expiración de sesión. Precisión monetaria mediante cadenas decimales y BigInt para formato; no se usa Number para calcular saldos.

Las entidades son propiedad del usuario derivado de la cookie, nunca de un UserId enviado por cliente. Todos los endpoints requieren identidad; POST y PUT además exigen antiforgery. Queries y updates filtran propietario, y una FK compuesta impide cuentas relacionadas con bancos ajenos incluso al escribir directamente en PostgreSQL. Una versión UUID evita sobrescribir ediciones obsoletas. No hay endpoints DELETE; las FK usan Restrict.

## API y migración

| Recurso | Rutas |
|---|---|
| Bancos | GET/POST `/api/finance/banks`; GET/PUT `/api/finance/banks/{id}` |
| Cuentas | GET/POST `/api/finance/accounts`; GET/PUT `/api/finance/accounts/{id}` |
| Categorías | GET/POST `/api/finance/categories`; GET/PUT `/api/finance/categories/{id}` |
| Iniciales | POST `/api/finance/categories/initialize` |

PUT incluye `version` devuelta al consultar. Desactivar usa `isActive=false`; reactivar `true`. AccountInput: `name`, `kind` (`bank`/`cash`), `bankId` o null, `openingBalance` decimal preferiblemente como cadena, `openingDate` ISO sin zona, `currency=USD`, `isActive`, `version` y `confirmOpeningChange` cuando corresponde. CategoryInput usa `kind=income/expense`. Listados incluyen inactivos; la interfaz permite filtrarlos. Recursos ajenos devuelven 404.

Migración aditiva **`20261122221226_FinancialCatalogs`**, tablas BANKS, ACCOUNTS y CATEGORIES, índices por propietario y nombre, FK/constraints y `numeric(18,2)`. Snapshot consistente. [SQL revisado](../infra/financial-catalogs-migration.sql). No cambia tablas existentes ni contiene seeds financieros. [ADR 0003](DECISIONS/0003-catalogos-financieros.md).

**Solo se aplicó en bases aisladas de pruebas.** La comprobación de la base del piloto confirma dos migraciones anteriores y ausencia de BANKS: [evidencia](evidence/phase02b/pilot-schema-unchanged.txt). Antes de aplicar en el entorno habitual, respaldar PostgreSQL y claves según [procedimiento](BACKUP_RESTORE.md), revisar SQL y ejecutar el flujo de migración controlada autorizado. Compose habitual tiene migraciones de arranque habilitadas: no recrearlo con la nueva imagen sin ese paso. No se ejecutó Down ni se eliminaron volúmenes.

## Validación ejecutada

| Prueba | Resultado | Evidencia |
|---|---|---|
| API/tests compilados | PASS, sin errores | [Build aislado](evidence/phase02b/docker-build.txt), [build tests](evidence/phase02b/backend-build.txt) |
| Integración financiera PostgreSQL real | **13/13 PASS** | [Salida](evidence/phase02b/backend-tests.txt) |
| Migración, seed y conservación tras reinicio | **1/1 PASS**, caso seleccionado de plataforma | [Salida](evidence/phase02b/migration-persistence-test.txt) |
| Formularios/HTTP y acceso afectado | **15/15 PASS**, tres archivos seleccionados | [Vitest](evidence/phase02b/frontend-tests.txt) |
| TypeScript estricto/Vite | PASS, 26 módulos | [Build](evidence/phase02b/frontend-build.txt) |
| E2E financiero, móvil y escritorio | **2/2 PASS**, recorridos completos sin HTTP mocks | [Consola](evidence/phase02b/e2e-console.txt), [JSON](evidence/phase02b/e2e-results.json) |
| Modelo y SQL | PASS, sin cambios pendientes tras migración | `dotnet ef migrations has-pending-model-changes`, SQL generado con `migrations script Authentication FinancialCatalogs` |
| Dump/restore aislado | PASS: 2 bancos, 4 cuentas y 10 categorías; cero propietarios cruzados; saldo de apertura agregado USD 330.50 | [Restore/hash](evidence/phase02b/restore-check.txt) |
| Configuración y diff | PASS | Compose financiero `config --quiet`, `git diff --check` |

Los 13 casos cubren anonimato y CSRF, consultas/ediciones entre dos usuarios, UserId manipulado, banco ajeno, duplicados normalizados y concurrentes, FK compuesta, desactivaciones y relaciones, edición obsoleta, precisión máxima `9999999999999999.99`, confirmación y bloqueo de apertura, rangos/decimales/tipos inválidos, categorías iniciales idempotentes y lectura desde un contexto nuevo.

Los dos E2E usan usuarios sintéticos y registro OTP local real (sin SMS externo), Nginx/API/PostgreSQL aislados. Prueban bancos y duplicados, bancaria/efectivo, precisión y confirmación, recarga persistida, desactivación bloqueada por cuentas, desactivación de cuenta/banco, categorías iniciales y personalizadas, edición/tipo/inactivos, navegación, foco, ausencia de overflow y bloqueo tras logout. Capturas sin OTP ni identificador de perfil; importes y nombres de ensayo pertenecen exclusivamente a esa base temporal.

Capturas inspeccionadas: [cuentas escritorio](evidence/phase02b/accounts-desktop.png), [cuentas móvil](evidence/phase02b/accounts-mobile.png), [formulario escritorio](evidence/phase02b/account-form-desktop.png), [formulario móvil](evidence/phase02b/account-form-mobile.png), [categorías escritorio](evidence/phase02b/categories-desktop.png), [categorías móvil](evidence/phase02b/categories-mobile.png). Resoluciones CSS 1440×1000 y 390×844, Chromium. No equivale a dispositivo físico ni a aprobación de identidad.

Se corrigieron errores de tipado detectados por el build (opciones de Testing Library distintas de Playwright), la concordancia del estado vacío y un selector E2E que coincidía con dos estados legítimos simultáneos (guardado/carga). El recorrido final espera la actualización de listas y pasa en ambos tamaños. El resto de la suite no se ejecutó por no corresponder al cambio.

## Reproducir

```powershell
# Backend: PostgreSQL tmpfs propio, no base del piloto.
docker compose --profile tests run --build --rm backend-tests --filter "FullyQualifiedName~FinanceTests"
# Frontend seleccionado:
cd frontend
npm.cmd run build
npm.cmd test -- src/features/finance/CatalogApp.test.tsx src/features/finance/api.test.ts src/features/auth/AuthApp.test.tsx
cd ..
# E2E: proyecto y etiquetas independientes.
docker compose -p alinea-finance-verify -f compose.yml -f infra/docker/compose.finance-verify.yml build api frontend
powershell -NoProfile -ExecutionPolicy Bypass -File infra/verify-finance-ui.ps1 -ResetTestContainers
```

El helper recrea únicamente contenedores del proyecto `alinea-finance-verify`, con PostgreSQL/keys tmpfs sin volúmenes del piloto, y autoriza solo dos teléfonos sintéticos. La opción ResetTestContainers descarta su almacenamiento temporal; nunca ejecuta down -v. Puerto de pruebas 127.0.0.1:8082. Brevo explícitamente deshabilitado, clave vacía y proveedor local en Testing. Secretos del buzón no se imprimen. La base efímera de tests de backend es independiente de la del navegador. `ALINEA_TEST_CONNECTION` nunca debe apuntar a datos reales: los fixtures eliminan exclusivamente sus tablas antes de cada caso.

Al cerrar se detuvieron únicamente el proyecto temporal de navegador y postgres-test; no se borraron volúmenes ni se sustituyó la instancia habitual. Evidencias y dump sintético ignorado por Git se conservan. El contenedor Docker del sistema quedó disponible para próximas comprobaciones.

## Pendientes y límites

- Aplicar la migración y actualizar el entorno habitual solo después de respaldo/revisión y autorización; no hay despliegue realizado en esta entrega.
- Movimientos, tarjetas y compromisos siguen fuera de alcance. La siguiente implementación deberá respetar OpeningLockedAt, moneda, fecha de apertura y aislamiento al registrar movimientos y derivar saldos. No se avanza automáticamente a ella.
- Brevo real, remitente, créditos y validación física/ARM64 conservan sus pendientes previos. Se usó exclusivamente SMS local.
- Un 409 por versión requiere recargar la lista/página y revisar la edición; no se sobrescriben cambios silenciosamente. No se ejecutó auditoría general de accesibilidad ni hardware móvil real.
- Reversión segura: conservar datos/backups y volver a binarios previos manteniendo tablas aditivas. No ejecutar Down automáticamente: borraría catálogos financieros.
