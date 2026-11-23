# Fase 02D — Tarjetas y compromisos programados

Fecha de negocio: 2026-11-22, America/Panama. Contrato 02D leído completo; al comenzar solo estaba sin versionar esa entrada del usuario. Se conserva el monolito, autenticación y libro de 02B/02C. Sin commits, pushes, despliegues, SMS reales, reservas automáticas, IA ni notificaciones. Sin cambios de Brevo, Cloudflare, Raspberry Pi, archivos/imágenes/configuración de Docker ni estilos globales. La identidad visual de 00B continúa rechazada.

## Funcionalidades

- **Tarjetas:** alta, consulta, edición y desactivación/reactivación de débito y crédito. Alias único por usuario, sin PAN, CVV ni credenciales. Débito vinculado a una cuenta propia. Crédito con límite, deuda inicial, fecha inicial y días mensuales de corte/pago 1–31; meses cortos interpretan el último día. No se generan intereses ni estados de cuenta.
- **Compras y pagos:** se registran directamente en TRANSACTIONS, sin un segundo libro ni gastos duplicados. Débito crea un gasto que reduce su cuenta. Compra a crédito crea gasto y deuda, con cuenta de efectivo nula. `card_payment` reduce cuenta y deuda, sin categoría ni gasto adicional. Deuda actual = deuda inicial + compras vigentes − pagos vigentes; crédito disponible = límite − deuda. Puede representarse deuda por encima del límite, disponible negativo y saldo a favor (deuda negativa). El crédito no se suma al efectivo.
- **Protección y trazabilidad:** tipo de tarjeta inmutable; cambiar apertura antes de uso exige confirmación. Tras cualquier operación se protegen permanentemente fecha/deuda inicial/cuenta vinculada. Las operaciones se corrigen por anulación trazable y nuevo registro. La tarjeta puede desactivarse sin borrar su historia; no admite nuevas operaciones estando inactiva. Movimientos muestra pagos, detalle de tarjeta y vínculos a compromisos, con auditoría existente.
- **Compromisos:** ingresos/gastos previstos con concepto, importe, categoría propia del mismo tipo, próxima fecha, frecuencia única/semanal/cada 15 días/mensual y estado activo/pausado/inactivo. Mensual conserva el día original: 31 de enero → último día de febrero → 31 de marzo. Consulta pendientes por períodos de hasta 366 días. Edición afecta previsiones futuras y no reescribe las condiciones de ocurrencias previamente confirmadas.
- **Realización:** confirmación explícita desde una cuenta propia crea una transacción y una ocurrencia vinculada, avanza la programación y persiste auditoría/idempotencia en una sola transacción. Versión vigente y lock por usuario; índice único parcial impide dos movimientos vigentes para la misma ocurrencia, incluso con escrituras directas. Anular el movimiento restaura el pendiente si el compromiso está activo; reconfirmarlo conserva el movimiento anulado y crea uno nuevo vinculado a la misma ocurrencia. La fecha real puede diferir de la prevista, respetando apertura y hoy en Panamá. Un compromiso único realizado queda sin próxima fecha; si se anula, aparece otra vez en pendientes.
- **Disponible:** saldo real de cuentas activas menos gastos pendientes del período. No se suman ingresos futuros ni crédito disponible; no se reservan ni mueven fondos hasta confirmar. Importes decimal/numeric(18,2), HTTP como cadenas, USD exclusivamente.
- **Frontend:** Tarjetas y Compromisos en la navegación existente, formularios, validaciones, estados de carga/error/vacío/éxito, compras/pagos, filtros por período, realización, pausa y desactivación. Reutiliza clases y controles existentes, sin dependencias ni estilos globales nuevos. Los reintentos conservan campos y clave. Se corrigió además un aviso de anulación anterior que podía presentarse como confirmación de la siguiente operación.

## API y persistencia

| Recurso | Rutas |
|---|---|
| Tarjetas | GET/POST `/api/finance/cards`; GET/PUT `/api/finance/cards/{id}` |
| Compras/pagos | POST `/api/finance/transactions`, con `cardId`; `kind=expense` o `card_payment` |
| Compromisos | GET/POST `/api/finance/commitments`; GET/PUT `/api/finance/commitments/{id}` |
| Pendientes/cálculos | GET `/api/finance/commitments/pending?from=YYYY-MM-DD&to=YYYY-MM-DD` |
| Realización | POST `/api/finance/commitments/{id}/confirm` |
| Detalle/anulación | Rutas existentes de transacciones; no hay DELETE financiero |

