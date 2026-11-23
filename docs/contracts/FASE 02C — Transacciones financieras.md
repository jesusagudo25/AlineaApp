# FASE 02C — Transacciones financieras

Implementa el módulo de transacciones de AlineaApp utilizando la arquitectura y las entidades de la Fase 02B.

## Objetivo

Permitir registrar ingresos, gastos y transferencias entre cuentas, consultar movimientos y calcular saldos reales en USD.

## Funcionalidades

**1. Registro de transacciones**
- Ingresos.
- Gastos.
- Transferencias entre cuentas propias.
- Fecha, importe, cuenta, categoría y descripción.
- Validación de montos positivos y fechas válidas.

**2. Historial**
- Consultar movimientos.
- Filtrar por fecha, cuenta, tipo y categoría.
- Ordenar por fecha y permitir paginación.
- Consultar el detalle de una transacción.

**3. Modificación y anulación**
- Permitir editar transacciones mediante reglas controladas.
- Anular movimientos preservando trazabilidad.
- Recalcular saldos sin inconsistencias.
- No eliminar físicamente movimientos financieros.

**4. Saldos**
- Saldo actual = saldo inicial + ingresos − gastos + transferencias recibidas − transferencias enviadas.
- Transferencias no cuentan como ingresos ni gastos.
- Operaciones atómicas en PostgreSQL.
- Evitar que solicitudes repetidas generen movimientos duplicados.

**5. Frontend**
- Pantalla de movimientos reales.
- Formulario para registrar ingresos, gastos y transferencias.
- Consulta y filtrado del historial.
- Acceso rápido para registrar gastos desde móvil.
- Estados de carga, error, vacío y confirmación.

## Reglas técnicas
- Reutilizar autenticación y aislamiento por usuario.
- Usar `decimal` en C# y `numeric` en PostgreSQL.
- Moneda USD.
- Respetar fecha de apertura de cada cuenta.
- No permitir operaciones entre cuentas de propietarios distintos.
- Conservar integridad y trazabilidad ante edición o anulación.
- No modificar silenciosamente los saldos iniciales.
- Aplicar migraciones solo en bases aisladas de pruebas; no en el entorno habitual sin autorización.
- Mantener CSRF, autorización y validaciones existentes.
- No implementar tarjetas, compromisos ni IA.

## Validación
- Pruebas PostgreSQL para ingresos, gastos, transferencias, edición y anulación.
- Probar aislamiento por usuario y operaciones concurrentes.
- Verificar que transferencias no alteren reportes de ingresos/gastos.
- Pruebas frontend específicas.
- Playwright para registro y consulta en móvil y escritorio.
- Evitar regresiones completas innecesarias.

## Entrega
Resumen breve de funcionalidades, migración, pruebas y pendientes.

No realizar commits, pushes ni despliegues. Detenerse al terminar 02C.