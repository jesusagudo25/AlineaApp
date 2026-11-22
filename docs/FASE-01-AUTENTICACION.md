# Fase 01 — autenticación local

Implementación funcional independiente de la propuesta visual rechazada en 00B. Se reutilizan clases y controles necesarios sin aprobar paleta ni componentes como identidad definitiva. El prototipo y sus evidencias permanecen conservados en `/prototype` y `docs/evidence/phase00b/`. No hay módulos financieros ni servicios externos integrados.

## Funcionamiento

Identity/EF almacena cuentas con teléfono normalizado y único, confirmado únicamente después de OTP. Nombre y correo son opcionales; el correo no queda confirmado. `ACCESO_PROTEGIDO=YES` exige lista de permitidos al solicitar y completar registro; `NO` permite registro sin lista, manteniendo OTP y autenticación. Valores ausentes o inválidos fallan cerrados. Se preserva USD.

La política solicitada por el propietario queda implementada: revocar teléfono marca su fila como deshabilitada, bloquea la cuenta, invalida OTP/pruebas pendientes y revoca todas sus sesiones. **No borrar la fila**: representa la revocación incluso en modo público. Volver a permitir el teléfono no rehabilita la cuenta; necesita `--enable-user`, y nunca recupera sesiones anteriores. Cambiar YES/NO no revoca cuentas por sí mismo.

OTP criptográfico de seis dígitos, cinco minutos, cinco intentos, HMAC SHA256 con clave externa y comparación constante. Prueba de posesión separada de cinco minutos y de un uso para registro/login. Transacciones PostgreSQL con advisory lock global compartido entre procesos aseguran consumo y alta únicos. Es una decisión para el piloto; el lock global y envío síncrono dentro de la transacción requieren revisión antes de alto volumen o de incorporar una red SMS real.

Solicitudes: cinco por teléfono, veinte por IP y diez por dispositivo cada ventana fija de quince minutos; cooldown de sesenta segundos por teléfono. Cada intento de solicitud cuenta, incluidos los reenvíos prematuros. Verificaciones: cien por IP y cincuenta por dispositivo por ventana, además de cinco intentos por OTP. Los límites persisten en PostgreSQL y usan identificadores HMAC, sin IP/teléfono en auditoría. Solicitudes sintácticamente válidas responden 202 con forma y mensaje uniformes, sin revelar elegibilidad, existencia o bloqueo. No se promete igualdad temporal para un futuro proveedor de red.

Cookie `Alinea.Session` HttpOnly, SameSite Strict, Secure por defecto, treinta minutos absolutos, sin renovación deslizante. Cada petición comprueba sesión, cuenta, teléfono revocado y security stamp en servidor. Logout revoca la sesión persistida. Todos los POST exigen antiforgery mediante cookie HttpOnly y `X-CSRF-TOKEN`. El frontend obtiene token nuevo antes de escribir, no guarda OTP/pruebas/sesión en Web Storage, y consulta el perfil real tras autenticar.

## API

| Ruta | Comportamiento |
|---|---|
| GET `/api/auth/status` | Disponibilidad real del mecanismo de envío |
| GET `/api/auth/csrf` | Token CSRF y cookie de dispositivo |
| POST `/api/auth/otp/request` | `{phone,purpose:register\|login}`; 202 uniforme o 503 si envío deshabilitado |
| POST `/api/auth/otp/verify` | `{challengeId,code}`; prueba de posesión o error genérico |
| POST `/api/auth/register` | `{verificationToken,name?,email?}`; crea cuenta/sesión si sigue autorizada |
| POST `/api/auth/login` | `{verificationToken}`; sesión de cuenta confirmada y habilitada |
| GET `/api/auth/session` | Perfil de la identidad autenticada; anónimo 401 |
| POST `/api/auth/logout` | Revoca sesión; exige identidad y CSRF |
| GET `/api/profile/{userId}` | Solo propietario; perfil ajeno 404 |

