# Fase 02C — Transacciones financieras

Fecha: 2026-11-22, America/Panama. Implementación limitada al contrato 02C, conservando arquitectura, autenticación y catálogos de 02B. Sin rediseño, aprobación de identidad visual, SMS reales, commits, pushes, despliegues ni cambios en Raspberry Pi/Cloudflare.

## Funcionalidad entregada

- Ingresos y gastos con categoría propia del mismo tipo; transferencias entre dos cuentas propias distintas. USD, importe positivo decimal/numeric(18,2), hasta dos decimales y descripción opcional de hasta 500 caracteres. Referencias activas y fecha desde la apertura de ambas cuentas, hasta hoy en Panamá.
- Historial real, filtros por fechas, cuenta (origen o destino), tipo y categoría, orden descendente por fecha/ID y paginación. Incluye anulados identificados; totales del filtro excluyen anulados y transferencias. Detalle con revisiones ordenadas por número, incluso si coinciden las marcas UTC.
- Edición con versión vigente y motivo obligatorio de hasta 100 caracteres. Anulación terminal con motivo; permite corregir el efecto aunque se hayan desactivado las referencias. No existen endpoints DELETE ni rehabilitación de movimientos anulados. Revisiones con snapshot completo y propietario, insertadas junto al cambio; la FK impide eliminar un movimiento que conserve revisiones.
- Saldo actual consultado mediante agregados PostgreSQL: apertura + ingresos − gastos + transferencias recibidas − enviadas, excluyendo anulados. No hay saldo mutable duplicado. Aperturas protegidas permanentemente al primer uso; cambiar de cuenta o anular no las desbloquea. Categorías utilizadas conservan su tipo, incluidas las referencias históricas. No se añade una política de saldo insuficiente: el saldo derivado puede ser negativo.
- Escrituras autenticadas, con CSRF, transacción y el mismo advisory lock por propietario de 02B. Versiones para rechazar ediciones obsoletas. Transferencia de una sola fila con dos efectos inseparables. Clave de solicitud persistida por usuario: reintento idéntico devuelve el resultado original; cambiar contenido con la misma clave devuelve 409. Las respuestas JSON equivalen semánticamente, aunque jsonb normalice su representación textual.
- Lecturas financieras Repeatable Read: detalle/revisiones y conjunto de página/conteo/totales usan una misma instantánea. FK compuestas impiden cuentas y categorías ajenas incluso en PostgreSQL. Propietario derivado exclusivamente de la sesión.
- Interfaz en español con navegación Movimientos/Catálogos, registro rápido de gasto, formulario de los tres tipos, filtros, detalle, edición, confirmación de anulación, carga, error y vacío. Reintentos conservan campos y clave; cambiar contenido genera una clave nueva. El frontend mantiene importes como cadenas y formato BigInt.

## API

| Operación | Ruta y contrato |
|---|---|
| Historial | GET `/api/finance/transactions?from=&to=&accountId=&kind=&categoryId=&page=1&pageSize=20`; parámetros vacíos se omiten, tamaño 1–100 |
| Registro | POST `/api/finance/transactions` |
| Detalle y auditoría | GET `/api/finance/transactions/{id}` |
| Edición | PUT `/api/finance/transactions/{id}` |
| Anulación | POST `/api/finance/transactions/{id}/void` |

Registro: `requestId` UUID no vacío, `kind=income/expense/transfer`, `accountId`, `destinationAccountId` (solo transferencia), `categoryId` (solo ingreso/gasto), `amount` preferentemente cadena decimal, `date` ISO y `description`. Edición añade `version` y `reason`. Anulación usa `requestId`, `version`, `reason`. Cada operación nueva necesita su propia clave; conservarla cuando el resultado de un envío sea incierto. No reutilizarla con otro contenido. Registros ajenos devuelven 404; datos inválidos 400, conflictos 409. Escrituras requieren el antiforgery existente.

## Migración y entorno

Única migración nueva **20261122224023_FinancialTransactions**: TRANSACTIONS, TRANSACTION_REVISIONS, FINANCIAL_REQUESTS, claves compuestas de cuentas/categorías y marcador permanente de uso de categoría. Avance aditivo, sin seeds financieros. [SQL idempotente 02B→02C](../infra/financial-transactions-migration.sql), [ADR](DECISIONS/0004-transacciones.md). EF confirma snapshot sin cambios pendientes.

**Aplicada exclusivamente en bases aisladas de pruebas.** La base habitual conserva **dos migraciones y ninguna tabla BANKS ni TRANSACTIONS**: [evidencia de lectura](evidence/phase02c/pilot-schema-unchanged.txt). Por tanto también sigue pendiente allí la migración 02B; no basta con aplicar solamente 02C. El Compose habitual conserva migración automática al arrancar: no reconstruir/recrear sus servicios con esta versión sin autorización y procedimiento controlado de respaldo/migración.

E2E en proyecto independiente `alinea-transactions-verify`, puerto loopback 8083, PostgreSQL y claves tmpfs, proveedor DevelopmentMailbox en Testing y Brevo deshabilitado con clave vacía. No se sustituyeron imágenes ni contenedores habituales. No se instalaron dependencias nuevas. Herramientas: SDK .NET 10.0.404, runtime 10.0.15, Node 22.23.1; PostgreSQL 18 e imágenes fijadas existentes.

