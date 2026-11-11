---
name: auth-security
description: Revisar cambios de acceso, registro, sesiones, CORS o exposición de datos en AlineaApp.
---

Lee [arquitectura](../../../docs/ARCHITECTURE.md) y [reglas](../../../docs/DOMAIN_RULES.md) antes de cambiar la superficie HTTP.

- [ ] En Fase 00 no introducir login, OTP, usuarios simulados ni escritura pública de configuración.
- [ ] ACCESO_PROTEGIDO controla registro; nunca deshabilita autenticación. No duplicar el interruptor.
- [ ] No copiar datos del piloto del contrato a código, tests, documentación o logs.
- [ ] Ante nuevas rutas privadas exigir identidad y aislamiento en servidor; no confiar en userId del cliente.
- [ ] Para Fase 01 planificar Identity, cookies HttpOnly/Secure, SameSite, CSRF, límites OTP y pruebas entre usuarios. No JWT en localStorage.
- [ ] Probar rutas y orígenes rechazados, dependencia caída y ausencia de secretos en errores.

No exponer servicios ni modificar Tunnel sin autorización de despliegue.
