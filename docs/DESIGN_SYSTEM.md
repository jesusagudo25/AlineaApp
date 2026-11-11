# Base de diseño provisional

Mobile-first, español y lectura tranquila. Sin balances ficticios, tarjetas decorativas, degradados ni iconografía abundante. Identidad definitiva pendiente de validación.

Tokens implementados en `frontend/src/styles.css`:

| Uso | Valor |
|---|---|
| Fondo / texto | #f7f7f2 / #202c27 |
| Texto secundario | #59685f |
| Acción y éxito | #235f46 |
| Error / pendiente | #a03e36 / #9a7325 |
| Separador | #d3d9cf |
| Focus | #306fbd, 3px y separación 4px |
| Tipografía de interfaz | system-ui, sin descarga de fuentes |
| Título editorial | Georgia; clamp 2.25–3.75rem |
| Espaciado | 4, 8, 12, 16, 24, 32, 48, 64px |

Contenedor máximo 1100px, texto máximo 720px; viewport mínimo 320px. Botón con área mínima 44px. Hover oscurece acción; active oscurece y desplaza 1px; disabled bloquea doble consulta con colores específicos; invalid usa borde de error y requerirá texto explicativo cuando haya formularios. Focus visible en enlaces y botones; estados no dependen solo del color. `role=status` anuncia cambios, `aria-busy` indica carga. Sin animación automática; respeta reducción de movimiento.

Estados implementados: carga, lista, PostgreSQL/configuración pendientes, error de red/contrato, vacío. Reintento por teclado. Las pruebas de componentes usan respuestas controladas; el E2E requiere stack real.
