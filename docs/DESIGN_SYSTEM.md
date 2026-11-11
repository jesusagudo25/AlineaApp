# Dirección visual propuesta · Fase 00B

**Estado: pendiente de aprobación visual del propietario.** Sustituye la pantalla de fundaciones como propuesta de UI; no declara definitiva esta paleta ni autoriza propagar componentes a otros módulos.

## Principios

AlineaApp es una herramienta financiera con lectura y controles compactos. El contenido empieza por saldo, cuentas, actividad y compromisos, sin una sección hero o texto promocional. La propuesta combina petróleo, verde suave y fondo claro cálido. La identidad se apoya en un símbolo de tres líneas alineadas, tipografía sans serif, números tabulares y navegación consistente.

Las superficies agrupan información con propósito: saldo, cuentas, listado y formulario. No hay gráficos ornamentales, sombras decorativas, gradientes ni importes inventados. En esta fase se muestran guiones, estados vacíos y etiquetas de prototipo; no existe una sesión de usuario.

## Tokens implementados

Fuente de verdad: frontend/src/styles.css. Sin fuentes remotas, nuevos paquetes de UI ni descarga de assets.

| Token / uso | Valor propuesto |
|---|---|
| Fondo de trabajo | #f3f5f2 |
| Superficie | #ffffff |
| Texto principal | #203d3b |
| Texto secundario | #596d67 |
| Petróleo / saldo | #173f3d |
| Acento | #285e52 |
| Verde suave / selección | #e1eee6 |
| Separador estructural | #dce3dd |
| Borde de campo | #80958c |
| Error | #a13732 |
| Focus | #196e91; 3px, offset 3–4px |
| Tipografía | Segoe UI, ui-sans-serif, system-ui, -apple-system, sans-serif |
| Escala de títulos | 25–32px h1; 14px sección; 21px formulario |
| Cuerpo / controles | 12–16px; anotaciones secundarias 10–11px |
| Números | tabular-nums; sin importes de demostración |
| Espaciado | 4, 8, 12, 16, 20, 24, 32, 40px |
| Radios | 6–10px controles/símbolo; 16px paneles |

El separador tenue no es el único indicador de un control: los campos usan un borde de contraste mayor. Los botones deshabilitados tienen explicación próxima y semántica disabled.

## Navegación y responsive

- **320px y teléfono actual:** encabezado compacto de 76px; cuatro destinos inferiores con icono y etiqueta: Inicio, Movimientos, Cuentas, Compromisos. Botón separado «Vista de acceso». Una columna; acciones con área mínima de 44px.
- **Desde 680px:** saldo/cuentas y actividad/compromisos forman dos columnas; acceso puede usar contexto/formulario lado a lado.
- **Desde 1024px:** navegación lateral de 218px; contenido con máximo 1250px, padding 40px y acceso en dos columnas sin hero.
- Área principal con scroll propio dentro de 100dvh. En móvil se reserva navegación y safe-area para no tapar campos o controles. En 320px los detalles secundarios del formulario pueden requerir scroll.
- Una sola navegación visible por breakpoint; aria-current identifica selección. Los cambios de vista conservan foco en el control activador. Enlace «Ir al contenido» y controles ocultos fuera del recorrido accesible.
- Verificado en 320×740, 390×844 y 1440×1000 CSS px. Móvil usa emulación con DPR 3; capturas conservan píxeles originales.
- Sin animaciones automáticas; respetar prefers-reduced-motion.

## Estados e interacción

| Estado | Patrón |
|---|---|
| Normal | Texto de alto contraste, superficies delimitadas e iconos monolineales discretos |
| Selección | Verde suave, texto petróleo y aria-current o aria-pressed |
| Hover | Cambio de superficie/acento en controles activos |
| Active | Fondo más intenso sin desplazar la composición |
| Focus | Anillo de 3px; en teléfono sobre el grupo de campo |
| Disabled | Fondos #e2e9e0 / #f1f4ef, textos específicos, disabled nativo y explicación |
| Error | Borde rojo, texto asociado, aria-describedby, aria-invalid y role=alert |
| Vacío financiero | Guiones y descripción del espacio; nunca saldo cero que parezca real |
| Salud técnica | Carga, ready, dependencia pendiente, error/reintento y vacío en desarrollo |

El acceso es un **prototipo visual**, no autenticación. Teléfono / Código SMS cambia solo la vista. +507 es una sugerencia Panamá; la comprobación de ocho dígitos demuestra un estado visual, no verifica identidad ni define reglas definitivas para otros países. OTP muestra entrada vacía y error de longitud: no se genera, envía ni verifica un código.

Labels persistentes, inputMode, ayuda próxima y texto de 16px en teléfono. Enviar código y Verificar código permanecen deshabilitados. Cambiar entre vistas descarta las entradas. No se escribe teléfono/código en almacenamiento, logs o solicitudes; no hay sesión ni transición de autenticación exitosa.

La comprobación técnica conserva el cliente HTTP y los endpoints de Fase 00, detrás de un details exclusivo de import.meta.env.DEV. La compilación estática omite el panel; la API conserva su salud real.

## Contraste y comprobación

Mediciones sobre UI renderizada en Chromium, iguales en los tres tamaños:

| Par | Relación |
|---|---|
| Texto principal / fondo | 10.69:1 |
| Ayuda secundaria / blanco | 5.51:1 |
| Error / blanco | 6.76:1 |
| Borde de campo / blanco | 3.18:1 |
| Focus / blanco | 5.71:1 |

Texto normal medido >=4.5:1; campo/focus >=3:1. No sustituye una auditoría WCAG completa ni pruebas con lector de pantalla real.

[Informe y capturas](FASE-00B-RESULTADOS.md). Componentes confinados a features/design-preview hasta aprobación visual. Fase 01 no se inicia con esta entrega.
