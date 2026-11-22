# ADR 0003 — Catálogos financieros, Fase 02B

Autorizada por el contrato de Fase 02B y la instrucción del propietario. Se conserva el monolito, autenticación y dirección visual no aprobada; solo se incorporan bancos, cuentas y categorías funcionales.

- Entidades con propietario derivado de la sesión, nombres normalizados Unicode NFKC/espacios/mayúsculas e índices únicos por usuario (categorías también por tipo). Una FK compuesta banco/propietario impide asociar cuentas a bancos ajenos incluso en BD. Borrado restrictivo y sin endpoints DELETE.
- Escrituras autenticadas con antiforgery existente, transacción y advisory lock por usuario; versión UUID para rechazar ediciones obsoletas. Desactivar banco con cuentas activas devuelve 409; nunca se propaga silenciosamente una desactivación.
- Cuentas `bank` requieren banco propio activo; `cash` no admite banco. USD únicamente en este incremento. Saldo inicial decimal/numeric(18,2), fecha DateOnly; eventos UTC. Se permiten saldos bancarios negativos; efectivo no negativo. Montos HTTP como cadenas decimales para conservar precisión en JavaScript.
- Saldo actual = inicial: todavía no hay movimientos. Modificar saldo/fecha requiere confirmación explícita y solo es posible sin OpeningLockedAt. El futuro módulo de movimientos deberá fijar ese bloqueo atómicamente antes del primer movimiento, usar la misma exclusión por propietario y calcular el saldo desde su libro; no existe un endpoint que pueda desbloquearlo.
- Categorías iniciales opt-in mediante POST idempotente por usuario, sin bancos, cuentas ni importes ficticios. Sus claves de origen evitan recrear categorías iniciales editadas/desactivadas. Categorías personalizadas y duplicados se controlan por usuario y tipo.
- Migración aditiva, validación PostgreSQL real aislada y pruebas UI del flujo autenticado con SMS local. No se activa Brevo ni se envían SMS reales.
