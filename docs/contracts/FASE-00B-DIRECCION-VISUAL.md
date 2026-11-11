# AlineaApp — FASE 00B: Cierre de dirección visual

## Contexto
La Fase 00 reporta stack funcional (React/TypeScript, Nginx, ASP.NET Core 10, PostgreSQL, Docker). La pantalla de fundaciones se percibe como landing genérica y NO está aprobada como referencia de diseño. Preservar la arquitectura y las verificaciones existentes.

## Objetivo
Establecer criterios visuales y patrones de interacción propios de una app de finanzas personales, con prioridad móvil, antes de implementar pantallas reales en Fase 01.

## Instrucciones para Codex
1. Inspeccionar el repositorio, AGENTS.md, `.agents/skills/frontend-design/SKILL.md`, componentes y estilos existentes. No asumir que las Skills están bien diseñadas solo porque su estructura se validó.
2. Usar `skill-creator` si está disponible para refinar `frontend-design`; si no, mejorarla manualmente. No duplicar ni instalar Skills innecesarias. Documentar el procedimiento.
3. Crear/actualizar `docs/DESIGN_SYSTEM.md` con principios y tokens (color, tipografía, espacios, radios, contraste, estados focus/hover/active/disabled/error, navegación y patrones responsive). No declarar una paleta definitiva sin validación del propietario.
4. Dirección propuesta: fintech contemporánea, cálida, sobria, verde suave/petróleo, fondo claro, densidad informativa moderada, contraste y accesibilidad adecuados. Evitar grandes hero sections, copys aspiracionales vacíos, tarjetas decorativas, gradientes y sombras exagerados.
5. Crear un prototipo de app shell responsivo: encabezado compacto, navegación móvil, jerarquía de contenido para saldo, movimientos, cuentas y compromisos. **NO inventar datos financieros reales ni representar funcionalidades no disponibles como si funcionaran.** Si se muestran ejemplos, marcarlos inequívocamente como demostración; deshabilitar acciones sin funcionalidad e indicar su estado. Conservar la verificación técnica de salud accesible solo en desarrollo, si conviene.
6. Priorizar diseño de inicio de sesión móvil para la futura Fase 01: campo telefónico con prefijo +507 como sugerencia, estados de validación y OTP; solo prototipo visual, sin simular autenticación exitosa ni enviar mensajes.
7. Verificar en tamaños 320px, teléfono actual y escritorio, con navegación por teclado, foco visible, contraste y pruebas frontend/E2E pertinentes.

## Fuera de alcance
Implementación real de autenticación, SMS, dashboards financieros, API financiera, migraciones financieras, despliegue público, cambios de stack, commits/pushes sin autorización.

## Entregables y aceptación
- Capturas comparables de móvil y escritorio y una explicación breve de decisiones visuales.
- `frontend-design` mejorada y `docs/DESIGN_SYSTEM.md` actualizado.
- Build/tests existentes sin regresiones, con resultados.
- No datos financieros simulados sin etiqueta ni botones que aparenten ser funcionales.
- Solicitar aprobación visual explícita antes de propagar componentes al resto de módulos.