Todas las rutas requieren sesión. Escrituras con CSRF y el lock/transacción de 02B; lecturas Repeatable Read. Propietario derivado de la sesión, nunca del cuerpo. FK compuestas entre tarjetas/cuentas, compromisos/categorías, ocurrencias/programaciones/categorías y transacciones/tarjetas/ocurrencias. Recursos ajenos devuelven 404; datos inválidos 400; conflictos/versiones/duplicados 409. Categorías utilizadas por programaciones conservan su tipo.

Cada escritura nueva usa `requestId` UUID; edición agrega `version`. Reintento con igual clave/contenido devuelve el resultado original; contenido distinto devuelve conflicto. CardInput: `name`, `kind=credit/debit`, `accountId` solo débito, `openingDate`, `creditLimit`, `initialDebt`, `closingDay`, `paymentDay`, `isActive`, `confirmOpeningChange`. CommitmentInput: `name`, `kind=income/expense`, `categoryId`, `amount`, `frequency=once/weekly/fortnightly/monthly`, `nextDate`, `status`. Confirmación: `requestId`, `version`, `dueDate`, `date`, `accountId`; API permite opcionalmente `cardId` para realizar un gasto con tarjeta. La interfaz de realización usa cuenta. Tarjetas registra compras/pagos y Movimientos permite consultarlos/anularlos; no editar directamente operaciones vinculadas.

Migración **20261123001813_CardsAndCommitments** (identificador UTC): añade PAYMENT_CARDS, COMMITMENTS y COMMITMENT_OCCURRENCES, relaciones/índices y dos referencias nullable en TRANSACTIONS. Amplía Kind de 10 a 15 y hace AccountId nullable exclusivamente para compras de crédito válidas, reemplazando su check de forma. No elimina tablas/columnas ni reescribe movimientos existentes. La advertencia general de EF sobre AlterColumn se revisó; el SQL de avance amplía capacidad. [SQL idempotente 02C→02D](../infra/cards-commitments-migration.sql), [ADR 0005](DECISIONS/0005-tarjetas-compromisos.md).

**Solo aplicada en bases aisladas.** El entorno habitual conserva dos migraciones, configuración YES/USD y ninguna tabla TRANSACTIONS/PAYMENT_CARDS: [comprobación](evidence/phase02d/habitual-schema.txt). Allí siguen pendientes también 02B y 02C. No recrear sus servicios con esta versión sin respaldo y autorización de migración controlada. No ejecutar Down: reduciría esquema y podría destruir historia nueva.

## Validación final

| Comprobación | Resultado y evidencia |
|---|---|
| Backend/PostgreSQL real | **43/43 PASS**, selección de 16 casos PlanningTests + 13 TransactionTests + 13 FinanceTests + 1 de migración/seed/reinicio. [Salida](evidence/phase02d/backend-tests.txt) |
| Modelo/migración | PASS, snapshot sin cambios pendientes. [Salida](evidence/phase02d/model-check.txt) |
| TypeScript/build | PASS. [Salida](evidence/phase02d/frontend-build.txt) |
| UI específica | **16/16 PASS**, PlanningApp, TransactionsApp y CatalogApp. [Salida](evidence/phase02d/frontend-tests.txt) |
| Playwright Chromium | **2/2 PASS**, móvil 390×844 y escritorio 1440×1000. [JSON](evidence/phase02d/e2e-results.json), [salida](evidence/phase02d/e2e-run.txt) |
| Actualización con libro 02C preexistente | PASS, base nueva con esquema 02C y movimiento/revisión sintéticos; tras 02D conserva datos, snapshot y saldo 87.66. [Antes](evidence/phase02d/upgrade-before.txt), [después](evidence/phase02d/upgrade-after.txt), [resultado](evidence/phase02d/upgrade-result.json) |
| Backup/restauración | PASS, dump sintético `backups/phase02d.dump` ignorado por Git, restaurado con `--exit-on-error` a otra base nueva. 5 migraciones, 4 tarjetas, 4 compromisos, 2 ocurrencias, 10 movimientos (4 anulados), 14 revisiones, 26 claves, efectivo 870.00 y deuda 120.00. Conteos/checksums coinciden. [Origen](evidence/phase02d/restore-source.txt), [restaurado](evidence/phase02d/restore-target.txt), [hash/resultado](evidence/phase02d/backup-check.json) |

Backend cubre compras de débito/crédito, pagos sin duplicar gasto, deuda/disponible, anulaciones, exceso de límite/saldo a favor, bloqueo de apertura, montos inválidos, aislamiento HTTP y FK, CSRF, pausa/desactivación, recurrencias, cálculo sin ingresos futuros, confirmación y restauración de pendientes con snapshot original. Cuatro reintentos simultáneos crean una tarjeta y ocho confirmaciones una transacción. Un índice PostgreSQL rechaza un segundo movimiento vigente por ocurrencia. Fallo inyectado de auditoría revierte confirmación, programación, ocurrencia, dinero y clave; después admite reintento.

