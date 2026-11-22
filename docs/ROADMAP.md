# Alcance por fases

- **00 — Fundaciones:** plataforma local, configuración persistente, pruebas, Docker y operación documentada; validada. 00B se conserva como prototipo técnico con dirección visual rechazada.
- **01 — Autenticación OTP:** Identity, permitidos, cookies, CSRF, límites y aislamiento implementados y probados localmente.
- **02A — Brevo SMS:** adaptador integrado; envío real pendiente de aprobación, créditos y autorización.
- **02B — Bancos, cuentas y categorías:** implementados y validados con PostgreSQL y UI aislados. Migración al entorno habitual pendiente.
- **Después:** apertura de cuentas y movimientos con reglas contables, tarjetas y compromisos incrementales. Definir contratos antes de construir.
- **Más adelante:** IA, reservas, notificaciones y automatizaciones.

La exposición pública y Cloudflare requieren autorización específica y validación previa de seguridad, restauración y operación ARM64.
