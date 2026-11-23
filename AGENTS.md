# AlineaApp

Lee el contrato de la fase activa en `docs/contracts/` y las decisiones antes de modificar. Inspecciona entorno y cambios existentes; aplica el mínimo cambio correcto. No hacer commits, pushes, despliegues ni cambios en Cloudflare o Raspberry Pi sin autorización explícita.

- Fase actual autorizada: 02D, contrato `docs/contracts/FASE 02D — Tarjetas y compromisos programados.md`. Tarjetas de débito/crédito y compromisos explícitos sobre 02B/02C; conservar autenticación OTP y Brevo bloqueado para SMS reales. Sin IA, reservas automáticas ni notificaciones. No modificar Brevo, Docker, Cloudflare ni Raspberry Pi. Migraciones únicamente en bases aisladas de pruebas; entorno habitual requiere autorización. Fase 00B cerrada como prototipo técnico con dirección visual rechazada; conservar evidencias, sin rediseño ni identidad definitiva. Detenerse al terminar 02D.
- No credenciales ni datos personales en código, tests, documentación nueva o logs. El contrato original es una entrada del usuario; no propagar su teléfono.
- Solo las escrituras anónimas del flujo OTP/registro autorizadas por Fase 01, protegidas con CSRF y límites. Sin administración pública. `ACCESO_PROTEGIDO` limita registro; la autenticación es obligatoria. Revocar teléfono bloquea también cuentas existentes y sesiones; readmitir no rehabilita cuentas automáticamente.
- TypeScript estricto; dependencias justificadas; UI en español sin balances inventados ni decoración de dashboard genérico.
- Futuras operaciones privadas: aislamiento por usuario desde servidor; dinero C# decimal / PostgreSQL numeric(18,2); eventos UTC, fechas DateOnly, America/Panama.
- Tests por riesgo: HTTP, migraciones, persistencia, fallos de dependencias y estados UI. No sustituir PostgreSQL por InMemory para probar persistencia.
- Ejecuta los checks relevantes; registra evidencia y pendientes sin simular éxito. No borrar volúmenes ni aplicar migraciones destructivas automáticamente.

Skills de repositorio descubiertas desde `.agents/skills/*/SKILL.md`: auth-security, frontend-design, react-typescript, dotnet-backend, database-finance y quality-review. Cargar la correspondiente al cambio; referencias resueltas desde su carpeta. Son guías, no autorización para ampliar la fase.