No hay API administrativa. CORS sigue cerrado a escrituras/credenciales de otros orígenes. Nginx y Vite reenvían `/api/` al backend; las respuestas de API no caen en el fallback HTML. Cuerpos limitados a 16 KiB en Nginx/Kestrel.

## Configuración y uso local

Conservar `.env` y la contraseña PostgreSQL existente. Desde la raíz:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File infra/init-auth-env.ps1
docker compose config --quiet
docker compose build api frontend
# Antes de aplicar la migración en una base existente:
powershell -NoProfile -ExecutionPolicy Bypass -File infra/backup.ps1
docker compose up -d --wait
```

El helper añade claves aleatorias de 256 bits que falten, sin mostrarlas ni sobrescribir valores existentes. No usar `docker compose config` sin `--quiet`, imprimir `.env` ni compartir inspecciones de variables. Para habilitar explícitamente el buzón local al crear la configuración: `infra/init-auth-env.ps1 -EnableLocalSms`; si `SMS_PROVIDER` ya existe, editarlo localmente sin imprimir secretos. El valor por defecto es `Disabled`: la API devuelve 503 al solicitar código, nunca un envío ficticio exitoso.

`DevelopmentMailbox` implementa `ISmsSender`, guarda códigos solo en memoria durante cinco minutos y **no envía SMS**. Se admite únicamente en Development/Testing con clave `DEV_SMS_INBOX_KEY` privada. GET `/api/development/sms/{challengeId}` requiere header `X-Local-Sms-Inbox` correcto y cookie del mismo dispositivo. Esta ruta no existe en Production. No introducir la clave en variables VITE ni en el navegador de la aplicación; el arnés de pruebas la usa desde Node. Al reiniciar se pierde el buzón y debe solicitarse otro código tras el cooldown. No hay código maestro.

Compose local enlaza exclusivamente a loopback y permite HTTP mediante `Auth__AllowInsecureLocalHttp=true`, validado solo en Development/Testing. Para ejecución nativa configurar `Auth__OtpKey`, entorno local explícito y esa opción, además de la conexión PostgreSQL; Vite usa mismo origen con proxy. En Linux generar las claves fuera del historial con un gestor de secretos o archivo privado; OTP_KEY debe contener al menos 64 caracteres hexadecimales de longitud par. El helper PowerShell es la opción verificada en este equipo.

Para administrar, cargar `ALINEA_ADMIN_PHONE` desde fuente local privada, sin escribir el número literal en comandos guardados o transcripciones:

```powershell
# ALINEA_ADMIN_PHONE ya cargado de forma privada en esta sesión:
docker compose run --rm -e ALINEA_ADMIN_PHONE api --allow-phone
# Alternativas: --revoke-phone, --disable-user, --enable-user
Remove-Item Env:ALINEA_ADMIN_PHONE
```

Las operaciones normalizan E.164, auditan el evento con HMAC y no imprimen el sujeto. Nunca sembrar el teléfono real del piloto en migraciones, tests, capturas o documentación. Los datos `+999…` del arnés son sintéticos y solo viven en bases efímeras, sin destinatario SMS.

## Migración, claves y operación futura

Migración aditiva `20261111035915_Authentication`: tablas Identity, permitidos, desafíos, sesiones, límites y auditoría; índice único de teléfono. `InitialConfiguration` y su seed idempotente se conservan. Nueva dependencia: `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.15, misma versión EF; locks actualizados. No dependencias nuevas en frontend.

Compose **local** conserva la inicialización automática para ejecutabilidad. En un futuro runtime público usar `Database__ApplyMigrationsOnStartup=false` y ejecutar `api --migrate` como tarea de mantenimiento con credencial separada. La cuenta runtime necesita CONNECT/USAGE, lectura de configuración/permitidos, y SELECT/INSERT/UPDATE/DELETE en tablas de autenticación más USAGE/SELECT en sus secuencias. No debe ser dueña de esquema/tablas ni tener CREATE, ALTER, DROP, CREATEDB, CREATEROLE o superuser. La administración CLI usa credencial de mantenimiento con escritura en permitidos. No se han creado roles ni cambiado privilegios del servidor piloto en esta tarea.

