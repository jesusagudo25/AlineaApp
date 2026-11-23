# ADR 0004 — Transacciones, Fase 02C

- Se conserva el monolito y la autenticación. Todas las escrituras financieras comparten la transacción y advisory lock por propietario de 02B. Una transferencia es una fila con dos cuentas propias; sus efectos no pueden confirmarse por separado.
- Saldos derivados del libro vigente, sin actualizar ni sustituir la apertura. La primera operación protege permanentemente las aperturas involucradas, incluso después de anular. No se impone una política de sobregiro en esta fase.
- Importe positivo numeric(18,2), HTTP decimal como cadena, USD y DateOnly desde la apertura de ambas cuentas. No se admiten fechas futuras respecto a America/Panama: este libro registra hechos, no previsiones.
- Edición con versión vigente y motivo obligatorio; anulación terminal con motivo, sin DELETE. Cada versión guarda un snapshot de auditoría en la misma transacción. Referencias nuevas deben estar activas; anular sigue permitido aunque estén inactivas. Una categoría utilizada conserva su tipo.
- Clave UUID de idempotencia por propietario para cada operación: el mismo contenido devuelve la respuesta original; contenido distinto devuelve 409. Persistencia sin expiración para impedir duplicación tras reintentos. Cambiar contenido requiere una clave nueva.
- Historial paginado estable por fecha e identificador, con filtros; totales excluyen anulaciones y transferencias. Los saldos se consultan en PostgreSQL, no se calculan con float en el cliente.
- Migración aditiva aplicada exclusivamente a bases aisladas de pruebas. Sin despliegue, SMS real ni adopción de identidad visual definitiva.
