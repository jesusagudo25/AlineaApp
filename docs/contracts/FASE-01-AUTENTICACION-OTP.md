# AlineaApp — FASE 01: Registro restringido y autenticación por teléfono

**Estado:** contrato propuesto. Ejecutar después de aprobar la dirección visual de Fase 00B.

## Resultado esperado
Como usuario piloto autorizado, registrarme con mi teléfono, verificar un OTP por SMS e iniciar sesión en una aplicación protegida. Al cambiar `ACCESO_PROTEGIDO` a `NO`, permitir registro de cualquier teléfono elegible, **manteniendo** la autenticación y todos los controles de seguridad.

## Reglas de negocio
- Configuración `CONFIG_GENERAL`: `ACCESO_PROTEGIDO = YES` por defecto; `MONEDA_PREDETERMINADA = USD` permanece sin cambios.
- Tabla de teléfonos permitidos con teléfono E.164 (+507...), estado, auditoría y clave única normalizada; sembrar el teléfono piloto solamente mediante una fuente local segura y configurable, nunca en código, pruebas, logs o capturas publicables.
- Si `ACCESO_PROTEGIDO=YES`: solo teléfonos explícitamente autorizados pueden completar un registro nuevo. Usuarios existentes y reglas de revocación deben definirse/validarse explícitamente: **no asumir** que cambiar la bandera revoca cuentas.
- Si `ACCESO_PROTEGIDO=NO`: registro habilitado para teléfonos elegibles. En ambos modos, verificar posesión del teléfono.
- Perfil: teléfono como identificador único confirmado; nombre y correo como datos complementarios.
- No confundir teléfono permitido con usuario registrado; no permitir duplicados ni apropiación de cuentas.

## Diseño técnico
1. Inspeccionar arquitectura, migraciones y Skills existentes antes de modificar.
2. Utilizar ASP.NET Core Identity o una alternativa justificada para usuarios/sesiones; cookies HttpOnly, Secure en HTTPS, SameSite apropiado, CSRF y expiración de sesión. Documentar estrategia de proxy/TLS con Cloudflare Tunnel/Nginx y encabezados forwarded de confianza.
3. OTP de un solo uso, corta vigencia, consumo atómico, almacenamiento no reversible, límites por número/IP/dispositivo cuando corresponda, cooldown, límite de reintentos, respuesta uniforme contra enumeración, auditoría sin secretos; el OTP no aparece en logs.
4. Definir `ISmsSender` e implementación fake restringida a desarrollo/test. **No declarar integración de SMS de producción operativa sin credenciales válidas y prueba real hacia Panamá**. Registrar decisión de proveedor y costos/requisitos antes de conectar envío real.
5. Flujos diferenciados: solicitar OTP, verificar OTP, completar registro, iniciar sesión, cerrar sesión y consultar sesión actual. Proteger endpoints privados por autenticación y pertenencia al usuario.
6. Migraciones PostgreSQL e índices/constraints adecuados; evitar aplicar permisos administrativos de BD en runtime público. Documentar flujo de migración controlada.
7. UI móvil coherente con `docs/DESIGN_SYSTEM.md`: introducir teléfono, solicitar código, reenviar con temporizador/cooldown, estados de error, carga y accesibilidad; sin falsa confirmación.
8. No desplegar al público ni enviar SMS reales sin autorización expresa. No incluir secrets en repositorio ni evidencia.

## Seguridad y pruebas mínimas
- Modo protegido permite registro solo en lista, incluso bajo concurrencia; modo público permite registro de teléfonos elegibles sin desactivar OTP.
- Repetición de OTP, expiración, código incorrecto, límite de intentos y reenvío controlados; pruebas de enumeración.
- Sesión/cookie segura; acceso no autenticado bloqueado; otro usuario no accede al perfil ajeno.
- Tests de integración con PostgreSQL real para reglas y concurrencia y pruebas UI móvil/escritorio.
- Mantener tests Fase 00, backups y checks en verde. Anotar resultados comprobados y riesgos.

## Fuera de alcance
Finanzas (cuentas, tarjetas, categorías, transacciones), notificaciones genéricas, correo transaccional de marketing, IA, lanzamiento público, plataforma de administración extensa.

## Entrega de Codex
Resumen de archivos, decisiones técnicas, migraciones y comandos reproducibles; resultados de pruebas; pendientes de SMS real; capturas de móvil/escritorio; riesgos de seguridad y plan de reversión. No commits, push ni despliegues sin autorización.
