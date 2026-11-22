# Entrega Fase 01

Fecha de negocio: 2026-11-10, America/Panama. Contrato completo leído y entorno/repositorio inspeccionados antes de modificar. Autorización del propietario para implementar autenticación independientemente de la dirección visual rechazada de 00B. Sin commits, pushes, despliegue público, cambios de Tunnel/Raspberry, servicios externos ni SMS reales.

## Resultado funcional

Registro restringido y público con OTP obligatorio, teléfono normalizado y único, perfil opcional, login, sesión actual y logout contra PostgreSQL. Identity gestiona cuentas/cookies y una tabla de sesiones permite revocación inmediata. La política elegida expresamente —retirar teléfono bloquea también cuentas existentes— invalida todas sus sesiones y verificaciones pendientes, incluso en modo público. Reautorizar no rehabilita automáticamente la cuenta ni sus sesiones anteriores.

OTP y pruebas de posesión HMAC, expiración, cinco intentos, consumo atómico, cooldown y límites persistentes por teléfono/IP/dispositivo. CSRF en todos los POST y perfil privado filtrado por identidad. Proveedor SMS desacoplado: Disabled por defecto y buzón efímero privado únicamente en Development/Testing. No se afirma entrega real ni validación de posesión de un teléfono piloto.

UI funcional mínima reutilizando controles existentes. Prototipo 00B en `/prototype`; sus archivos, capturas históricas, styles.css y DESIGN_SYSTEM.md permanecen intactos. La propuesta visual sigue rechazada y no define la identidad de AlineaApp.

## Evidencias comprobadas

| Comprobación | Resultado | Evidencia |
|---|---|---|
| Compilación API Release y restore bloqueado | PASS, sin errores | [Build final](evidence/phase01/api-final-build.txt), [build imágenes](evidence/phase01/docker-build.txt) |
| Integración HTTP/PostgreSQL real | **26/26 PASS**: 10 de plataforma conservados, 16 de autenticación/normalización | [Salida xUnit](evidence/phase01/backend-tests.txt), [build de tests](evidence/phase01/backend-test-build.txt) |
| TypeScript estricto y Vite | PASS, 23 módulos | [Build frontend](evidence/phase01/frontend-build.txt) |
| Pruebas de componentes/HTTP | **20/20 PASS**, incluidos 11 tests previos | [Vitest](evidence/phase01/frontend-tests.txt) |
| E2E autenticación real con proveedor local | **6/6 PASS** en 1440×1000, 390×844 y 320×740 | [Consola](evidence/phase01/e2e-console.txt), [JSON](evidence/phase01/e2e-results.json) |
| Regresión de prototipo preservado | **9/9 PASS** en su ruta independiente | [Consola](evidence/phase01/prototype-e2e-console.txt), [JSON](evidence/phase01/prototype-e2e-results.json) |
| Modelo/migración EF | PASS, sin cambios pendientes; SQL aditivo revisado | [Check](evidence/phase01/migration-check.txt), [SQL](../infra/auth-migration.sql) |
| Configuración Compose | PASS: principal, dev y verificación; sin imprimir expansión de secretos | `docker compose ... config --quiet` ejecutados con código 0 |
| Migración local tras respaldo previo | PASS, dos migraciones; YES/USD preservados; cero cuentas/teléfonos piloto sembrados | [BD local](evidence/phase01/database-local.txt), [respaldo previo y hash](evidence/phase01/backup-before-migration.txt) |
| Dump/restore en base nueva aislada | PASS: tres cuentas sintéticas, seis sesiones, tres permitidos, YES/USD y ambas migraciones | [Restauración y SHA256](evidence/phase01/backup-restore.txt) |
| Fallo real de PostgreSQL y recuperación | PASS: liveness 200, readiness 503, recuperación 200 | [Smoke](evidence/phase01/outage-recovery.txt) |
| Permisos y persistencia de claves DP | PASS: API uid 1654, directorio 0700; claves idénticas tras recrear contenedor | [Permisos](evidence/phase01/runtime-permissions.txt), [recreación](evidence/phase01/key-persistence.txt) |
| SMS sin configuración | PASS, disponibilidad `unavailable`; solicitudes 503 verificadas por test HTTP | [Estado local](evidence/phase01/sms-disabled.json) |
| Revisión de valores sensibles y preservación visual | PASS, sin coincidencias de secretos locales/teléfono original en archivos compartibles | [Revisión](evidence/phase01/sensitive-data-review.txt); diff de evidencia 00B/diseño vacío |

Backend cubre específicamente: modo protegido, cambio YES/NO entre verificación y alta, OTP incorrecto/expirado/repetido, reenvío que invalida el anterior, cinco intentos, verificación y registro simultáneos con un único ganador, pruebas con propósito/dispositivo incorrectos o vencidas, revocación de dos sesiones y de pruebas pendientes, rehabilitación explícita, expiry absoluto, logout/replay, CSRF ausente, buzón con clave/dispositivo incorrectos, límites persistidos tras recrear host, X-Forwarded-For falsificado ignorado, fallo de envío sin sesión, fake e HTTP inseguro rechazados en Production, cookies Secure/HttpOnly/Strict, perfil ajeno rechazado y fallo de BD sin detalles sensibles. Cookie y sesión sobreviven a recrear host conservando claves DP. Con cookie incidental, liveness continúa funcionando aun si BD está caída.

