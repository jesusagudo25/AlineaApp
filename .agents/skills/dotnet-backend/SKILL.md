---
name: dotnet-backend
description: Modificar endpoints, DI, opciones o infraestructura de la API ASP.NET Core 10 de AlineaApp.
---

Lee [arquitectura](../../../docs/ARCHITECTURE.md) y [ADR](../../../docs/DECISIONS/0001-fundaciones.md).

- [ ] Mantener un monolito por funcionalidades; evitar nuevos proyectos sin comportamiento.
- [ ] Validar opciones al inicio sin mostrar cadenas de conexión. CORS exacto, cerrado por defecto.
- [ ] Separar liveness de readiness; fallos públicos genéricos y logs estructurados sin mensajes sensibles.
- [ ] No crear endpoints públicos de escritura ni perfiles autenticados de prueba.
- [ ] Compilar API y ejecutar tests HTTP con PostgreSQL real mediante perfil Compose tests.

No instalar servicios del host si el SDK y PostgreSQL de Docker bastan.
