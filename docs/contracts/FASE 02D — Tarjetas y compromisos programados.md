# FASE 02D — Tarjetas y compromisos programados

Implementa exclusivamente la Fase 02D de AlineaApp, utilizando los módulos financieros existentes de 02B y 02C.

## 1. Tarjetas

- Registrar, consultar, editar y desactivar tarjetas de débito y crédito.
- Asociar tarjetas de débito a cuentas existentes.
- Para tarjetas de crédito: registrar límite, deuda inicial, fecha de corte y fecha de pago.
- Permitir asociar compras y pagos a tarjetas sin duplicar gastos.
- Calcular deuda actual y crédito disponible correctamente.
- Evitar que el pago de una tarjeta vuelva a contarse como gasto.
- Mantener trazabilidad de operaciones y aislamiento por usuario.

## 2. Compromisos programados

- Registrar ingresos y gastos previstos.
- Configurar concepto, importe, categoría, frecuencia, próxima fecha y estado.
- Admitir compromisos únicos, semanales, quincenales y mensuales.
- Consultar compromisos pendientes por período.
- Permitir marcar un compromiso como realizado mediante una transacción real vinculada.
- Evitar la duplicación de transacciones cuando se confirma un compromiso.
- Permitir editar, pausar y desactivar compromisos.

## 3. Cálculos financieros

- Mantener separados saldos reales y movimientos previstos.
- Disponible estimado = saldo disponible actual menos compromisos de gasto pendientes del período.
- No sumar ingresos futuros como dinero disponible.
- No contabilizar pagos de tarjeta como gastos adicionales.
- Utilizar exclusivamente USD, `decimal` en .NET y `numeric` en PostgreSQL.

## 4. Frontend

Implementar pantallas funcionales para tarjetas y compromisos, conectadas a API/PostgreSQL.

Reutilizar navegación, autenticación y componentes existentes. Mobile-first, formularios sencillos y estados completos.

No reconstruir el sistema visual ni introducir nuevos estilos globales. La identidad visual definitiva sigue pendiente de aprobación.

## 5. Seguridad y calidad

- Filtrar todos los recursos financieros por usuario autenticado.
- Conservar CSRF y autorización existentes.
- Utilizar migraciones EF Core aditivas.
- Evitar operaciones duplicadas mediante idempotencia.
- Probar relaciones, cálculos, anulación y aislamiento por usuario.
- Ejecutar pruebas específicas y recorridos Playwright móvil/escritorio.
- Aplicar migraciones únicamente en bases aisladas de pruebas.

## Restricciones

No modificar Brevo, Cloudflare, Docker ni Raspberry Pi.

No desarrollar IA, reservas automáticas ni notificaciones.

No realizar commits, pushes ni despliegues.

## Entrega

Resumen breve de funcionalidades, migración creada, pruebas realizadas y pendientes. Detenerse al terminar la Fase 02D.