Playwright registra cuentas/categorías desde el flujo real autenticado con OTP local privado, tarjetas de ambos tipos, compras, pago, dos compromisos, realización, anulación consecutiva de compra y compromiso, reconfirmación, pausa y desactivación. Comprueba totales, deuda y saldo, controles bloqueados, ausencia de errores JS y desbordamiento horizontal. Capturas inspeccionadas, con identidad enmascarada:

- Tarjetas: [móvil](evidence/phase02d/cards-mobile.png), [escritorio](evidence/phase02d/cards-desktop.png).
- Compromisos: [móvil](evidence/phase02d/commitments-mobile.png), [escritorio](evidence/phase02d/commitments-desktop.png).

Se ejecutaron pruebas por riesgo, sin regresión general, hardware móvil real ni validación física ARM64. No se afirma aprobación de identidad visual ni preparación para publicación.

## Verificador nativo e incidencia resuelta

Para respetar Docker, no se construyeron imágenes, editaron archivos de infraestructura ni iniciaron/detuvieron contenedores. Se usaron .NET/Vite nativos, bases NUEVAS `alinea02d_*` en PostgreSQL existente y un puente TCP loopback 5544 mediante `docker exec`. API/WEB solo en 127.0.0.1:5084/5174. Credenciales generadas y claves únicamente en `.local/phase02d/` ignorado; ningún SMS externo. Versiones existentes: SDK .NET 10.0.404, Node 22.23.1 y PostgreSQL 18.

**Incidencia:** la primera versión del puente no esperaba a su lector auxiliar al cerrar. PostgreSQL, PID 1 del contenedor existente, heredó procesos terminados por señal y reinicializó transitoriamente sus procesos internos, interrumpiendo el arranque del verificador. Se corrigió esperando explícitamente al lector antes de salir; también se fijó content root y readiness nativos. No se sustituyó/reconstruyó/reinició el contenedor mediante Docker. Los checks finales confirman salud y esquema/configuración habitual sin migrar; la ejecución final y revisión de logs no observaron nuevas reinicializaciones tras la corrección. Esta incidencia no se oculta ni se presenta como aislamiento físico: las bases de pruebas comparten el servidor PostgreSQL existente.

Reproducir desde PowerShell:

```powershell
# Solo la primera vez: crea bases nuevas y configuración privada, sin reemplazar existentes.
node infra/verify-planning-native.cjs prepare
dotnet build backend/Alinea.Api --no-restore
# Terminal 1, mantiene puente, API y Vite; Ctrl+C para cerrar procesos nativos.
node infra/verify-planning-native.cjs serve
# Terminal 2:
node infra/verify-planning-native.cjs tests
cd frontend
npm.cmd run build
npm.cmd test -- src/features/finance/PlanningApp.test.tsx src/features/finance/TransactionsApp.test.tsx src/features/finance/CatalogApp.test.tsx
npx.cmd playwright test --config playwright.planning.config.ts
cd ..
node infra/verify-planning-native.cjs backup
```

Una nueva ejecución E2E requiere cuentas sintéticas sin registrar: con el verificador detenido, `node infra/verify-planning-native.cjs renew-browser` crea OTRA base, conserva la anterior y actualiza configuración privada; volver a iniciar `serve`. `renew-tests` hace lo mismo para la base de tests. Los fixtures de integración eliminan solo datos del prefijo aislado validado por el helper; nunca apuntar ALINEA_TEST_CONNECTION al entorno habitual. El modo `upgrade` requiere generar antes `.local/phase02d/baseline.sql` con EF script 0→FinancialTransactions, usa el fixture sintético exclusivamente en otra base NUEVA y aplica el SQL de avance 02D.

## Pendientes y límites reales

- Autorizar respaldo/actualización del entorno habitual y migraciones 02B/02C/02D. No se desplegó esta versión ni se migró esa base.
- Bases/rol de pruebas, dump y claves locales se conservan; limpieza posterior debe limitarse a los nombres `alinea02d_*` inspeccionados. No se borraron bases ni volúmenes. La restauración local sintética no demuestra recuperación externa ni de claves del piloto.
- Corte/pago son días de configuración; intereses, extractos, conciliación bancaria, notificaciones y reservas automáticas no están implementados ni incluidos en 02D. Brevo real conserva sus pendientes anteriores.
- La auditoría es inmutable por API, no frente a un administrador PostgreSQL. Sin revisión general de seguridad/accesibilidad ni pruebas de carga masiva.

Detención al terminar 02D; sin avance automático a otra fase.