E2E usa Nginx/API/PostgreSQL reales en proyecto aislado, sin mocks HTTP: registro, lectura del OTP mediante buzón privado del arnés, verificación, perfil, recarga, logout, espera real de 61 segundos y login nuevo. También denegación uniforme, código inválido, cooldown, foco y ausencia de Web Storage/pageerrors/overflow. Las capturas se inspeccionaron; se corrigió una clase heredada que comprimía el formulario de escritorio y se repitieron los seis E2E con comprobación geométrica adicional. No hay captura de OTP o perfil; trazas automáticas desactivadas.

Capturas funcionales de formulario vacío: [escritorio](evidence/phase01/auth-desktop.png), [móvil](evidence/phase01/auth-mobile.png), [320px](evidence/phase01/auth-compact.png). Demuestran operabilidad responsive, no aprobación de una identidad visual.

## Archivos y decisiones

- `backend/Alinea.Api/Features/Auth/`: entidades Identity/permitidos/OTP/sesiones/límites/auditoría, servicio transaccional, proveedor, endpoints, opciones, cookies y CLI administrativa.
- `Infrastructure/AlineaDbContext.cs`, migración `20261111035915_Authentication`, snapshot y `infra/auth-migration.sql`: esquema aditivo. Configuración de Fase 00 sin reset.
- `Program.cs`, csproj y locks: wiring y Identity.EntityFrameworkCore **10.0.15**, alineado con EF existente; sin nuevas dependencias frontend.
- `frontend/src/features/auth/`, HTTP compartido y main: flujo funcional en español; `/prototype` conserva el anterior. Vite y Nginx incorporan `/api/`.
- Compose/Docker: claves privadas persistidas, configuración opt-in local y plataforma de verificación tmpfs separada. Helpers `init-auth-env.ps1` y `verify-auth-ui.ps1` sin secretos impresos.
- [ADR 0002](DECISIONS/0002-autenticacion-otp.md), [operación y comandos reproducibles](FASE-01-AUTENTICACION.md), README/arquitectura/reglas/backup/AGENTS actualizados al alcance autorizado.

Entorno: Windows amd64, PowerShell, Node 22.23.1/npm 12.0.2. SDK .NET ausente en host: builds/tests mediante Docker Linux amd64, SDK 10.0.404, runtime/ASP.NET/EF/Identity 10.0.15, Npgsql 10.0.3, PostgreSQL 18.6. React 19.3.1, TypeScript 7.0.2, Vite 8.3.7, Vitest 5.1.0 y Playwright 1.65.0 conservados. No instalaciones de servicios del host.

Incidencias resueltas: se preservaron los argumentos de entorno de ASP.NET al filtrar comandos propios de CLI; se corrigieron el montaje tmpfs de claves y el estado inicial del temporizador. Los analizadores xUnit exigieron usar la sobrecarga filtrada de Assert.Single. La política PowerShell impide npm.ps1: se verificó con npm.cmd, sin modificar políticas globales. Algunas salidas .NET contienen aviso de biblioteca Kerberos opcional ausente (`libgssapi_krb5`); la conexión con contraseña, migraciones y tests PostgreSQL funcionan y terminaron 0. No se usa autenticación Kerberos. Los avisos DP en factories sin ruta pertenecen a contenedores de pruebas efímeros; el runtime local tiene volumen 0700 verificado.

## Pendientes reales y reversión

SMS real: seleccionar proveedor, cotización vigente para Panamá, requisitos de remitente/consentimiento, credenciales privadas y prueba expresamente autorizada. No hay integración externa operativa. El teléfono real del piloto no se ha sembrado ni usado. Permisos runtime/migración separados, TLS/cadena exacta de proxies, protección y respaldo cifrado de claves siguen siendo requisitos previos a publicación. El volumen local DP no añade cifrado en reposo por sí mismo. El dump verificado no respalda esas claves.

El advisory lock global y el envío dentro de la transacción son adecuados para el piloto; antes de un proveedor de red o alto volumen se requiere revisar tiempos, contención, fallos de entrega y resistencia temporal a enumeración. Los límites usan ventanas fijas. No se validan operadora o existencia real de un teléfono con una expresión E.164. No se ejecutó build ARM64 de esta Fase 01 ni stack sobre Raspberry física; evidencias ARM64 anteriores no se presentan como prueba de esta versión.

Reversión: preservar backups/claves, detener acceso y recuperar binarios previos manteniendo las tablas aditivas. No se ejecutó Down ni se eliminaron volúmenes. Cualquier eliminación/restauración sobre datos reales requiere autorización y verificación independiente. El flujo nunca debe sustituirse por un usuario ficticio ni acceso sin autenticar.