## Pruebas ejecutadas

| Verificación | Resultado y evidencia |
|---|---|
| Backend, PostgreSQL real | **27/27 PASS**: 13 casos de transacciones, 13 de catálogos afectados y uno de migración/seed/reinicio. [Salida](evidence/phase02c/backend-tests.txt), [build](evidence/phase02c/backend-build.txt) |
| Modelo y migración | PASS, [sin cambios de modelo pendientes](evidence/phase02c/model-check.txt); SQL revisado sin operaciones destructivas de avance |
| Frontend TypeScript/build | PASS, [salida](evidence/phase02c/frontend-build.txt) |
| Pruebas UI específicas | **10/10 PASS** en TransactionsApp y CatalogApp: contratos inválidos, carga/vacío/error, sesión vencida, filtros, validación, precisión, doble envío y reintento con la misma clave. [Salida](evidence/phase02c/frontend-tests.txt) |
| Playwright Chromium | **2/2 PASS**, escritorio 1440×1000 y móvil 390×844, sin errores JS ni desbordamiento horizontal. [JSON](evidence/phase02c/e2e-results.json), [salida](evidence/phase02c/e2e-run.txt) |
| Backup/restauración aislada | PASS, dump sintético en `backups/phase02c.dump` ignorado por Git. Restauración a base nueva del mismo clúster temporal con `--exit-on-error`; 4 migraciones, 6 movimientos, 10 revisiones, 10 claves, apertura total 240.00 y saldo total 340.20. Conteos, importes y checksums coinciden: [origen](evidence/phase02c/restore-source.txt), [restaurado](evidence/phase02c/restore-target.txt), [hash del dump](evidence/phase02c/backup-hash.txt) |

Backend comprueba ingresos/gastos/transferencias y saldos exactos, filtros/paginación, exclusión de transferencias en totales, edición/anulación/auditoría, protección de apertura, aislamiento de lectura/escritura y FK compuestas, autorización/CSRF, categorías utilizadas/inactivas, fechas y montos inválidos. Ocho reintentos simultáneos generan una fila; dos ediciones con la misma versión producen un éxito y un conflicto; transferencias concurrentes conservan los efectos. Un fallo inyectado al insertar la auditoría devuelve error genérico y revierte movimiento, ambas patas, locks de apertura y clave, permitiendo reintentar después. La primera aserción de replay comparaba texto JSON; se corrigió para verificar equivalencia semántica jsonb y pasó la ejecución final.

Playwright usa el registro OTP real con buzón local privado, sin SMS reales, y registra los tres tipos desde formularios. Filtra transferencias, comprueba totales, edita y anula un gasto, recarga, revisa auditoría y confirma saldo y apertura bloqueada. Identidad enmascarada en capturas:

- Historial: [móvil](evidence/phase02c/history-mobile.png), [escritorio](evidence/phase02c/history-desktop.png).
- Formulario y validación: [móvil](evidence/phase02c/movement-form-mobile.png), [escritorio](evidence/phase02c/movement-form-desktop.png).

Se inspeccionaron las capturas. Son evidencia funcional; no aprueban la dirección visual rechazada de 00B. No se ejecutó la regresión completa ni se afirma validación en hardware móvil/ARM64 real.

## Reproducir

```powershell
docker compose --profile tests up -d --wait postgres-test
docker compose --profile tests build backend-tests
docker compose --profile tests run --rm backend-tests --filter 'FullyQualifiedName~TransactionTests|FullyQualifiedName~FinanceTests|FullyQualifiedName~Migration_seed_restart_and_existing_values_are_preserved'
dotnet ef migrations has-pending-model-changes --project backend/Alinea.Api
cd frontend
npm.cmd run build
npm.cmd test -- src/features/finance/TransactionsApp.test.tsx src/features/finance/CatalogApp.test.tsx
cd ..
docker compose -p alinea-transactions-verify -f compose.yml -f infra/docker/compose.transactions-verify.yml build api frontend
powershell -NoProfile -ExecutionPolicy Bypass -File infra/verify-transactions-ui.ps1 -ResetTestContainers
```

El reset se limita al proyecto E2E temporal, sin `down -v`. Los fixtures backend eliminan datos exclusivamente de ALINEA_TEST_CONNECTION: nunca apuntarlo al entorno habitual. Al cerrar se detuvieron solo los contenedores temporales de 02C y postgres-test; evidencias y dump conservados, entorno habitual saludable.

## Pendientes reales

- Autorizar la actualización del entorno habitual y las migraciones 02B/02C después de respaldo privado, revisión y restauración controlada. No se aplicaron allí ni se desplegó esta implementación.
- La restauración verificada es local y sintética; no demuestra recuperación externa, claves privadas del piloto ni uso en hardware físico. Antes de despliegue siguen vigentes las medidas operativas de seguridad y mínimo privilegio ya documentadas.
- Brevo real mantiene sus pendientes anteriores de activación; no se modificó ni probó envío real en 02C.
- La trazabilidad es inmutable por la API; una cuenta privilegiada de mantenimiento de PostgreSQL puede modificar la base. No se afirma protección frente a un administrador de BD.

Sin bloqueos funcionales observados en las pruebas ejecutadas. No se implementan tarjetas, compromisos, IA ni otra fase. Detención al terminar 02C.
