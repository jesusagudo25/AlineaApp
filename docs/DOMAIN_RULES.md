# Reglas conservadas para las próximas fases

- `CONFIG_GENERAL`: clave única y valor persistente. `ACCESO_PROTEGIDO=YES` restringe registro a teléfonos permitidos; `NO` permite registro público. Ambos requieren autenticación. No añadir otro interruptor contradictorio.
- `MONEDA_PREDETERMINADA=USD`, representación `$`. Dinero: `decimal` C# y `numeric(18,2)` PostgreSQL inicialmente. Nunca float/double.
- Futuro usuario: teléfono E.164 verificado por OTP; nombre y correo opcional. Lista permitida con habilitación, revocación y auditoría. Sin teléfonos reales en seeds, código o pruebas.
- Aislamiento de datos desde servidor. Sin un usuario global para simular multiusuario.
- Apertura por cuenta con saldo y fecha de corte; movimientos posteriores no duplican saldo inicial. No se requieren 30 días históricos.
- Ingreso, gasto y transferencia son conceptos distintos; transferencia no altera ingresos/gastos. Deuda y tarjeta no son efectivo. Pagar tarjeta no duplica el gasto.
- Compromisos e ingresos programados son previsiones; confirmarlos evita duplicidad con movimientos liquidados.
- Eventos en UTC (`DateTimeOffset`); fechas calendario explícitas (`DateOnly`). Interpretación del negocio en `America/Panama`.
- Saldos deterministas son autoridad contable. IA futura recomienda.

En Fase 00 solo existe configuración global; estas reglas no autorizan implementar módulos financieros.
