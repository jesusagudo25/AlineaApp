# ADR 0001 — Base local de Fase 00

Estado: aceptada para esta implementación. Fecha de negocio: 2026-11-10.

- Un proyecto API .NET 10 con carpetas por funcionalidad; un proyecto de tests aporta valor verificando HTTP y PostgreSQL real. Sin capas vacías ni organizaciones.
- React, TS estricto y Vite; cliente HTTP con timeout, validación de respuesta y cancelación. Un único origen con proxy Vite o Nginx; CORS deniega otros orígenes por defecto. Lista exacta opcional validada al arrancar, nunca comodín ni credenciales en Fase 00.
- `/health` no depende de PostgreSQL. `/health/ready` solo revela disponibilidad, verifica conexión, migraciones y claves necesarias; no devuelve valores ni excepciones. No existen rutas de escritura ni de usuario.
- Clave primaria natural `Clave` en `CONFIG_GENERAL`, longitudes acotadas. Migraciones EF versionadas; seed con `ON CONFLICT DO NOTHING`, también al reiniciar, preserva decisiones persistidas. Migraciones automáticas solo habilitadas explícitamente para entorno local Compose; futura publicación deberá usar paso controlado de migración con cuenta separada.
- Las tablas de usuario y finanzas todavía no existen. En Fase 01 se elegirá ASP.NET Core Identity con cookies seguras y CSRF; después cada tabla privada tendrá propietario y consultas filtradas por identidad de servidor. No hay un usuario global provisional.
- .NET se compila y prueba en SDK Docker porque el host no tiene SDK. No instalar servicios del host. PostgreSQL 18 usa volumen en `/var/lib/postgresql` (layout de imagen oficial 18).
- Imágenes oficiales multiarquitectura, fijadas por digest del índice (no por digest de una sola arquitectura). Paquetes exactos y locks. SDK/Node usan BUILDPLATFORM para compilar DLL portable y JS/CSS; los runtimes siguen la plataforma destino. Revisar actualizaciones y digests conscientemente. La evidencia diferencia manifests y build ARM64 de ejecución real ARM64.
- Dos redes: datos con internal:true (API/BD) y web bridge privada (API/Nginx). Se comprobó que Docker Desktop no publica el puerto de un frontend conectado únicamente a red internal; web usa bridge normal. Único puerto publicado: frontend en loopback. Override de desarrollo expone API y BD solo en loopback. HTTP es exclusivamente local; cookies/HTTPS y proxy confiable se resolverán antes de exposición pública.
- Sin sistema de backup automatizado aún: dump binario y restauración a base desechable, verificando contenido y schema antes de cualquier despliegue. No borrar bases de usuario.

Referencias: [Npgsql 10](https://www.npgsql.org/efcore/release-notes/10.0.html), [salud ASP.NET](https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0), [migraciones EF](https://learn.microsoft.com/ef/core/managing-schemas/migrations/applying), [PostgreSQL oficial](https://hub.docker.com/_/postgres).