Volumen `auth_keys` privado, directorio `/app/keys` uid 1654 modo 0700: conserva Data Protection entre recreaciones. Los archivos XML no tienen cifrado adicional en esta base local: requieren protección del host/volumen y respaldo cifrado fuera del repo antes de publicación. Respaldar juntos PostgreSQL, claves DP y secretos de configuración en almacén seguro; un pg_dump no incluye esos archivos. Rotar OTP_KEY invalida desafíos, pruebas y correlación de límites previos; planificar fuera de una ventana activa. No eliminar volúmenes para solucionar autenticación.

Forwarded headers se ignoran por defecto. Solo `Auth__KnownProxies__N` con IP exacta habilita el middleware, ForwardLimit=1; jamás confiar en todas las redes. Nginx reemplaza X-Forwarded-For/Proto del cliente. Antes de Cloudflare/TLS hay que definir la cadena real de proxies y limitar el acceso al origen, establecer HTTPS efectivo y cookies Secure; no basta confiar en encabezados enviados por un cliente. Sin cambios en Cloudflare, Raspberry o servicios externos.

Retención oportunista al solicitar OTP: límites vencidos, desafíos con más de un día desde vencimiento, sesiones con más de un día desde vencimiento y auditoría de más de treinta días. No se borran usuarios ni configuración. Para operación continua será necesario acordar retención y mantenimiento independiente del tráfico.

## Pruebas reproducibles

```powershell
docker compose --profile tests run --build --rm backend-tests
cd frontend
npm.cmd run build
npm.cmd test
cd ..
docker compose build api frontend
powershell -NoProfile -ExecutionPolicy Bypass -File infra/verify-auth-ui.ps1
```

El proyecto `alinea-auth-verify` usa PostgreSQL y claves temporales, puerto 8081, sin compartir datos con `alinea-local`. El perfil backend-tests usa otra BD tmpfs. Las pruebas borran sus tablas de autenticación: **ALINEA_TEST_CONNECTION nunca debe apuntar a una base real**. Para repetir E2E partir de un contenedor PostgreSQL de verificación nuevo (su tmpfs es desechable); no tocar volúmenes del piloto. El login E2E espera realmente 61 segundos para respetar el cooldown. Trazas/capturas automáticas desactivadas para no conservar OTP; solo se capturan formularios vacíos. El prototipo anterior se prueba en `/prototype`, con nuevas evidencias en phase01, preservando phase00b.

Resultados concretos, limitaciones y capturas: [entrega de Fase 01](FASE-01-RESULTADOS.md). Decisiones: [ADR 0002](DECISIONS/0002-autenticacion-otp.md).

## Pendientes y reversión

SMS real pendiente: elegir proveedor con entrega a Panamá, verificar país/remitente/números admitidos, consentimiento y requisitos regulatorios, presupuesto por segmento y recargos, límites antifraude y política de almacenamiento; cotización vigente antes de implementar adaptador. Después configurar credenciales fuera del repo y realizar una prueba real expresamente autorizada. No hay proveedor, cuenta externa ni coste contratado en esta entrega. No se declara posesión de un teléfono real validada mediante el buzón local.

Antes de publicación: roles BD separados, TLS/proxy exacto, cifrado/backup de claves, revisión del transporte SMS y temporización contra enumeración, pruebas de capacidad y ejecución ARM64 física. SMS no debe habilitarse en red sin esos pasos.

Reversión segura: detener acceso, preservar backup y claves, volver a binarios anteriores manteniendo tablas aditivas y sesiones inaccesibles. No ejecutar Down de Authentication ni borrar tablas/volúmenes automáticamente; eliminaría usuarios y requeriría autorización y restauración verificada. Si la nueva autenticación falla, no abrir acceso privado ni sustituirla por identidad ficticia.
