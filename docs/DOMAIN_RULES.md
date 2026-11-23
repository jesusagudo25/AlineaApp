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

Fase 01 agrega autenticación por teléfono. Revocar un teléfono bloquea también la cuenta existente, sesiones y verificaciones pendientes; volver a permitirlo no rehabilita la cuenta automáticamente. La identidad se valida en servidor incluso con registro público.

Fase 02B autoriza exclusivamente bancos, cuentas y categorías por usuario. Sin borrado físico por API; banco con cuentas activas no se desactiva. USD, decimal/numeric(18,2), saldo actual igual al inicial mientras no haya movimientos. Cambiar apertura requiere confirmación explícita y OpeningLockedAt vacío. La futura implementación de movimientos deberá establecer ese bloqueo atómicamente antes de registrar el primero; no hay operaciones de movimientos en este incremento.

En Fase 02C ese bloqueo se establece dentro de la escritura del primer movimiento y nunca se retira, aunque se edite o anule. Saldo actual = apertura + ingresos − gastos + transferencias recibidas − enviadas, excluyendo anulados. Transferencias no son ingresos ni gastos. No hay política de rechazo por saldo insuficiente: los saldos derivados pueden ser negativos. Importe estrictamente positivo, hasta dos decimales; fecha desde la apertura de ambas cuentas y hasta hoy en America/Panama. Edición y anulación requieren motivo y versión vigente; cada cambio añade una revisión. La categoría usada conserva su tipo incluso si el movimiento cambia de categoría o se anula. No hay eliminación física por API ni reapertura de movimientos anulados. Reintentos con igual clave y contenido recuperan el resultado original.
