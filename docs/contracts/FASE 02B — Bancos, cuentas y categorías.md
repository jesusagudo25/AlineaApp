# FASE 02B — Bancos, cuentas y categorías

Implementa el primer incremento financiero funcional de AlineaApp.

## Contexto
- Fases 00 y 01 completadas.
- Fase 02A: Brevo SMS integrado; activación real pendiente.
- Stack existente: ASP.NET Core 10, PostgreSQL y React/TypeScript.
- Moneda predeterminada: USD.
- Autenticación y sesiones existentes deben reutilizarse.
- No modificar arquitectura ni rediseñar la aplicación.

## Alcance

**1. Bancos**
- Registrar, consultar, editar y desactivar bancos asociados al usuario.
- Validar duplicados y proteger los registros por usuario.

**2. Cuentas**
- Registrar cuentas bancarias y efectivo.
- Asociar banco cuando corresponda.
- Establecer saldo inicial y fecha de apertura.
- Consultar, editar y desactivar cuentas.
- Mostrar saldo inicial y saldo actual.
- Preparar el modelo para movimientos posteriores.

**3. Categorías**
- Registrar categorías de ingresos y gastos.
- Consultar, editar y desactivar.
- Permitir categorías iniciales y personalizadas.
- Evitar duplicados por usuario y tipo.

**4. Frontend**
- Pantallas funcionales de Bancos, Cuentas y Categorías.
- Formularios con validación, estados de carga, error y vacío.
- Navegación móvil y escritorio.
- Conectar a la API y PostgreSQL reales.
- No introducir datos financieros ficticios en producción.

## Reglas técnicas
- Toda información financiera debe estar aislada por usuario autenticado.
- Montos `decimal` en C# y `numeric` en PostgreSQL.
- USD como moneda predeterminada.
- No modificar saldos iniciales silenciosamente cuando existan movimientos.
- No permitir eliminar entidades con relaciones activas.
- Migraciones EF Core aditivas y versionadas.
- Mantener los controles CSRF, autorización y seguridad existentes.
- Reutilizar componentes y patrones del proyecto cuando sean adecuados, sin imponer la identidad visual rechazada.
- No implementar transacciones, tarjetas ni compromisos todavía.

## Validación
- Pruebas específicas de persistencia, autorización, validación y aislamiento entre usuarios.
- Probar formularios con Playwright en móvil y escritorio.
- Corregir errores funcionales o visuales concretos.
- No ejecutar regresiones generales innecesarias.

## Entrega
- Funcionalidades implementadas.
- Pruebas aprobadas.
- Migraciones creadas.
- Pendientes o bloqueos reales.

No realizar commits, pushes ni despliegues. No avanzar automáticamente a Fase 02C.